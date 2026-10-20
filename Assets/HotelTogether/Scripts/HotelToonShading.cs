using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

namespace HotelTogether
{
    // Turns the hotel into a cartoon at runtime: every opaque URP Lit / glTF material is swapped for a
    // "Hotel Together/Toon" copy (cel shading, banded lamp light, toon highlights, reflections, outlines).
    // Source materials and assets are never edited, so turning TOON off in Settings restores the original look.
    [DefaultExecutionOrder(-150), DisallowMultipleComponent]
    public sealed class HotelToonShading : MonoBehaviour
    {
        public enum Kind { Character, Prop, Architecture }

        const string Pref = "HotelTogether.Settings.ToonShading";
        const string ShaderName = "Hotel Together/Toon";
        const string OutlinePass = "SRPDefaultUnlit";

        public static HotelToonShading Instance { get; private set; }
        public static bool Enabled => PlayerPrefs.GetInt(Pref, 1) != 0;
        /// <summary>Raised after materials were converted or restored (reflection probes re-render).</summary>
        public static event Action Changed;

        [Header("Characters (guests, staff, zombies, hands)")]
        [Range(0, 4)] public float characterOutline = 1.05f;
        public Color characterOutlineColor = new Color(.30f, .22f, .26f);
        [Range(0, 2)] public float characterRim = .32f;
        public Color skinShadowTint = new Color(1f, .86f, .84f);

        [Header("Props (furniture and items smaller than Prop Max Size)")]
        [Range(0, 4)] public float propOutline = .62f;
        public Color propOutlineColor = new Color(.40f, .32f, .32f);
        [Min(.1f)] public float propMaxSize = 2.6f;

        [Header("Look")]
        public Color shadowTint = new Color(.86f, .84f, .97f);
        [Range(0, 6)] public float lampBands = 0; // 0 = smooth lamp falloff, no visible rings
        [Range(0, 1)] public float lampSoftness = .75f;
        [Range(-1, 1)] public float celMidPoint = -.05f;
        [Range(0, 1)] public float celSoftness = .06f;

        [Header("Work per frame")]
        [Min(50)] public int renderersPerFrame = 500;
        [Min(.5f)] public float rescanSeconds = 2;

        public int ConvertedMaterials => toonToSource.Count;
        public int ConvertedRenderers => converted.Count;

        Shader toonShader;
        Material opaqueTemplate, cutoutTemplate;
        bool active, outlinesForProps = true, outlinesForCharacters = true;
        readonly Dictionary<(Material, Kind), Material> cache = new Dictionary<(Material, Kind), Material>();
        readonly Dictionary<Material, Material> toonToSource = new Dictionary<Material, Material>();
        readonly Dictionary<Material, Kind> toonKind = new Dictionary<Material, Kind>();
        sealed class LiveLink { public Material source, toon; public Color color, emission; public Texture texture; }
        readonly List<LiveLink> everyFrame = new List<LiveLink>(), roundRobin = new List<LiveLink>();
        int robin;
        readonly HashSet<Renderer> converted = new HashSet<Renderer>();
        readonly HashSet<string> skippedShaders = new HashSet<string>();
        readonly Dictionary<Renderer, Kind> kinds = new Dictionary<Renderer, Kind>();
        readonly HashSet<Mesh> smoothed = new HashSet<Mesh>();
        readonly Queue<Mesh> smoothQueue = new Queue<Mesh>();
        readonly List<Material> scratch = new List<Material>();
        Renderer[] snapshot = Array.Empty<Renderer>();
        int cursor;
        float nextScan;
        bool firstPassLogged, notifyPending;

        static readonly string[] LitShaders =
        {
            "Universal Render Pipeline/Lit", "Universal Render Pipeline/Simple Lit", "Universal Render Pipeline/Complex Lit",
        };

