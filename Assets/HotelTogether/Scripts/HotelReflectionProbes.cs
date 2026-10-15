using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace HotelTogether
{
    // Room-shaped reflection probes, generated at runtime.
    // The hotel has no baked probes, so brass, chrome, tiles and polished wood only reflected the sky.
    // This finds the rooms by ray casting against the level colliders, places one box-projected
    // probe per room and re-renders them (time-sliced) when the toon materials change or day turns to night.
    [DefaultExecutionOrder(600), DisallowMultipleComponent]
    public sealed class HotelReflectionProbes : MonoBehaviour
    {
        public static HotelReflectionProbes Instance { get; private set; }

        [Tooltip("Probe budget for LOW, MEDIUM, HIGH and ULTRA.")]
        public int[] probesPerTier = { 0, 12, 26, 40 };
        public int[] resolutionPerTier = { 64, 64, 128, 256 };
        [Min(2)] public float gridSpacing = 6f;
        public float captureHeight = 1.55f;
        public float maxRoomSize = 44f;
        [Range(.05f, .5f)] public float rerenderNightStep = .25f;

        public int ProbeCount => probes.Count;

        readonly List<ReflectionProbe> probes = new List<ReflectionProbe>();
        readonly List<Bounds> rooms = new List<Bounds>();
        readonly List<Vector3> capturePoints = new List<Vector3>();
        Coroutine rendering;
        bool dirty;
        float renderedNight = -1;
        int budget, resolution;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            SceneManager.sceneLoaded -= Loaded; SceneManager.sceneLoaded += Loaded;
            Ensure(SceneManager.GetActiveScene());
        }
        static void Loaded(Scene scene, LoadSceneMode mode) { Ensure(scene); }
        public static HotelReflectionProbes Ensure(Scene scene)
        {
            if (!Application.isPlaying || !scene.IsValid() || scene.name != "HotelLobby") return null;
            if (Instance) return Instance;
            var root = new GameObject("Hotel reflection probes");
            SceneManager.MoveGameObjectToScene(root, scene);
            return root.AddComponent<HotelReflectionProbes>();
        }

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(this); return; }
            Instance = this;
            HotelPerformanceDirector.TierChanged += OnTierChanged;
            HotelToonShading.Changed += MarkDirty;
            ReadTier(HotelPerformanceDirector.Instance);
        }

        IEnumerator Start()
        {
            // Let the level, the toon conversion and the lighting director settle first.
            yield return new WaitForSecondsRealtime(1.5f);
            FindRooms();
            Rebuild();
        }

        void OnDestroy()
        {
            HotelPerformanceDirector.TierChanged -= OnTierChanged;
            HotelToonShading.Changed -= MarkDirty;
            if (Instance == this) Instance = null;
        }

        void MarkDirty() { dirty = true; }

        void ReadTier(HotelPerformanceDirector performance)
        {
            int tier = performance ? (int)performance.CurrentTier : (int)HotelPerformanceDirector.Tier.High;
            budget = probesPerTier[Mathf.Clamp(tier, 0, probesPerTier.Length - 1)];
            resolution = resolutionPerTier[Mathf.Clamp(tier, 0, resolutionPerTier.Length - 1)];
        }

        void OnTierChanged(HotelPerformanceDirector performance)
        {
            int oldBudget = budget, oldResolution = resolution;
            ReadTier(performance);
            if (rooms.Count > 0 && (oldBudget != budget || oldResolution != resolution)) Rebuild();
        }

        void Update()
        {
            if (probes.Count == 0) return;
            float night = HotelVisualDirector.Instance ? HotelVisualDirector.Instance.NightBlend : 0;
            if (renderedNight >= 0 && Mathf.Abs(night - renderedNight) >= rerenderNightStep) dirty = true;
            if (dirty && rendering == null) { dirty = false; rendering = StartCoroutine(RenderAll()); }
        }

        // ------------------------------------------------------------------ room discovery
        void FindRooms()
        {
            rooms.Clear(); capturePoints.Clear();
            var scene = gameObject.scene;
            bool any = false;
            var level = new Bounds();
            foreach (var collider in FindObjectsByType<Collider>(FindObjectsInactive.Exclude))
            {
                if (collider.isTrigger || collider.gameObject.scene != scene || collider.attachedRigidbody && !collider.attachedRigidbody.isKinematic) continue;
                var b = collider.bounds;
                if (b.size.x > 400 || b.size.z > 400) continue; // city backdrop / ground plane
                if (!any) { level = b; any = true; } else level.Encapsulate(b);
            }
            if (!any) return;

            const int mask = Physics.DefaultRaycastLayers;
            var trigger = QueryTriggerInteraction.Ignore;
            var candidates = new List<(Vector3 point, Bounds box)>();
            float top = level.max.y + 1;
            for (float x = level.min.x + gridSpacing * .5f; x < level.max.x; x += gridSpacing)
            for (float z = level.min.z + gridSpacing * .5f; z < level.max.z; z += gridSpacing)
            {
                var hits = Physics.RaycastAll(new Vector3(x, top, z), Vector3.down, level.size.y + 2, mask, trigger);
                foreach (var hit in hits)
                {
                    if (hit.normal.y < .7f) continue;
                    var point = hit.point + Vector3.up * captureHeight;
                    if (Physics.CheckSphere(point, .3f, mask, trigger)) continue;
                    if (!MeasureRoom(point, hit.point.y, mask, trigger, out var box)) continue;
                    candidates.Add((point, box));
                }
            }
            // Probes already placed in the scene (lobby, west wing) keep their spaces.
            var existing = new List<Bounds>();
            foreach (var probe in FindObjectsByType<ReflectionProbe>(FindObjectsInactive.Exclude))
                if (probe.transform.parent != transform) existing.Add(probe.bounds);
            // Biggest spaces first (lobby, café, corridors), then guest rooms; skip points already covered.
            candidates.Sort((a, b) => Volume(b.box).CompareTo(Volume(a.box)));
            foreach (var c in candidates)
            {
                bool covered = false;
                foreach (var room in existing) if (room.Contains(c.point)) { covered = true; break; }
                if (!covered)
                foreach (var room in rooms)
                {
                    var inner = room; inner.Expand(-.6f);
                    if (inner.Contains(c.point)) { covered = true; break; }
                }
                if (covered) continue;
                rooms.Add(c.box); capturePoints.Add(c.point);
            }
            HotelGraphicsLog.Write("probes: " + rooms.Count + " rooms, " + candidates.Count + " samples, " + existing.Count + " scene probes, level " + level);
            Debug.Log("Hotel reflection probes: found " + rooms.Count + " rooms in " + candidates.Count + " samples (" + existing.Count + " scene probes kept).");
        }

        bool MeasureRoom(Vector3 point, float floor, int mask, QueryTriggerInteraction trigger, out Bounds box)
        {
            box = default;
            float reach = maxRoomSize * .5f;
            int walls = 0;
            float Cast(Vector3 dir, float max)
            {
                if (Physics.Raycast(point, dir, out var h, max, mask, trigger)) { walls++; return h.distance; }
                return max;
            }
            float px = Cast(Vector3.right, reach), nx = Cast(Vector3.left, reach);
            float pz = Cast(Vector3.forward, reach), nz = Cast(Vector3.back, reach);
            int sides = walls;
            float up = Cast(Vector3.up, 9);
            bool ceiling = walls > sides;
            // Indoors = a ceiling and at least three walls. Outdoor terraces use the sky reflection.
            if (!ceiling || sides < 3) return false;
            var min = new Vector3(point.x - nx, floor, point.z - nz);
            var max = new Vector3(point.x + px, point.y + up, point.z + pz);
            box = new Bounds((min + max) * .5f, max - min);
            return box.size.x >= 1.5f && box.size.z >= 1.5f && box.size.y >= 2f;
        }

        static float Volume(Bounds b) => b.size.x * b.size.y * b.size.z;

        // ------------------------------------------------------------------ probes
        void Rebuild()
        {
            if (rendering != null) { StopCoroutine(rendering); rendering = null; }
            foreach (var p in probes) if (p) Destroy(p.gameObject);
            probes.Clear();
            int count = Mathf.Min(budget, rooms.Count);
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Room reflection " + (i + 1));
                go.transform.SetParent(transform, false);
                go.transform.position = capturePoints[i];
                var probe = go.AddComponent<ReflectionProbe>();
                var box = rooms[i];
                probe.mode = ReflectionProbeMode.Realtime;
                probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
                probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
                probe.boxProjection = true;
                probe.center = box.center - capturePoints[i];
                probe.size = box.size + Vector3.one * .2f;
                probe.blendDistance = .8f;
                probe.resolution = resolution;
                probe.hdr = true;
                probe.importance = 2;
                probe.intensity = 1;
                probe.nearClipPlane = .08f;
                probe.farClipPlane = Mathf.Clamp(box.extents.magnitude * 2.2f, 8, 60);
                probe.shadowDistance = Mathf.Min(30, probe.farClipPlane);
                probe.clearFlags = ReflectionProbeClearFlags.Skybox;
                probe.cullingMask = ~(1 << 5); // no UI
                probes.Add(probe);
            }
            dirty = count > 0;
            HotelGraphicsLog.Write("probes: built " + count + " probes at " + resolution + "px (budget " + budget + ")");
        }

        IEnumerator RenderAll()
        {
            renderedNight = HotelVisualDirector.Instance ? HotelVisualDirector.Instance.NightBlend : 0;
            // One probe at a time, one cube face per frame: spreads the cost of the extra scene renders.
            foreach (var probe in probes)
            {
                if (!probe) continue;
                int id = probe.RenderProbe();
                float giveUp = Time.unscaledTime + 3;
                while (probe && !probe.IsFinishedRendering(id) && Time.unscaledTime < giveUp) yield return null;
            }
            rendering = null;
            HotelGraphicsLog.Write("probes: rendered " + probes.Count + " probes (night " + renderedNight.ToString("0.00") + ")");
        }
    }
}
