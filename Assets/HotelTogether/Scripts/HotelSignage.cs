using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HotelTogether
{
    /// <summary>
    /// Replaces the plain text signs in HotelLobby with crafted 3D signage at runtime:
    /// - the CHECK-IN / RECEPTION board becomes a lit marquee with a brass bell on top;
    /// - the floating HOTEL MAP becomes a hanging directory board with numbered badges;
    /// - room door labels become brass-framed door plaques;
    /// - job/area signs (LAUNDRY, TRAY SERVICE, ...) get a gold header ribbon and key-cap instructions.
    /// Labels that scripts update (elevators, washers, luggage tags) are never touched.
    /// </summary>
    public sealed class HotelSignage : MonoBehaviour
    {
        [Tooltip("Turn the crafted signs off to see the original labels.")] public bool crafted = true;
        [Tooltip("Animated marquee bulbs on the reception sign.")] public bool marqueeLights = true;

        static readonly Color GoldColor = new Color(1f, .78f, .33f), WalnutColor = new Color(.30f, .16f, .08f), TealColor = new Color(.06f, .31f, .35f);
        static readonly Color Cream = new Color(1f, .95f, .82f), Ink = new Color(.22f, .12f, .06f), ChipColor = new Color(.10f, .12f, .20f);
        Material gold, walnut, teal, tealLight, chip, bulbA, bulbB, ribbon;
        Font font;
        readonly List<GameObject> built = new();
        readonly List<GameObject> hidden = new();
        readonly List<Renderer> hiddenRenderers = new();
        readonly HashSet<TextMesh> handled = new();
        static readonly Dictionary<string, Mesh> slabs = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() { SceneManager.sceneLoaded -= Loaded; SceneManager.sceneLoaded += Loaded; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { Attach(SceneManager.GetActiveScene()); }
        static void Loaded(Scene scene, LoadSceneMode mode) { Attach(scene); }
        static void Attach(Scene scene)
        {
            if (!Application.isPlaying || !scene.IsValid() || scene.name != "HotelLobby") return;
            foreach (var s in FindObjectsByType<HotelSignage>(FindObjectsSortMode.None)) if (s.gameObject.scene == scene) return;
            var go = new GameObject("Hotel crafted signage"); SceneManager.MoveGameObjectToScene(go, scene); go.AddComponent<HotelSignage>();
        }

        IEnumerator Start()
        {
            // Let other systems spawn their labels (and HotelWorldText upgrade fonts) first.
            yield return null; yield return null;
            font = Resources.Load<Font>("HotelTogetherUI/LilitaOne-Regular");
            if (!font || !crafted) yield break;
            BuildMaterials();
            StartCoroutine(BuildReceptionWhenReady());
            ScanLabels();
            yield return new WaitForSecondsRealtime(2);
            ScanLabels(); // labels created a little later (armory, maintenance racks)
            Debug.Log("Hotel signage: crafted " + built.Count + " signs.");
        }

        void Update()
        {
            if (!marqueeLights || !bulbA) return;
            float t = Time.time * 3.2f;
            float a = .55f + .45f * Mathf.Max(0, Mathf.Sin(t)), b = .55f + .45f * Mathf.Max(0, Mathf.Sin(t + Mathf.PI));
            bulbA.SetColor("_EmissionColor", new Color(1f, .78f, .42f) * (1.2f + a * 2.2f));
            bulbB.SetColor("_EmissionColor", new Color(1f, .78f, .42f) * (1.2f + b * 2.2f));
        }

        void OnDestroy()
        {
            foreach (var g in built) if (g) Destroy(g);
            foreach (var g in hidden) if (g) g.SetActive(true);
            foreach (var r in hiddenRenderers) if (r) r.enabled = true;
            foreach (var m in new[] { gold, walnut, teal, tealLight, chip, bulbA, bulbB, ribbon }) if (m) Destroy(m);
        }

        // ------------------------------------------------------------------ materials
        Material Lit(string name, Color color, float metallic, float smoothness)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var m = new Material(shader) { name = "Sign " + name, hideFlags = HideFlags.HideAndDontSave };
            m.SetColor("_BaseColor", color); m.SetColor("_Color", color);
            m.SetFloat("_Metallic", metallic); m.SetFloat("_Smoothness", smoothness); m.SetFloat("_Glossiness", smoothness);
            return m;
        }
        Material Emissive(string name, Color color)
        {
            var m = Lit(name, color, 0, .8f);
            m.EnableKeyword("_EMISSION"); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            m.SetColor("_EmissionColor", color * 2);
            return m;
        }
        void BuildMaterials()
        {
            gold = Lit("brass", GoldColor, .85f, .62f);
            ribbon = Lit("gold ribbon", new Color(1f, .74f, .26f), .55f, .5f);
            walnut = Lit("walnut", WalnutColor, 0, .45f);
            teal = Lit("teal face", TealColor, .05f, .55f);
            tealLight = Lit("teal badge", new Color(.12f, .5f, .55f), .1f, .6f);
            chip = Lit("key cap", ChipColor, .1f, .55f);
            bulbA = Emissive("bulb A", new Color(1f, .9f, .7f));
            bulbB = Emissive("bulb B", new Color(1f, .9f, .7f));
        }

        // ------------------------------------------------------------------ geometry
        static Mesh Slab(float w, float h, float d, float r)
        {
            r = Mathf.Clamp(r, 0, Mathf.Min(w, h) * .5f - .0001f);
            string key = w.ToString("0.####") + "x" + h.ToString("0.####") + "x" + d.ToString("0.####") + "r" + r.ToString("0.####");
            if (slabs.TryGetValue(key, out var cached) && cached) return cached;
            const int seg = 6;
            var outline = new List<Vector2>();
            Vector2[] centers = { new(w / 2 - r, h / 2 - r), new(-w / 2 + r, h / 2 - r), new(-w / 2 + r, -h / 2 + r), new(w / 2 - r, -h / 2 + r) };
            for (int c = 0; c < 4; c++)
                for (int i = 0; i <= seg; i++)
                {
                    float a = (c * 90 + i * 90f / seg) * Mathf.Deg2Rad;
                    outline.Add(centers[c] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
                }
            int n = outline.Count;
            var v = new List<Vector3>(); var nr = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            // Front (-Z) and back (+Z) caps as fans.
            for (int face = 0; face < 2; face++)
            {
                float z = face == 0 ? -d / 2 : d / 2; var normal = face == 0 ? Vector3.back : Vector3.forward;
                int start = v.Count; v.Add(new Vector3(0, 0, z)); nr.Add(normal); uv.Add(new Vector2(.5f, .5f));
                foreach (var p in outline) { v.Add(new Vector3(p.x, p.y, z)); nr.Add(normal); uv.Add(new Vector2(p.x / w + .5f, p.y / h + .5f)); }
                for (int i = 0; i < n; i++)
                {
                    int a = start + 1 + i, b = start + 1 + (i + 1) % n;
                    if (face == 0) { tri.Add(start); tri.Add(b); tri.Add(a); } else { tri.Add(start); tri.Add(a); tri.Add(b); }
                }
            }
            // Sides.
            for (int i = 0; i < n; i++)
            {
                var p = outline[i]; var q = outline[(i + 1) % n];
                var edge = (q - p).normalized; var normal = new Vector3(edge.y, -edge.x, 0);
                int s = v.Count;
                v.Add(new Vector3(p.x, p.y, -d / 2)); v.Add(new Vector3(q.x, q.y, -d / 2)); v.Add(new Vector3(q.x, q.y, d / 2)); v.Add(new Vector3(p.x, p.y, d / 2));
                for (int k = 0; k < 4; k++) { nr.Add(normal); uv.Add(Vector2.zero); }
                tri.Add(s); tri.Add(s + 2); tri.Add(s + 1); tri.Add(s); tri.Add(s + 3); tri.Add(s + 2);
            }
            var mesh = new Mesh { name = "Sign slab " + key, hideFlags = HideFlags.HideAndDontSave };
            mesh.SetVertices(v); mesh.SetNormals(nr); mesh.SetUVs(0, uv); mesh.SetTriangles(tri, 0); mesh.RecalculateBounds();
            slabs[key] = mesh; return mesh;
        }

        GameObject Piece(Transform parent, string name, Mesh mesh, Material material, Vector3 position, Quaternion? rotation = null, Vector3? scale = null)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            go.transform.localPosition = position; go.transform.localRotation = rotation ?? Quaternion.identity; go.transform.localScale = scale ?? Vector3.one;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = material; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }
        // A slab whose FRONT face sits at depth z (negative = toward the viewer).
        GameObject SlabAt(Transform parent, string name, float w, float h, float d, float r, Material m, float x, float y, float frontZ)
            => Piece(parent, name, Slab(w, h, d, r), m, new Vector3(x, y, frontZ + d / 2));

        static Mesh primitive(PrimitiveType type)
        {
            var key = "prim" + type;
            if (slabs.TryGetValue(key, out var m) && m) return m;
            var temp = GameObject.CreatePrimitive(type); m = temp.GetComponent<MeshFilter>().sharedMesh; Destroy(temp);
            slabs[key] = m; return m;
        }

        // ------------------------------------------------------------------ text
        float Measure(string text, float em)
        {
            font.RequestCharactersInTexture(text, 96);
            float px = 0;
            foreach (char c in text) if (font.GetCharacterInfo(c, out var info, 96)) px += info.advance;
            return px / 96f * em;
        }
        TextMesh Words(Transform parent, string text, float em, Color color, Vector3 position, TextAnchor anchor = TextAnchor.MiddleCenter, bool shadow = true)
        {
            var go = new GameObject("Text / " + text); go.transform.SetParent(parent, false); go.transform.localPosition = position;
            var t = go.AddComponent<TextMesh>();
            t.font = font; t.fontSize = 96; t.characterSize = em / 9.6f; t.text = text; t.anchor = anchor;
            t.alignment = anchor == TextAnchor.MiddleLeft ? TextAlignment.Left : TextAlignment.Center; t.color = color;
            var w = go.AddComponent<HotelWorldText>(); w.maximumDistance = 0; w.dropShadow = shadow;
            handled.Add(t);
            return t;
        }

        // ------------------------------------------------------------------ reception marquee
        IEnumerator BuildReceptionWhenReady()
        {
            // The board's mesh is streamed in by the glTF importer, so wait until it has real bounds.
            for (int attempt = 0; attempt < 40; attempt++)
            {
                if (BuildReception()) yield break;
                yield return new WaitForSecondsRealtime(.5f);
            }
            string report = "";
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (r && r.gameObject.name == "Node-342") { var f = r.GetComponent<MeshFilter>(); report += " [" + r.GetType().Name + " pos " + r.transform.position + " active " + r.gameObject.activeInHierarchy + " enabled " + r.enabled + " mesh " + (f && f.sharedMesh ? f.sharedMesh.name + " " + f.sharedMesh.vertexCount : "none") + " bounds " + r.bounds.size + "]"; }
            Debug.Log("Hotel signage: reception board not found." + report);
        }

        bool BuildReception()
        {
            Renderer target = null;
            var expected = new Vector3(-8.84f, 2.7f, -4.33f);
            foreach (var r in FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (r && r.gameObject.name == "Node-342" && (r.transform.position - expected).sqrMagnitude < 9) { target = r; break; }
            if (!target) return false;
            var mf = target.GetComponent<MeshFilter>();
            if (!mf || !mf.sharedMesh || target.bounds.size.sqrMagnitude < .01f) return false;
            Debug.Log("Hotel signage: replacing reception board " + target.name + " " + target.bounds.size);
            var b = target.bounds;
            // Front faces the open lobby: pick the thin axis and point it toward the room.
            Vector3 normal = b.size.x < b.size.z ? Vector3.right : Vector3.forward;
            var hotel = FindAnyObjectByType<HotelGuestSimulation>();
            Vector3 toward = (hotel ? hotel.receptionPoint + new Vector3(0, 1.5f, 0) : new Vector3(0, 1.5f, 5.8f)) - b.center;
            if (Vector3.Dot(toward, normal) < 0) normal = -normal;
            float W = b.size.x < b.size.z ? b.size.z : b.size.x, H = b.size.y;
            if (W < .5f || H < .2f) return true;
            target.enabled = false; hiddenRenderers.Add(target);

            var root = new GameObject("Crafted reception marquee").transform;
            root.SetParent(target.transform.parent, true);
            root.position = b.center + normal * .015f; root.rotation = Quaternion.LookRotation(-normal, Vector3.up);
            built.Add(root.gameObject);

            W = Mathf.Max(W, 2.2f) * 1.02f; H = Mathf.Max(H, .62f);
            SlabAt(root, "Walnut back", W + .16f, H + .16f, .07f, .1f, walnut, 0, 0, -.02f);
            SlabAt(root, "Brass frame", W + .04f, H + .04f, .03f, .07f, gold, 0, 0, -.045f);
            SlabAt(root, "Teal face", W - .12f, H - .12f, .03f, .05f, teal, 0, 0, -.06f);
            SlabAt(root, "Inner brass line", W - .2f, H - .2f, .005f, .04f, gold, 0, 0, -.062f);
            SlabAt(root, "Teal inner", W - .23f, H - .23f, .006f, .035f, teal, 0, 0, -.066f);

            // Marquee bulbs around the brass frame.
            var sphere = primitive(PrimitiveType.Sphere);
            float bw = W - .04f, bh = H - .04f, step = .15f; int k = 0;
            void Bulb(float x, float y) { Piece(root, "Bulb", sphere, k++ % 2 == 0 ? bulbA : bulbB, new Vector3(x, y, -.065f), null, Vector3.one * .05f); }
            int nx = Mathf.Max(2, Mathf.RoundToInt(bw / step)), ny = Mathf.Max(1, Mathf.RoundToInt(bh / step));
            for (int i = 0; i <= nx; i++) { float x = -bw / 2 + bw * i / nx; Bulb(x, bh / 2); Bulb(x, -bh / 2); }
            for (int i = 1; i < ny; i++) { float y = -bh / 2 + bh * i / ny; Bulb(-bw / 2, y); Bulb(bw / 2, y); }

            // Lettering.
            float em = Mathf.Min(H * .42f, (W - .5f) / Mathf.Max(.01f, Measure("RECEPTION", 1)) * .92f);
            Words(root, "RECEPTION", em, Cream, new Vector3(0, H * .1f, -.07f));
            float sub = em * .36f;
            var ribbonW = Measure("CHECK-IN  /  CHECK-OUT", sub) + sub * 2.4f;
            SlabAt(root, "Ribbon", ribbonW, sub * 1.7f, .015f, sub * .85f, ribbon, 0, -H * .3f, -.08f);
            Words(root, "CHECK-IN  /  CHECK-OUT", sub, Ink, new Vector3(0, -H * .3f, -.082f), TextAnchor.MiddleCenter, false);

            // Brass service bell on top.
            float top = H / 2 + .08f;
            var cyl = primitive(PrimitiveType.Cylinder);
            Piece(root, "Bell base", cyl, walnut, new Vector3(0, top + .03f, -.05f), null, new Vector3(.34f, .03f, .2f));
            Piece(root, "Bell dome", sphere, gold, new Vector3(0, top + .1f, -.05f), null, new Vector3(.24f, .2f, .16f));
            Piece(root, "Bell knob", sphere, gold, new Vector3(0, top + .22f, -.05f), null, Vector3.one * .05f);
            for (int i = -1; i <= 1; i++)
                Piece(root, "Ding ray", primitive(PrimitiveType.Cube), ribbon, new Vector3(i * .13f, top + .3f + (i == 0 ? .04f : 0), -.05f), Quaternion.Euler(0, 0, -i * 30), new Vector3(.025f, .08f, .02f));

            // Warm glow on the counter area.
            var light = new GameObject("Marquee glow").AddComponent<Light>();
            light.transform.SetParent(root, false); light.transform.localPosition = new Vector3(0, 0, -.6f);
            light.type = LightType.Point; light.range = 2.6f; light.intensity = .8f; light.color = new Color(1f, .82f, .55f); light.shadows = LightShadows.None;
            return true;
        }

        // ------------------------------------------------------------------ labels
        static readonly Regex RoomDoor = new(@"^ROOM\s+(\d{3})$"), GuestDoor = new(@"^(\d{3})\s*/\s*(.+)$"), ZoneCode = new(@"^([A-Z]\d{2})\s*/\s*(.+)$");
        static readonly string[] InfoHeaders = { "HOTEL MAP", "GUEST SERVICE", "TRAY SERVICE", "TAGGED LUGGAGE", "HOUSEKEEPING", "LAUNDRY", "FRESH", "REFILL", "ESPRESSO", "COFFEE", "MAINTENANCE", "ARMORY" };

        void ScanLabels()
        {
            foreach (var label in FindObjectsByType<TextMesh>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!label || handled.Contains(label) || label.GetComponent<HotelWorldTextShadow>() || label.gameObject.scene != gameObject.scene) continue;
                // Never touch labels that scripts rewrite.
                if (label.GetComponentInParent<HotelElevator>() || label.GetComponentInParent<HotelLaundryMachine>() || label.GetComponentInParent<HotelGuestDelivery>() || label.GetComponentInParent<HotelUtilityPlant>()) continue;
                var lines = new List<string>();
                foreach (var raw in (label.text ?? "").Split('\n')) { var t = raw.Trim(); if (t.Length > 0) lines.Add(t); }
                if (lines.Count == 0) continue;
                float em = Mathf.Max(.004f, label.characterSize * (label.fontSize <= 0 ? 13 : label.fontSize) * .1f);
                string head = lines[0].ToUpperInvariant();
                Transform root = null;
                Match m;
                if ((m = RoomDoor.Match(head)).Success) root = BuildDoorPlaque(label, m.Groups[1].Value, lines.Count > 1 ? lines[1] : null, em);
                else if ((m = GuestDoor.Match(lines[0])).Success && lines.Count == 1) root = BuildDoorPlaque(label, m.Groups[1].Value, m.Groups[2].Value, em, true);
                else if ((m = ZoneCode.Match(lines[0])).Success && lines.Count == 1) root = BuildBadgeSign(label, m.Groups[1].Value, m.Groups[2].Value, em);
                else if (head == "HOTEL MAP") root = BuildDirectory(label, lines, em);
                else foreach (var h in InfoHeaders) if (head.StartsWith(h)) { root = BuildInfoSign(label, lines, em); break; }
                if (!root) continue;
                handled.Add(label);
                label.gameObject.SetActive(false); hidden.Add(label.gameObject);
            }
        }

        Transform RootAt(TextMesh label, string name)
        {
            var root = new GameObject(name).transform;
            root.SetParent(label.transform.parent, false);
            root.localPosition = label.transform.localPosition; root.localRotation = label.transform.localRotation; root.localScale = label.transform.localScale;
            built.Add(root.gameObject);
            return root;
        }

        // Sign box with walnut back, brass frame and teal face; returns the z of the face for text.
        float Board(Transform root, float w, float h, float u)
        {
            float d = u * .5f;
            SlabAt(root, "Walnut back", w + u * .7f, h + u * .7f, d, u * .9f, walnut, 0, 0, u * .05f);
            SlabAt(root, "Brass frame", w + u * .25f, h + u * .25f, d * .5f, u * .7f, gold, 0, 0, -u * .12f);
            SlabAt(root, "Teal face", w - u * .3f, h - u * .3f, d * .5f, u * .5f, teal, 0, 0, -u * .2f);
            return -u * .22f;
        }

        // Brass screw heads in the corners.
        void Screws(Transform root, float w, float h, float u, float z)
        {
            var sphere = primitive(PrimitiveType.Sphere);
            foreach (var sx in new[] { -1, 1 }) foreach (var sy in new[] { -1, 1 })
                Piece(root, "Screw", sphere, gold, new Vector3(sx * (w / 2 - u * .45f), sy * (h / 2 - u * .45f), z), null, Vector3.one * u * .32f);
        }

        // When nothing is behind the sign, hang it from two brass rods.
        void HangIfFloating(Transform root, float w, float h, float u)
        {
            var center = root.position; var into = root.forward;
            float scale = root.lossyScale.y;
            if (Physics.Raycast(center - into * .02f, into, .25f, ~0, QueryTriggerInteraction.Ignore)) return;
            float length = .6f;
            var topWorld = root.TransformPoint(new Vector3(0, h / 2, 0));
            if (Physics.Raycast(topWorld, Vector3.up, out var hit, 3f, ~0, QueryTriggerInteraction.Ignore)) length = hit.distance;
            length /= Mathf.Max(.0001f, scale);
            var cyl = primitive(PrimitiveType.Cylinder);
            foreach (var sx in new[] { -1, 1 })
            {
                float x = sx * (w / 2 - u * 1.2f);
                Piece(root, "Hanging rod", cyl, gold, new Vector3(x, h / 2 + length / 2, u * .1f), null, new Vector3(u * .14f, length / 2, u * .14f));
                Piece(root, "Rod cap", primitive(PrimitiveType.Sphere), gold, new Vector3(x, h / 2 + u * .2f, u * .1f), null, Vector3.one * u * .4f);
            }
        }

        Transform BuildDirectory(TextMesh label, List<string> lines, float em)
        {
            var root = RootAt(label, "Crafted hotel map");
            float u = em, rowH = em * 1.75f, header = em * 1.25f;
            var entries = lines.GetRange(1, lines.Count - 1);
            float textW = Measure(lines[0], header) + u * 3;
            foreach (var e in entries) textW = Mathf.Max(textW, Measure(Label(e), em) + u * 3.6f);
            float w = textW + u * 1.5f, h = header * 2.2f + entries.Count * rowH + u * 1.2f;
            float z = Board(root, w, h, u);
            Screws(root, w, h, u, z - u * .05f);
            // Header ribbon.
            float hy = h / 2 - header * 1.15f;
            SlabAt(root, "Header ribbon", w + u * .9f, header * 1.6f, u * .25f, header * .8f, ribbon, 0, hy, z - u * .1f);
            Words(root, lines[0].ToUpperInvariant(), header, Ink, new Vector3(0, hy, z - u * .37f), TextAnchor.MiddleCenter, false);
            // Rows with numbered badges.
            var cyl = primitive(PrimitiveType.Cylinder);
            float y = hy - header * 1.25f - rowH * .5f;
            float left = -w / 2 + u * 1.1f;
            foreach (var e in entries)
            {
                string code = Code(e), text = Label(e);
                var badge = Piece(root, "Badge", cyl, gold, new Vector3(left + u * .6f, y, z - u * .06f), Quaternion.Euler(90, 0, 0), new Vector3(u * 1.25f, u * .06f, u * 1.25f));
                Piece(root, "Badge face", cyl, tealLight, new Vector3(left + u * .6f, y, z - u * .14f), Quaternion.Euler(90, 0, 0), new Vector3(u * 1.0f, u * .03f, u * 1.0f));
                Words(root, code, em * .8f, Cream, new Vector3(left + u * .6f, y, z - u * .2f), TextAnchor.MiddleCenter, false);
                Words(root, text, em, Cream, new Vector3(left + u * 1.6f, y, z - u * .06f), TextAnchor.MiddleLeft);
                y -= rowH;
            }
            HangIfFloating(root, w, h, u);
            return root;
        }

        static string Code(string line)
        {
            var parts = line.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 1 && parts[0].Length <= 2 && char.IsDigit(parts[0][0])) return parts[0];
            foreach (char c in line) if (char.IsLetterOrDigit(c)) return char.ToUpperInvariant(c).ToString();
            return "*";
        }
        static string Label(string line)
        {
            var parts = line.Split(new[] { ' ' }, 2, System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 && parts[0].Length <= 2 && char.IsDigit(parts[0][0])) return parts[1].Trim();
            return line.Trim();
        }

        Transform BuildDoorPlaque(TextMesh label, string number, string action, float em, bool guestName = false)
        {
            var root = RootAt(label, "Crafted door plaque " + number);
            float u = em, big = em * 1.9f, small = em * .7f;
            float w = Mathf.Max(Measure(number, big), Measure(guestName ? action ?? "" : "ROOM", small)) + u * 2.6f;
            float h = big + small + u * 2.2f;
            var root2 = root;
            float d = u * .5f;
            // Arched brass plaque: rounded walnut back, brass rim, teal face.
            SlabAt(root, "Walnut back", w + u * .6f, h + u * .6f, d, Mathf.Min(w, h) * .5f, walnut, 0, 0, u * .05f);
            SlabAt(root, "Brass rim", w + u * .2f, h + u * .2f, d * .5f, Mathf.Min(w, h) * .45f, gold, 0, 0, -u * .12f);
            SlabAt(root, "Teal face", w - u * .35f, h - u * .35f, d * .5f, Mathf.Min(w, h) * .4f, teal, 0, 0, -u * .2f);
            float z = -u * .22f;
            Words(root, guestName ? (action ?? "").ToUpperInvariant() : "ROOM", small, new Color(1f, .84f, .45f), new Vector3(0, h / 2 - u * .9f - small * .4f, z), TextAnchor.MiddleCenter, false);
            Words(root, number, big, Cream, new Vector3(0, -u * .25f, z));
            // Key-cap hint tab under the plaque ("E  DELIVER").
            if (!guestName && !string.IsNullOrEmpty(action))
            {
                var (key, rest) = SplitKey(action);
                float tw = (key != null ? u * 1.4f : 0) + Measure(rest, em * .75f) + u * 1.4f, th = em * 1.45f, ty = -h / 2 - th * .5f - u * .35f;
                SlabAt(root, "Hint tab", tw, th, u * .3f, th * .5f, chip, 0, ty, -u * .05f);
                float x = -tw / 2 + u * .7f;
                if (key != null) { KeyCap(root, key, new Vector3(x + u * .45f, ty, -u * .12f), em * .75f, u); x += u * 1.4f; }
                Words(root, rest, em * .75f, Cream, new Vector3(x, ty, -u * .12f), TextAnchor.MiddleLeft, false);
            }
            return root2;
        }

        Transform BuildBadgeSign(TextMesh label, string code, string name, float em)
        {
            var root = RootAt(label, "Crafted area sign " + code);
            float u = em, w = Measure(name, em) + u * 4.4f, h = em * 2.3f;
            float z = Board(root, w, h, u);
            var cyl = primitive(PrimitiveType.Cylinder);
            float bx = -w / 2 + u * 1.3f;
            Piece(root, "Code badge", cyl, gold, new Vector3(bx, 0, z - u * .06f), Quaternion.Euler(90, 0, 0), new Vector3(u * 1.9f, u * .06f, u * 1.9f));
            Piece(root, "Code face", cyl, tealLight, new Vector3(bx, 0, z - u * .14f), Quaternion.Euler(90, 0, 0), new Vector3(u * 1.6f, u * .03f, u * 1.6f));
            Words(root, code, em * .62f, Cream, new Vector3(bx, 0, z - u * .2f), TextAnchor.MiddleCenter, false);
            Words(root, name, em, Cream, new Vector3(bx + u * 1.3f, 0, z - u * .06f), TextAnchor.MiddleLeft);
            HangIfFloating(root, w, h, u);
            return root;
        }

        Transform BuildInfoSign(TextMesh label, List<string> lines, float em)
        {
            var root = RootAt(label, "Crafted sign " + lines[0]);
            float u = em, header = em * 1.2f, rowH = em * 1.7f;
            var rows = lines.GetRange(1, lines.Count - 1);
            float w = Measure(lines[0].ToUpperInvariant(), header) + u * 3.5f;
            foreach (var r in rows) w = Mathf.Max(w, RowWidth(r, em, u) + u * 2.5f);
            float h = header * 1.9f + rows.Count * rowH + u * (rows.Count > 0 ? 1.2f : .6f);
            float z = Board(root, w, h, u);
            Screws(root, w, h, u, z - u * .05f);
            float hy = rows.Count > 0 ? h / 2 - header * 1.05f : 0;
            SlabAt(root, "Header ribbon", w + u * .9f, header * 1.6f, u * .25f, header * .8f, ribbon, 0, hy, z - u * .1f);
            Words(root, lines[0].ToUpperInvariant(), header, Ink, new Vector3(0, hy, z - u * .37f), TextAnchor.MiddleCenter, false);
            float y = hy - header * 1.15f - rowH * .5f;
            foreach (var r in rows) { DrawRow(root, r, em, u, y, z); y -= rowH; }
            HangIfFloating(root, w, h, u);
            return root;
        }

        // "E LOAD FOOD / E SERVE" -> [E] LOAD FOOD  •  [E] SERVE
        static (string key, string rest) SplitKey(string segment)
        {
            segment = segment.Trim();
            if (segment.Length == 1 && char.IsLetter(segment[0])) return (segment, "");
            if (segment.Length > 2 && char.IsLetter(segment[0]) && segment[1] == ' ' && char.IsUpper(segment[0])) return (segment.Substring(0, 1), segment.Substring(2).Trim());
            return (null, segment);
        }
        float RowWidth(string row, float em, float u)
        {
            float w = 0; var segs = row.Split('/');
            for (int i = 0; i < segs.Length; i++)
            {
                var (key, rest) = SplitKey(segs[i]);
                if (key != null) w += u * 1.4f;
                if (rest.Length > 0) w += Measure(rest, em) + u * .3f;
                if (i < segs.Length - 1) w += u * 1.2f;
            }
            return w;
        }
        void DrawRow(Transform root, string row, float em, float u, float y, float z)
        {
            float x = -RowWidth(row, em, u) / 2; var segs = row.Split('/');
            var sphere = primitive(PrimitiveType.Sphere);
            for (int i = 0; i < segs.Length; i++)
            {
                var (key, rest) = SplitKey(segs[i]);
                if (key != null) { KeyCap(root, key, new Vector3(x + u * .55f, y, z - u * .02f), em * .8f, u); x += u * 1.4f; }
                if (rest.Length > 0) { Words(root, rest, em, Cream, new Vector3(x, y, z - u * .06f), TextAnchor.MiddleLeft); x += Measure(rest, em) + u * .3f; }
                if (i < segs.Length - 1) { Piece(root, "Dot", sphere, gold, new Vector3(x + u * .45f, y, z - u * .05f), null, Vector3.one * u * .32f); x += u * 1.2f; }
            }
        }
        void KeyCap(Transform root, string key, Vector3 at, float em, float u)
        {
            SlabAt(root, "Key cap edge", u * 1.15f, u * 1.15f, u * .25f, u * .28f, gold, at.x, at.y, at.z - u * .02f);
            SlabAt(root, "Key cap", u * .95f, u * .95f, u * .2f, u * .22f, chip, at.x, at.y + u * .02f, at.z - u * .12f);
            Words(root, key, em, Cream, new Vector3(at.x, at.y + u * .02f, at.z - u * .34f), TextAnchor.MiddleCenter, false);
        }
    }
}