        // ------------------------------------------------------------------ boot
        // Play mode runs without a domain reload in this project: clear statics left over from the last session.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; Changed = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            SceneManager.sceneLoaded -= Loaded; SceneManager.sceneLoaded += Loaded;
            Ensure(SceneManager.GetActiveScene());
        }
        static void Loaded(Scene scene, LoadSceneMode mode) { Ensure(scene); }
        public static HotelToonShading Ensure(Scene scene)
        {
            if (!Application.isPlaying || !scene.IsValid() || scene.name != "HotelLobby") return null;
            if (Instance) return Instance;
            var root = new GameObject("Hotel toon shading");
            SceneManager.MoveGameObjectToScene(root, scene);
            return root.AddComponent<HotelToonShading>();
        }

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(this); return; }
            Instance = this;
            opaqueTemplate = Resources.Load<Material>("HotelTogetherGraphics/Toon/HotelToonOpaque");
            cutoutTemplate = Resources.Load<Material>("HotelTogetherGraphics/Toon/HotelToonCutout");
            toonShader = opaqueTemplate ? opaqueTemplate.shader : Shader.Find(ShaderName);
            if (!toonShader || !toonShader.isSupported)
            {
                Debug.LogWarning("Hotel toon shading: the '" + ShaderName + "' shader is missing or unsupported on this GPU, keeping the original materials.");
                HotelGraphicsLog.Write("toon: shader missing/unsupported (template " + (opaqueTemplate ? "ok" : "missing") + ")");
                enabled = false; return;
            }
            HotelPerformanceDirector.TierChanged += OnTierChanged;
            OnTierChanged(HotelPerformanceDirector.Instance);
            active = Enabled;
            HotelGraphicsLog.Write("toon: awake, enabled=" + active + ", templates=" + (opaqueTemplate != null) + "/" + (cutoutTemplate != null));
        }

        void OnDestroy()
        {
            HotelPerformanceDirector.TierChanged -= OnTierChanged;
            if (Instance == this) Instance = null;
            RestoreAll();
            foreach (var toon in toonToSource.Keys) if (toon) Destroy(toon);
            toonToSource.Clear(); cache.Clear(); toonKind.Clear(); everyFrame.Clear(); roundRobin.Clear();
        }

        // ------------------------------------------------------------------ public API
        public void SetEnabled(bool value)
        {
            PlayerPrefs.SetInt(Pref, value ? 1 : 0); PlayerPrefs.Save();
            HotelGraphicsLog.Write("toon: set enabled " + value);
            if (active == value) return;
            active = value;
            if (!active) RestoreAll();
            else { snapshot = Array.Empty<Renderer>(); cursor = 0; nextScan = 0; }
            notifyPending = true;
        }

        /// <summary>Convert a freshly built object (new character skin, look change) right away instead of on the next sweep.</summary>
        public static void Apply(GameObject root)
        {
            var self = Instance;
            if (!self || !self.active || !root) return;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true)) self.Process(renderer, true);
        }

        void OnTierChanged(HotelPerformanceDirector performance)
        {
            var tier = performance ? performance.CurrentTier : HotelPerformanceDirector.Tier.High;
            outlinesForCharacters = true;
            outlinesForProps = tier >= HotelPerformanceDirector.Tier.Medium;
            foreach (var entry in toonKind) if (entry.Key) ConfigureOutline(entry.Key, entry.Value, toonToSource.TryGetValue(entry.Key, out var s) ? s : null);
        }

        // ------------------------------------------------------------------ sweep
        void Update()
        {
            // F9 flips between toon and the original materials (quick before/after check while playing).
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f9Key.wasPressedThisFrame) SetEnabled(!active);
#if UNITY_EDITOR
            // F7 (editor only): save before/after screenshots to Logs/Captures for tuning.
            if (keyboard != null && keyboard.f7Key.wasPressedThisFrame && !capturing) StartCoroutine(CaptureComparison());
