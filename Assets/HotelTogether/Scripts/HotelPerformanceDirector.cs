using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace HotelTogether
{
    // Owns graphics quality tiers and frame-rate protection for HotelLobby.
    // Everything is applied at runtime and restored on exit, so pipeline assets
    // are never permanently rewritten by Play mode.
    [DefaultExecutionOrder(-200), DisallowMultipleComponent]
    public sealed class HotelPerformanceDirector : MonoBehaviour
    {
        public enum Tier { Low, Medium, High, Ultra }

        [Serializable]
        public struct TierSettings
        {
            public string label;
            [Range(.5f, 1)] public float renderScale, minimumScale;
            public int msaa;
            public AntialiasingMode antialiasing;
            public AntialiasingQuality antialiasingQuality;
            public float shadowDistance;
            public int cascades, shadowResolution, shadowedLights;
            public bool ambientOcclusion, highQualityBloom;
            public bool floorReflections;
            [Range(.2f, 1)] public float reflectionScale;
            public int reflectionMaxSize, reflectionInterval;
            public float smallShadowSize, lodBias;
        }

        const string Prefix = "HotelTogether.Settings.";
        public static HotelPerformanceDirector Instance { get; private set; }
        public static event Action<HotelPerformanceDirector> TierChanged;

        public float targetFrameRate = 60;
        public TierSettings[] tiers =
        {
            new TierSettings{label="LOW",renderScale=.8f,minimumScale=.6f,msaa=1,antialiasing=AntialiasingMode.FastApproximateAntialiasing,antialiasingQuality=AntialiasingQuality.Low,shadowDistance=22,cascades=2,shadowResolution=1024,shadowedLights=0,ambientOcclusion=false,highQualityBloom=false,floorReflections=false,reflectionScale=.3f,reflectionMaxSize=384,reflectionInterval=3,smallShadowSize=.6f,lodBias=1},
            new TierSettings{label="MEDIUM",renderScale=.9f,minimumScale=.7f,msaa=1,antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing,antialiasingQuality=AntialiasingQuality.Medium,shadowDistance=32,cascades=2,shadowResolution=2048,shadowedLights=1,ambientOcclusion=true,highQualityBloom=false,floorReflections=true,reflectionScale=.35f,reflectionMaxSize=512,reflectionInterval=2,smallShadowSize=.35f,lodBias=1.5f},
            new TierSettings{label="HIGH",renderScale=1,minimumScale=.8f,msaa=2,antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing,antialiasingQuality=AntialiasingQuality.High,shadowDistance=45,cascades=4,shadowResolution=2048,shadowedLights=3,ambientOcclusion=true,highQualityBloom=true,floorReflections=true,reflectionScale=.5f,reflectionMaxSize=768,reflectionInterval=1,smallShadowSize=.18f,lodBias=2},
            new TierSettings{label="ULTRA",renderScale=1,minimumScale=.9f,msaa=4,antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing,antialiasingQuality=AntialiasingQuality.High,shadowDistance=60,cascades=4,shadowResolution=4096,shadowedLights=6,ambientOcclusion=true,highQualityBloom=true,floorReflections=true,reflectionScale=.65f,reflectionMaxSize=1024,reflectionInterval=1,smallShadowSize=0,lodBias=2.5f}
        };

        public Tier CurrentTier { get; private set; } = Tier.High;
        public TierSettings Settings => tiers[(int)CurrentTier];
        public bool AutoResolution { get; private set; } = true;
        public bool ShowFrameRate { get; private set; }
        public float CurrentScale { get; private set; } = 1;
        public float FramesPerSecond { get; private set; } = 60;
        public float FrameMilliseconds { get; private set; } = 16.7f;

        UniversalRenderPipelineAsset pipeline;
        PipelineSnapshot snapshot;
        readonly List<ScriptableRendererFeature> occlusionFeatures = new List<ScriptableRendererFeature>();
        readonly Dictionary<ScriptableRendererFeature, bool> featureStates = new Dictionary<ScriptableRendererFeature, bool>();
        readonly Dictionary<Renderer, ShadowCastingMode> prunedShadows = new Dictionary<Renderer, ShadowCastingMode>();
        readonly List<ManagedLight> managedLights = new List<ManagedLight>();
        readonly FrameTiming[] timings = new FrameTiming[1];
        float sampleTime, sampleSum, lightTick, lightScan;
        int sampleCount, fastWindows;
        float originalLodBias;
        AnisotropicFiltering originalAniso;
        bool applied;

        struct ManagedLight { public Light light; public LightShadows shadows; }
        struct PipelineSnapshot
        {
            public float renderScale, shadowDistance, fsrSharpness;
            public int msaa, cascades, shadowResolution;
            public UpscalingFilterSelection upscaling;
            public bool fsrOverride;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            SceneManager.sceneLoaded -= Loaded; SceneManager.sceneLoaded += Loaded;
            Ensure(SceneManager.GetActiveScene());
        }
        static void Loaded(Scene scene, LoadSceneMode mode) { Ensure(scene); }
        public static HotelPerformanceDirector Ensure(Scene scene)
        {
            if (!Application.isPlaying || !scene.IsValid() || scene.name != "HotelLobby") return null;
            if (Instance) return Instance;
            var root = new GameObject("Hotel performance director");
            SceneManager.MoveGameObjectToScene(root, scene);
            return root.AddComponent<HotelPerformanceDirector>();
        }

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(this); return; }
            Instance = this;
            CurrentTier = (Tier)Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "GraphicsTier", (int)DetectTier()), 0, tiers.Length - 1);
            AutoResolution = PlayerPrefs.GetInt(Prefix + "AutoResolution", 1) != 0;
            ShowFrameRate = PlayerPrefs.GetInt(Prefix + "ShowFps", 0) != 0;
            originalLodBias = QualitySettings.lodBias;
            originalAniso = QualitySettings.anisotropicFiltering;
            // Smooth, tear-free presentation; the adaptive scaler protects 60 fps.
            Application.targetFrameRate = -1;
            if (QualitySettings.vSyncCount == 0) QualitySettings.vSyncCount = 1;
        }

        void Start() { Apply(); }

        // Pick a sensible first-launch tier from the GPU. Players can change it in Settings.
        public static Tier DetectTier()
        {
            int vram = SystemInfo.graphicsMemorySize;
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return Tier.Medium;
            if (vram >= 10000) return Tier.Ultra;
            if (vram >= 5000) return Tier.High;
            if (vram >= 2500) return Tier.Medium;
            return Tier.Low;
        }

        public void SetTier(Tier tier)
        {
            CurrentTier = (Tier)Mathf.Clamp((int)tier, 0, tiers.Length - 1);
            PlayerPrefs.SetInt(Prefix + "GraphicsTier", (int)CurrentTier); PlayerPrefs.Save();
            Apply();
        }
        public void CycleTier() { SetTier((Tier)(((int)CurrentTier + 1) % tiers.Length)); }
        public void SetAutoResolution(bool value)
        {
            AutoResolution = value; PlayerPrefs.SetInt(Prefix + "AutoResolution", value ? 1 : 0); PlayerPrefs.Save();
            if (!value) SetScale(Settings.renderScale, true);
        }
        public void SetShowFrameRate(bool value) { ShowFrameRate = value; PlayerPrefs.SetInt(Prefix + "ShowFps", value ? 1 : 0); PlayerPrefs.Save(); }

        public void Apply()
        {
            var s = Settings;
            pipeline = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
            if (!pipeline) pipeline = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline)
            {
                if (!applied)
                {
                    snapshot = new PipelineSnapshot
                    {
                        renderScale = pipeline.renderScale, shadowDistance = pipeline.shadowDistance, msaa = pipeline.msaaSampleCount,
                        cascades = pipeline.shadowCascadeCount, shadowResolution = pipeline.mainLightShadowmapResolution,
                        upscaling = pipeline.upscalingFilter, fsrOverride = pipeline.fsrOverrideSharpness, fsrSharpness = pipeline.fsrSharpness
                    };
                    foreach (var data in pipeline.rendererDataList)
                    {
                        var universal = data as UniversalRendererData;
                        if (!universal) continue;
                        foreach (var feature in universal.rendererFeatures)
                            if (feature && feature.GetType().Name == "ScreenSpaceAmbientOcclusion")
                            { occlusionFeatures.Add(feature); featureStates[feature] = feature.isActive; }
                    }
                }
                pipeline.msaaSampleCount = s.msaa;
                pipeline.shadowDistance = s.shadowDistance;
                // Never drop to one cascade: that switches shader keywords that builds may strip.
                pipeline.shadowCascadeCount = Mathf.Max(2, s.cascades);
                pipeline.mainLightShadowmapResolution = s.shadowResolution;
                // FSR keeps a sharp cartoon edge whenever the adaptive scaler drops below native.
                pipeline.upscalingFilter = UpscalingFilterSelection.FSR;
                pipeline.fsrOverrideSharpness = true; pipeline.fsrSharpness = .86f;
                foreach (var feature in occlusionFeatures) if (feature) feature.SetActive(s.ambientOcclusion && featureStates[feature]);
            }
            applied = true;
            SetScale(s.renderScale, true);
            QualitySettings.lodBias = s.lodBias;
            QualitySettings.anisotropicFiltering = CurrentTier >= Tier.High ? AnisotropicFiltering.ForceEnable : AnisotropicFiltering.Enable;
            ConfigureCameras();
            PruneSmallShadows(s.smallShadowSize);
            lightScan = 0; lightTick = 0;
            foreach (var quality in FindObjectsByType<HotelGraphicsQuality>(FindObjectsSortMode.None)) quality.ApplyTier(s);
            TierChanged?.Invoke(this);
        }

        void ConfigureCameras()
        {
            var s = Settings;
            foreach (var camera in Camera.allCameras)
            {
                if (camera.cameraType != CameraType.Game || camera.targetTexture) continue;
                camera.allowMSAA = s.msaa > 1; camera.allowHDR = true;
                var data = camera.GetUniversalAdditionalCameraData();
                if (!data) continue;
                data.antialiasing = s.antialiasing; data.antialiasingQuality = s.antialiasingQuality;
                data.renderPostProcessing = true;
            }
        }

        // Tiny props (cups, cutlery, labels) cost shadow draw calls in every cascade
        // but their shadows are barely visible. Characters keep their shadows.
        void PruneSmallShadows(float size)
        {
            foreach (var entry in prunedShadows) if (entry.Key) entry.Key.shadowCastingMode = entry.Value;
            prunedShadows.Clear();
            if (size <= 0) return;
            foreach (var renderer in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (renderer.shadowCastingMode != ShadowCastingMode.On || renderer.gameObject.scene != gameObject.scene) continue;
                if (renderer.GetComponentInParent<HotelGuestVisual>() || renderer.GetComponentInParent<HotelEmployeeRig>() || renderer.GetComponentInParent<HotelPlayer>()) continue;
                var extents = renderer.bounds.size;
                if (Mathf.Max(extents.x, Mathf.Max(extents.y, extents.z)) >= size) continue;
                prunedShadows[renderer] = renderer.shadowCastingMode;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        void Update()
        {
            MeasureFrame();
            if (Time.unscaledTime >= lightTick) { lightTick = Time.unscaledTime + .5f; BudgetShadowedLights(); }
        }

        void MeasureFrame()
        {
            float dt = Time.unscaledDeltaTime;
            if (dt <= 0 || dt > .5f) return;
            FramesPerSecond = Mathf.Lerp(FramesPerSecond, 1 / dt, .06f);
            float ms = dt * 1000;
            // GPU time ignores v-sync waits, so the scaler can tell spare headroom from a capped frame.
            if (FrameTimingManager.IsFeatureEnabled())
            {
                FrameTimingManager.CaptureFrameTimings();
                if (FrameTimingManager.GetLatestTimings(1, timings) > 0 && timings[0].gpuFrameTime > .1)
                    ms = (float)Math.Max(timings[0].gpuFrameTime, timings[0].cpuMainThreadFrameTime * .5);
            }
            FrameMilliseconds = Mathf.Lerp(FrameMilliseconds, ms, .1f);
            if (!AutoResolution || Time.timeScale <= 0) { sampleSum = 0; sampleCount = 0; return; }
            sampleSum += ms; sampleCount++;
            sampleTime += dt;
            if (sampleTime < .75f) return;
            float average = sampleSum / Mathf.Max(1, sampleCount);
            sampleTime = 0; sampleSum = 0; sampleCount = 0;
            float budget = 1000f / Mathf.Max(30, targetFrameRate);
            var s = Settings;
            if (average > budget * 1.06f) { fastWindows = 0; SetScale(CurrentScale - (average > budget * 1.35f ? .1f : .05f), false); }
            else if (average < budget * .78f) { if (++fastWindows >= 4) { fastWindows = 0; SetScale(CurrentScale + .05f, false); } }
            else fastWindows = 0;
        }

        void SetScale(float value, bool force)
        {
            var s = Settings;
            value = Mathf.Clamp(Mathf.Round(value * 20) / 20, AutoResolution ? s.minimumScale : s.renderScale, s.renderScale);
            if (!force && Mathf.Abs(value - CurrentScale) < .01f) return;
            CurrentScale = value;
            if (pipeline) pipeline.renderScale = value;
        }

        // Real-time point/spot shadows are the most expensive lights in a furnished
        // hotel. Only the lights nearest the player keep them.
        void BudgetShadowedLights()
        {
            if (Time.unscaledTime >= lightScan)
            {
                lightScan = Time.unscaledTime + 8;
                var known = new HashSet<Light>();
                foreach (var item in managedLights) if (item.light) known.Add(item.light);
                managedLights.RemoveAll(item => !item.light);
                foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (light.type != LightType.Directional && light.shadows != LightShadows.None && !known.Contains(light))
                        managedLights.Add(new ManagedLight { light = light, shadows = light.shadows });
            }
            var camera = Camera.main;
            if (!camera || managedLights.Count == 0) return;
            var origin = camera.transform.position;
            managedLights.Sort((a, b) => Score(a.light, origin).CompareTo(Score(b.light, origin)));
            int budget = Settings.shadowedLights;
            for (int i = 0; i < managedLights.Count; i++)
            {
                var item = managedLights[i];
                if (!item.light) continue;
                var wanted = i < budget && item.light.isActiveAndEnabled ? item.shadows : LightShadows.None;
                if (item.light.shadows != wanted) item.light.shadows = wanted;
            }
        }
        static float Score(Light light, Vector3 origin)
        {
            if (!light || !light.isActiveAndEnabled) return float.MaxValue;
            return (light.transform.position - origin).sqrMagnitude / Mathf.Max(.25f, light.range * light.range);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            foreach (var entry in prunedShadows) if (entry.Key) entry.Key.shadowCastingMode = entry.Value;
            foreach (var item in managedLights) if (item.light) item.light.shadows = item.shadows;
            foreach (var entry in featureStates) if (entry.Key) entry.Key.SetActive(entry.Value);
            if (pipeline && applied)
            {
                // Restore the pipeline asset so Play mode never leaves it modified.
                pipeline.renderScale = snapshot.renderScale; pipeline.shadowDistance = snapshot.shadowDistance;
                pipeline.msaaSampleCount = snapshot.msaa; pipeline.shadowCascadeCount = snapshot.cascades;
                pipeline.mainLightShadowmapResolution = snapshot.shadowResolution; pipeline.upscalingFilter = snapshot.upscaling;
                pipeline.fsrOverrideSharpness = snapshot.fsrOverride; pipeline.fsrSharpness = snapshot.fsrSharpness;
            }
            QualitySettings.lodBias = originalLodBias;
            QualitySettings.anisotropicFiltering = originalAniso;
        }
    }
}
