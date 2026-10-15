using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace HotelTogether
{
    // Art direction for the bright toy-like daytime hotel and the neon "Zombie Inn"
    // night. Runtime-only volume profiles blend with the day/night cycle.
    [DefaultExecutionOrder(500), DisallowMultipleComponent]
    public sealed class HotelVisualDirector : MonoBehaviour
    {
        public static HotelVisualDirector Instance { get; private set; }

        [Header("Daytime: sunny, saturated, soft shadows")]
        public Color daySun = new Color(1f, .955f, .87f);
        public Color daySky = new Color(.74f, .84f, .98f);
        public Color dayEquator = new Color(.66f, .64f, .62f);
        public Color dayGround = new Color(.52f, .44f, .36f);
        [Range(0, 1)] public float dayShadowStrength = .78f;

        [Header("Night: moonlit blue with neon glow")]
        public Color nightSun = new Color(.42f, .52f, 1f);
        public Color nightSky = new Color(.22f, .25f, .55f);
        public Color nightEquator = new Color(.16f, .14f, .34f);
        public Color nightGround = new Color(.10f, .07f, .17f);
        [Range(0, 1)] public float nightShadowStrength = .62f;

        /// <summary>0 = full day, 1 = full night (dusk/dawn are in between).</summary>
        public float NightBlend { get; private set; }
        /// <summary>Title shots can preview the night. Negative = follow the hotel clock.</summary>
        [System.NonSerialized] public float nightOverride = -1;
        /// <summary>0..1 cinematic depth of field and letterbox grade used by the title camera.</summary>
        [System.NonSerialized] public float cinematicWeight;
        [System.NonSerialized] public float focusDistance = 8;

        HotelGuestSimulation hotel;
        Light sun;
        float searchAt, baseSunIntensity = -1;
        VolumeProfile dayProfile, nightProfile, cinemaProfile;
        Volume dayVolume, nightVolume, cinemaVolume;
        Bloom dayBloom, nightBloom;
        DepthOfField depth;
        float previousShadowStrength = -1;
        Color previousSky, previousEquator, previousGround;
        bool capturedAmbient;
        ParticleSystem motes;
        Material moteMaterial;
        Texture2D moteTexture;
        float moteDensity = 1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            SceneManager.sceneLoaded -= Loaded; SceneManager.sceneLoaded += Loaded;
            Ensure(SceneManager.GetActiveScene());
        }
        static void Loaded(Scene scene, LoadSceneMode mode) { Ensure(scene); }
        public static HotelVisualDirector Ensure(Scene scene)
        {
            if (!Application.isPlaying || !scene.IsValid() || scene.name != "HotelLobby") return null;
            if (Instance) return Instance;
            var root = new GameObject("Hotel visual director");
            SceneManager.MoveGameObjectToScene(root, scene);
            return root.AddComponent<HotelVisualDirector>();
        }

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(this); return; }
            Instance = this;
            BuildProfiles();
            BuildMotes();
            HotelPerformanceDirector.TierChanged += OnTierChanged;
        }

        void BuildProfiles()
        {
            // ---- Day: clean whites, punchy toy colours, warm highlights and cool shadows.
            dayProfile = NewProfile("Hotel daytime grade");
            dayProfile.Add<Tonemapping>(true).mode.value = TonemappingMode.Neutral;
            var color = dayProfile.Add<ColorAdjustments>(true);
            color.postExposure.value = .32f; color.contrast.value = 14; color.saturation.value = 22; color.colorFilter.value = new Color(1, .99f, .965f);
            var white = dayProfile.Add<WhiteBalance>(true); white.temperature.value = 5; white.tint.value = 1;
            var smh = dayProfile.Add<ShadowsMidtonesHighlights>(true);
            smh.shadows.value = new Vector4(.95f, .99f, 1.10f, 0); smh.midtones.value = new Vector4(1.01f, 1, .99f, 0); smh.highlights.value = new Vector4(1.04f, 1.02f, .96f, 0);
            var lgg = dayProfile.Add<LiftGammaGain>(true); lgg.lift.value = new Vector4(1, 1, 1.02f, .015f); lgg.gain.value = new Vector4(1, 1, 1, .02f);
            dayBloom = dayProfile.Add<Bloom>(true);
            dayBloom.intensity.value = .42f; dayBloom.threshold.value = .95f; dayBloom.scatter.value = .66f; dayBloom.tint.value = new Color(1, .96f, .9f);
            var vignette = dayProfile.Add<Vignette>(true); vignette.intensity.value = .2f; vignette.smoothness.value = .48f; vignette.color.value = new Color(.13f, .08f, .12f);
            dayProfile.Add<ChromaticAberration>(true).intensity.value = 0;
            dayProfile.Add<FilmGrain>(true).intensity.value = 0;

            // ---- Night: violet-blue shadows, neon bloom, a hint of lens character.
            nightProfile = NewProfile("Hotel Zombie Inn night grade");
            var nColor = nightProfile.Add<ColorAdjustments>(true);
            nColor.postExposure.value = .22f; nColor.contrast.value = 20; nColor.saturation.value = 30; nColor.colorFilter.value = new Color(.84f, .9f, 1.12f);
            var nWhite = nightProfile.Add<WhiteBalance>(true); nWhite.temperature.value = -20; nWhite.tint.value = 9;
            var nSmh = nightProfile.Add<ShadowsMidtonesHighlights>(true);
            nSmh.shadows.value = new Vector4(.88f, .94f, 1.30f, -.02f); nSmh.midtones.value = new Vector4(.97f, 1, 1.07f, 0); nSmh.highlights.value = new Vector4(1.06f, 1, 1.10f, .03f);
            var nLgg = nightProfile.Add<LiftGammaGain>(true); nLgg.lift.value = new Vector4(.98f, .98f, 1.06f, .01f); nLgg.gain.value = new Vector4(1, 1, 1, 0);
            nightBloom = nightProfile.Add<Bloom>(true);
            nightBloom.intensity.value = 1.15f; nightBloom.threshold.value = .72f; nightBloom.scatter.value = .74f; nightBloom.tint.value = new Color(.86f, .9f, 1);
            var nVignette = nightProfile.Add<Vignette>(true); nVignette.intensity.value = .36f; nVignette.smoothness.value = .5f; nVignette.color.value = new Color(.02f, .02f, .09f);
            nightProfile.Add<ChromaticAberration>(true).intensity.value = .07f;
            var grain = nightProfile.Add<FilmGrain>(true); grain.type.value = FilmGrainLookup.Thin1; grain.intensity.value = .16f; grain.response.value = .85f;

            // ---- Cinematic: shallow focus for the title camera.
            cinemaProfile = NewProfile("Hotel title cinematic");
            depth = cinemaProfile.Add<DepthOfField>(true);
            depth.mode.value = DepthOfFieldMode.Bokeh; depth.focusDistance.value = 8; depth.aperture.value = 3.2f; depth.focalLength.value = 58; depth.bladeCount.value = 6;
            var cVignette = cinemaProfile.Add<Vignette>(true); cVignette.intensity.value = .3f; cVignette.smoothness.value = .55f; cVignette.color.value = new Color(.05f, .03f, .08f);

            dayVolume = NewVolume("Daytime grade", dayProfile, 40, 1);
            nightVolume = NewVolume("Night grade", nightProfile, 41, 0);
            cinemaVolume = NewVolume("Title cinematic", cinemaProfile, 42, 0);
            OnTierChanged(HotelPerformanceDirector.Instance);
        }

        static VolumeProfile NewProfile(string name)
        {
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = name; profile.hideFlags = HideFlags.DontSave;
            return profile;
        }
        Volume NewVolume(string name, VolumeProfile profile, float priority, float weight)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(transform, false);
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true; volume.priority = priority; volume.weight = weight; volume.sharedProfile = profile;
            return volume;
        }

        void OnTierChanged(HotelPerformanceDirector performance)
        {
            bool high = !performance || performance.CurrentTier >= HotelPerformanceDirector.Tier.High;
            bool low = performance && performance.CurrentTier == HotelPerformanceDirector.Tier.Low;
            foreach (var bloom in new[] { dayBloom, nightBloom })
            {
                if (bloom == null) continue;
                bloom.highQualityFiltering.Override(high);
                bloom.downscale.Override(high ? BloomDownscaleMode.Half : BloomDownscaleMode.Quarter);
                bloom.maxIterations.Override(low ? 4 : 6);
            }
            if (depth != null) depth.highQualitySampling.Override(high);
            moteDensity = low ? 0 : high ? 1 : .6f;
        }

        void LateUpdate()
        {
            if (!hotel && Time.unscaledTime >= searchAt)
            {
                searchAt = Time.unscaledTime + 1;
                hotel = FindAnyObjectByType<HotelGuestSimulation>();
                if (hotel && hotel.sun) sun = hotel.sun;
            }
            if (!sun) sun = RenderSettings.sun;

            float clock = hotel ? hotel.NightBlend : 0;
            float target = nightOverride >= 0 ? nightOverride : clock;
            // Title-shot overrides ease in quickly; the gameplay clock is slower than this rate.
            NightBlend = Mathf.MoveTowards(NightBlend, target, Mathf.Max(Time.unscaledDeltaTime, .001f) * 4f);
            float n = NightBlend;

            nightVolume.weight = n;
            cinemaVolume.weight = cinematicWeight;
            if (cinematicWeight > 0) depth.focusDistance.value = Mathf.Max(.5f, focusDistance);

            if (!capturedAmbient)
            {
                previousSky = RenderSettings.ambientSkyColor; previousEquator = RenderSettings.ambientEquatorColor; previousGround = RenderSettings.ambientGroundColor;
                capturedAmbient = true;
            }
            if (RenderSettings.ambientMode == AmbientMode.Trilight)
            {
                RenderSettings.ambientSkyColor = Color.Lerp(daySky, nightSky, n);
                RenderSettings.ambientEquatorColor = Color.Lerp(dayEquator, nightEquator, n);
                RenderSettings.ambientGroundColor = Color.Lerp(dayGround, nightGround, n);
            }
            if (sun)
            {
                if (previousShadowStrength < 0) previousShadowStrength = sun.shadowStrength;
                sun.color = Color.Lerp(daySun, nightSun, n);
                sun.shadowStrength = Mathf.Lerp(dayShadowStrength, nightShadowStrength, n);
                // The hotel clock owns sun intensity while running; title previews borrow it.
                if (nightOverride >= 0)
                {
                    if (baseSunIntensity < 0) baseSunIntensity = sun.intensity;
                    sun.intensity = Mathf.Lerp(baseSunIntensity, .2f, n);
                }
                else if (baseSunIntensity >= 0) { sun.intensity = baseSunIntensity; baseSunIntensity = -1; }
            }
            UpdateMotes(n);
        }

        // Soft floating dust that catches the light: sunny motes by day, cool specks at night.
        void BuildMotes()
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (!shader) return;
            moteTexture = new Texture2D(32, 32, TextureFormat.RGBA32, false) { name = "Hotel dust mote", hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[32 * 32];
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
            {
                float r = new Vector2((x - 15.5f) / 15.5f, (y - 15.5f) / 15.5f).magnitude;
                pixels[y * 32 + x] = new Color(1, 1, 1, Mathf.Pow(Mathf.Clamp01(1 - r), 2.2f));
            }
            moteTexture.SetPixels(pixels); moteTexture.Apply(false, true);
            moteMaterial = new Material(shader) { name = "Hotel dust motes", hideFlags = HideFlags.DontSave };
            moteMaterial.SetTexture("_BaseMap", moteTexture); moteMaterial.mainTexture = moteTexture;
            moteMaterial.SetFloat("_Surface", 1); moteMaterial.SetFloat("_ZWrite", 0); moteMaterial.SetFloat("_SrcBlend", 5); moteMaterial.SetFloat("_DstBlend", 10);
            moteMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); moteMaterial.renderQueue = 3000;

            var go = new GameObject("Floating dust motes") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(transform, false);
            motes = go.AddComponent<ParticleSystem>();
            motes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = motes.main;
            main.duration = 10; main.loop = true; main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(7, 12);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.01f, .05f);
            main.startSize = new ParticleSystem.MinMaxCurve(.012f, .032f);
            main.startColor = new Color(1.25f, 1.15f, .95f, .42f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 160; main.gravityModifier = -.002f;
            main.useUnscaledTime = true;
            var emission = motes.emission; emission.rateOverTime = 14;
            var shape = motes.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(9, 3.2f, 9);
            var fade = motes.colorOverLifetime; fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .25f), new GradientAlphaKey(1, .7f), new GradientAlphaKey(0, 1) });
            fade.color = gradient;
            var noise = motes.noise; noise.enabled = true; noise.strength = .07f; noise.frequency = .35f; noise.scrollSpeed = .08f; noise.quality = ParticleSystemNoiseQuality.Low;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = moteMaterial; renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.renderMode = ParticleSystemRenderMode.Billboard; renderer.sortingFudge = 10;
            motes.Play();
        }

        void UpdateMotes(float night)
        {
            if (!motes) return;
            var camera = Camera.main;
            if (camera) motes.transform.position = camera.transform.position + camera.transform.forward * 2.5f;
            var emission = motes.emission;
            emission.rateOverTime = 14 * moteDensity * Mathf.Lerp(1, .45f, night);
            var main = motes.main;
            main.startColor = Color.Lerp(new Color(1.25f, 1.15f, .95f, .42f), new Color(.75f, .85f, 1.3f, .32f), night);
        }

        void OnDestroy()
        {
            HotelPerformanceDirector.TierChanged -= OnTierChanged;
            if (Instance == this) Instance = null;
            if (capturedAmbient) { RenderSettings.ambientSkyColor = previousSky; RenderSettings.ambientEquatorColor = previousEquator; RenderSettings.ambientGroundColor = previousGround; }
            if (sun && previousShadowStrength >= 0) sun.shadowStrength = previousShadowStrength;
            if (sun && baseSunIntensity >= 0) sun.intensity = baseSunIntensity;
            foreach (var profile in new[] { dayProfile, nightProfile, cinemaProfile }) if (profile) Destroy(profile);
            if (moteMaterial) Destroy(moteMaterial);
            if (moteTexture) Destroy(moteTexture);
        }
    }
}