#endif
            if (!active) return;
            float now = Time.unscaledTime;
            if (cursor >= snapshot.Length && now >= nextScan)
            {
                // The first sweep includes inactive objects so pooled guests and hidden props are ready before they appear.
                snapshot = FindObjectsByType<Renderer>(firstPassLogged ? FindObjectsInactive.Exclude : FindObjectsInactive.Include);
                cursor = 0;
                nextScan = now + rescanSeconds;
            }
            int end = Mathf.Min(snapshot.Length, cursor + renderersPerFrame);
            for (; cursor < end; cursor++) Process(snapshot[cursor], false);
            if (cursor >= snapshot.Length && snapshot.Length > 0 && !firstPassLogged)
            {
                firstPassLogged = true; notifyPending = true;
                Debug.Log("Hotel toon shading: " + converted.Count + " renderers now use " + toonToSource.Count + " toon materials.");
                int characters = 0, props = 0, architecture = 0;
                foreach (var k in toonKind.Values) { if (k == Kind.Character) characters++; else if (k == Kind.Prop) props++; else architecture++; }
                HotelGraphicsLog.Write("toon: " + converted.Count + " renderers, " + toonToSource.Count + " materials (characters " + characters + ", props " + props + ", architecture " + architecture + ")");
                HotelGraphicsLog.Write("toon: shaders left unchanged: " + string.Join(", ", skippedShaders));
            }
            // Smoothed outline normals are built a few meshes per frame.
            for (int i = 0; i < 6 && smoothQueue.Count > 0; i++) BuildSmoothNormals(smoothQueue.Dequeue());
            SyncLiveMaterials();
            if (notifyPending && cursor >= snapshot.Length) { notifyPending = false; Changed?.Invoke(); }
        }

#if UNITY_EDITOR
        bool capturing;
        int captureIndex;
        System.Collections.IEnumerator CaptureComparison()
        {
            capturing = true;
            string folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "Logs", "Captures"));
            System.IO.Directory.CreateDirectory(folder);
            captureIndex++;
            bool was = active;
            float scale = Time.timeScale; Time.timeScale = 0;
            for (int pass = 0; pass < 2; pass++)
            {
                bool toon = pass == 0;
                if (active != toon) { active = toon; if (!toon) RestoreAll(); else { snapshot = FindObjectsByType<Renderer>(FindObjectsInactive.Exclude); cursor = 0; while (cursor < snapshot.Length) Process(snapshot[cursor++], true); } }
                yield return null; yield return null; yield return new WaitForEndOfFrame();
                var shot = ScreenCapture.CaptureScreenshotAsTexture();
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder, "capture" + captureIndex + (toon ? "_toon.png" : "_original.png")), shot.EncodeToPNG());
                Destroy(shot);
            }
            if (active != was) { active = was; if (!was) RestoreAll(); else { snapshot = Array.Empty<Renderer>(); cursor = 0; nextScan = 0; } }
            Time.timeScale = scale;
            HotelGraphicsLog.Write("capture " + captureIndex + " saved");
            capturing = false;
        }
#endif

        void Process(Renderer renderer, bool immediate)
        {
            if (!renderer || !(renderer is MeshRenderer || renderer is SkinnedMeshRenderer)) return;
            renderer.GetSharedMaterials(scratch);
            bool changed = false;
            Kind kind = Kind.Prop;
            bool kindKnown = false;
            for (int i = 0; i < scratch.Count; i++)
            {
                var source = scratch[i];
                if (!source || toonToSource.ContainsKey(source) || !Convertible(source)) continue;
                if (!kindKnown) { kind = Classify(renderer); kindKnown = true; }
                var toon = GetToon(source, kind);
                if (!toon) continue;
                scratch[i] = toon; changed = true;
            }
            if (!changed) return;
            renderer.SetSharedMaterials(scratch);
            converted.Add(renderer);
            if (kind == Kind.Prop && outlinesForProps && renderer is MeshRenderer && renderer.TryGetComponent<MeshFilter>(out var filter))
                QueueSmoothNormals(filter.sharedMesh, renderer, immediate);
        }

        void RestoreAll()
        {
            foreach (var renderer in converted)
            {
                if (!renderer) continue;
                renderer.GetSharedMaterials(scratch);
                bool changed = false;
                for (int i = 0; i < scratch.Count; i++)
                    if (scratch[i] && toonToSource.TryGetValue(scratch[i], out var source) && source) { scratch[i] = source; changed = true; }
                if (changed) renderer.SetSharedMaterials(scratch);
            }
            converted.Clear();
        }

        // ------------------------------------------------------------------ classification
        Kind Classify(Renderer renderer)
        {
            if (kinds.TryGetValue(renderer, out var known)) return known;
            Kind kind;
            if (renderer is SkinnedMeshRenderer || renderer.GetComponentInParent<HotelGuestVisual>(true) || renderer.GetComponentInParent<HotelEmployeeRig>(true) ||
                renderer.GetComponentInParent<HotelPlayer>(true) || renderer.GetComponentInParent<HotelZombieBot>(true))
                kind = Kind.Character;
            else
            {
                var size = renderer.bounds.size;
                kind = Mathf.Max(size.x, Mathf.Max(size.y, size.z)) <= propMaxSize ? Kind.Prop : Kind.Architecture;
            }
            kinds[renderer] = kind;
            return kind;
        }

        static bool Convertible(Material material)
        {
            var shader = material.shader;
            if (!shader) return false;
            string name = shader.name;
            bool lit = Array.IndexOf(LitShaders, name) >= 0 || name.IndexOf("glTF-pbr", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       name.IndexOf("glTF/Pbr", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("glTF-specular", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!lit) { if (Instance) Instance.skippedShaders.Add(name); return false; }
            // Glass, water, fades and other see-through surfaces keep their original shader.
            if (material.renderQueue > (int)RenderQueue.GeometryLast) return false;
            if (material.HasProperty("_Surface") && material.GetFloat("_Surface") > .5f) return false;
            if (material.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT")) return false;
            return true;
        }

        // ------------------------------------------------------------------ material conversion
        Material GetToon(Material source, Kind kind)
        {
            if (cache.TryGetValue((source, kind), out var existing) && existing) return existing;
            bool clip = source.IsKeywordEnabled("_ALPHATEST_ON") || Float(source, 0, "_AlphaClip") > .5f;
            var template = clip ? cutoutTemplate : opaqueTemplate;
            var toon = template ? new Material(template) : new Material(toonShader);
            toon.name = source.name;
            toon.hideFlags = HideFlags.DontSave;
            if (!template && clip) toon.EnableKeyword("_ALPHATEST_ON");
            toon.SetFloat("_AlphaClip", clip ? 1 : 0);
            toon.SetFloat("_Cutoff", Float(source, .5f, "_Cutoff", "alphaCutoff"));

            CopyLook(source, toon);
            Stylise(source, toon, kind);
            ConfigureOutline(toon, kind, source);

            cache[(source, kind)] = toon;
            toonToSource[toon] = source;
            toonKind[toon] = kind;
            // Scripts may animate the original material (sign bulbs, status lights): keep the copy in sync.
            // Script-made materials (DontSave) are checked every frame, the rest a few per frame.
            var link = new LiveLink { source = source, toon = toon, color = toon.GetColor("_BaseColor"), emission = toon.GetColor("_EmissionColor"), texture = toon.GetTexture("_BaseMap") };
            if ((source.hideFlags & HideFlags.DontSave) != 0) everyFrame.Add(link); else roundRobin.Add(link);
            return toon;
        }

        static void CopyLook(Material source, Material toon)
        {
            string baseName = First(source, "_BaseMap", "baseColorTexture", "_MainTex");
            if (baseName != null)
            {
                toon.SetTexture("_BaseMap", source.GetTexture(baseName));
                toon.SetTextureScale("_BaseMap", source.GetTextureScale(baseName));
                toon.SetTextureOffset("_BaseMap", source.GetTextureOffset(baseName));
            }
            toon.SetColor("_BaseColor", Colour(source, Color.white, "_BaseColor", "baseColorFactor", "_Color"));

            float smoothness = source.HasProperty("_Smoothness") ? source.GetFloat("_Smoothness")
                             : source.HasProperty("roughnessFactor") ? 1 - source.GetFloat("roughnessFactor")
                             : Float(source, .3f, "_Glossiness");
            toon.SetFloat("_Smoothness", Mathf.Clamp01(smoothness));
            toon.SetFloat("_Metallic", Mathf.Clamp01(Float(source, 0, "_Metallic", "metallicFactor")));

            // Emission only when the source actually emits.
            Color emission = Color.black;
            if (source.IsKeywordEnabled("_EMISSION") && source.HasProperty("_EmissionColor")) emission = source.GetColor("_EmissionColor");
            else if (source.HasProperty("emissiveFactor")) emission = source.GetColor("emissiveFactor");
            toon.SetColor("_EmissionColor", emission);
            string emissionMap = First(source, "_EmissionMap", "emissiveTexture");
            if (emissionMap != null && source.GetTexture(emissionMap)) toon.SetTexture("_EmissionMap", source.GetTexture(emissionMap));

            string occlusionMap = First(source, "_OcclusionMap", "occlusionTexture");
            if (occlusionMap != null && source.GetTexture(occlusionMap))
            {
                toon.SetTexture("_OcclusionMap", source.GetTexture(occlusionMap));
                toon.SetFloat("_OcclusionStrength", Float(source, 1, "_OcclusionStrength", "occlusionTexture_strength"));
            }
            toon.SetFloat("_Cull", Float(source, 2, "_Cull", "_CullMode"));
        }

        void Stylise(Material source, Material toon, Kind kind)
        {
            string name = source.name.ToLowerInvariant();
            float metallic = toon.GetFloat("_Metallic"), smoothness = toon.GetFloat("_Smoothness");
            toon.SetColor("_ShadowColor", shadowTint);
            toon.SetFloat("_LightBands", lampBands);
            toon.SetFloat("_AdditionalLightSoftness", lampSoftness);
            toon.SetFloat("_CelShadeMidPoint", celMidPoint);
            toon.SetFloat("_CelShadeSoftness", celSoftness);
            // Polished metal and ceramics get a crisp glint and stronger reflections; matte fabric gets none.
            toon.SetFloat("_SpecularStrength", Mathf.Lerp(.35f, 1.1f, Mathf.Clamp01(metallic + smoothness * .6f)));
            toon.SetFloat("_ReflectionStrength", metallic > .5f ? 1.25f : 1);
            toon.SetFloat("_RimStrength", .14f);

            if (kind == Kind.Character)
            {
                bool face = Contains(name, "pupil", "eye", "mouth", "lip", "teeth", "tongue", "brow", "lash", "iris");
                bool skin = Contains(name, "skin", "face", "head", "hand", "body", "arm", "leg", "neck");
                toon.SetFloat("_IsFace", face ? 1 : 0);
                toon.SetFloat("_RimStrength", characterRim);
                toon.SetFloat("_CelShadeSoftness", .045f);
                toon.SetFloat("_CelShadeMidPoint", -.1f);
                toon.SetFloat("_IndirectLightFlatten", .7f);
                if (skin) { toon.SetColor("_ShadowColor", skinShadowTint); toon.SetFloat("_SpecularStrength", .18f); toon.SetFloat("_Smoothness", Mathf.Min(smoothness, .35f)); }
            }
            else if (kind == Kind.Architecture)
            {
                // Walls and floors: a little more form from the sky so rooms do not look flat-lit.
                toon.SetFloat("_IndirectLightFlatten", .35f);
                toon.SetFloat("_RimStrength", 0);
            }
        }

        void ConfigureOutline(Material toon, Kind kind, Material source)
        {
            bool face = toon.GetFloat("_IsFace") > .5f;
            bool doubleSided = toon.GetFloat("_Cull") < .5f;
            bool glowing = toon.GetColor("_EmissionColor").maxColorComponent > .5f;
            bool on;
            switch (kind)
            {
                case Kind.Character:
                    on = outlinesForCharacters && !face;
                    toon.SetFloat("_OutlineWidth", characterOutline);
                    toon.SetColor("_OutlineColor", characterOutlineColor);
                    toon.SetFloat("_OutlineSmoothNormals", 0);
                    break;
                case Kind.Prop:
                    on = outlinesForProps && !doubleSided && !glowing;
                    toon.SetFloat("_OutlineWidth", propOutline);
                    toon.SetColor("_OutlineColor", propOutlineColor);
                    toon.SetFloat("_OutlineSmoothNormals", 1);
                    break;
                default:
                    on = false;
                    toon.SetFloat("_OutlineWidth", 0);
                    break;
            }
            toon.SetShaderPassEnabled(OutlinePass, on);
        }

        void SyncLiveMaterials()
        {
            for (int i = everyFrame.Count - 1; i >= 0; i--) if (!Sync(everyFrame[i])) everyFrame.RemoveAt(i);
            int count = Mathf.Min(40, roundRobin.Count);
            for (int n = 0; n < count; n++)
            {
                if (robin >= roundRobin.Count) robin = 0;
                if (!Sync(roundRobin[robin])) roundRobin.RemoveAt(robin); else robin++;
            }
        }

        // Copies colour, emission and texture changes from the original material; only writes when something changed.
        static bool Sync(LiveLink link)
        {
            var source = link.source; var toon = link.toon;
            if (!source || !toon) return false;
            Color color = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : source.HasProperty("baseColorFactor") ? source.GetColor("baseColorFactor") : link.color;
            if (color != link.color) { link.color = color; toon.SetColor("_BaseColor", color); }
            if (source.HasProperty("_EmissionColor"))
            {
                Color emission = source.IsKeywordEnabled("_EMISSION") ? source.GetColor("_EmissionColor") : Color.black;
                if (emission != link.emission) { link.emission = emission; toon.SetColor("_EmissionColor", emission); }
            }
            if (source.HasProperty("_BaseMap"))
            {
                var texture = source.GetTexture("_BaseMap");
                if (texture != link.texture) { link.texture = texture; toon.SetTexture("_BaseMap", texture); }
            }
            return true;
        }

        // ------------------------------------------------------------------ smoothed outline normals
        void QueueSmoothNormals(Mesh mesh, Renderer renderer, bool immediate)
        {
            if (!mesh || smoothed.Contains(mesh)) return;
            smoothed.Add(mesh);
            if (!mesh.isReadable || mesh.vertexCount > 60000 || renderer.isPartOfStaticBatch || mesh.name.StartsWith("Combined Mesh", StringComparison.Ordinal)) return;
            if (immediate) BuildSmoothNormals(mesh); else smoothQueue.Enqueue(mesh);
        }

        static readonly List<Vector3> vertexBuffer = new List<Vector3>(), normalBuffer = new List<Vector3>(), smoothBuffer = new List<Vector3>();
        // Hard-edged meshes duplicate vertices along every crease. Averaging normals across duplicates
        // gives the outline a single direction per corner, so the inverted hull stays closed.
        static void BuildSmoothNormals(Mesh mesh)
        {
            if (!mesh) return;
            mesh.GetVertices(vertexBuffer); mesh.GetNormals(normalBuffer);
            if (normalBuffer.Count != vertexBuffer.Count || vertexBuffer.Count == 0) return;
            var sums = new Dictionary<Vector3Int, Vector3>(vertexBuffer.Count);
            for (int i = 0; i < vertexBuffer.Count; i++)
            {
                var key = Quantise(vertexBuffer[i]);
                sums.TryGetValue(key, out var sum);
                sums[key] = sum + normalBuffer[i];
            }
            smoothBuffer.Clear();
            for (int i = 0; i < vertexBuffer.Count; i++)
            {
                var n = sums[Quantise(vertexBuffer[i])];
                smoothBuffer.Add(n.sqrMagnitude > 1e-6f ? n.normalized : normalBuffer[i]);
            }
            mesh.SetUVs(7, smoothBuffer);
        }
        static Vector3Int Quantise(Vector3 p) => new Vector3Int(Mathf.RoundToInt(p.x * 2000), Mathf.RoundToInt(p.y * 2000), Mathf.RoundToInt(p.z * 2000));

        // ------------------------------------------------------------------ helpers
        static string First(Material m, params string[] names) { foreach (var n in names) if (m.HasProperty(n)) return n; return null; }
        static float Float(Material m, float fallback, params string[] names) { foreach (var n in names) if (m.HasProperty(n)) return m.GetFloat(n); return fallback; }
        static Color Colour(Material m, Color fallback, params string[] names) { foreach (var n in names) if (m.HasProperty(n)) return m.GetColor(n); return fallback; }
        static bool Contains(string text, params string[] words) { foreach (var w in words) if (text.Contains(w)) return true; return false; }
    }
}
