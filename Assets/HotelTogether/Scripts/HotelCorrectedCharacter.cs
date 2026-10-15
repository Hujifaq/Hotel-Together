using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace HotelTogether
{
    // The hotel guests already have an animation rig driven by HotelGuestVisual.
    // Skin the corrected Tripo geometry to those same bones so every existing
    // walking, seated, service, fleeing, attack, and blink pose still plays.
    internal static class HotelCorrectedCharacter
    {
        const string CustomerPath = "HotelTogetherCharacters/Customer-Corrected-v2";
        const string ZombiePath = "HotelTogetherCharacters/Zombie-Corrected";
        const string LookPath = "HotelTogetherCharacters/CustomerLooks/";
        static readonly string[] LookNames = { "01-Tropical", "02-Coastal", "03-Garden", "04-Sunset", "05-Midnight", "06-Skyline" };
        static readonly Color32[] SkinColors =
        {
            new Color32(251, 177, 128, 255), new Color32(205, 139, 95, 255),
            new Color32(247, 195, 151, 255), new Color32(184, 112, 78, 255),
            new Color32(136, 80, 61, 255), new Color32(231, 168, 119, 255)
        };
        static readonly Color32[] HairColors =
        {
            new Color32(151, 88, 35, 255), new Color32(55, 42, 40, 255),
            new Color32(188, 100, 55, 255), new Color32(89, 57, 39, 255),
            new Color32(45, 38, 49, 255), new Color32(204, 160, 91, 255)
        };

        sealed class Part
        {
            public Mesh mesh;
            public Material[] materials;
        }
        sealed class Model
        {
            public Part[] parts;
            public Vector3 leftEye, rightEye;
        }

        static Model customerModel, zombieModel;
        static Material[][] customerLookMaterials;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache()
        {
            customerModel = null;
            zombieModel = null;
            customerLookMaterials = null;
        }

        static bool IsValid(Model model) => model != null && model.parts != null &&
            model.parts.Length > 0 && model.parts.All(p => p.mesh && p.materials.All(m => m));

        public static void Install(HotelGuestVisual visual)
        {
            bool zombie = visual.GetComponentInParent<HotelZombieBot>() != null ||
                          visual.transform.root.name.IndexOf("zombie", StringComparison.OrdinalIgnoreCase) >= 0;
            if (zombie)
            {
                // The bellhop mesh has lower shoulders and outward-sloping arms.
                // Bind at its joints, rather than the older guest skeleton's neck.
                visual.leftArm.localPosition = new Vector3(-.20f, -.035f, -.075f);
                visual.rightArm.localPosition = new Vector3(.20f, -.035f, -.075f);
                visual.leftForearm.localPosition = new Vector3(-.15f, -.26f, .025f);
                visual.rightForearm.localPosition = new Vector3(.15f, -.26f, .025f);
            }
            else
            {
                // The corrected customer's shoulders are wider than the old
                // procedural rig. Place the pivots inside the actual sleeves.
                visual.leftArm.localPosition = new Vector3(-.36f, .105f, 0);
                visual.rightArm.localPosition = new Vector3(.36f, .105f, 0);
                visual.leftForearm.localPosition = new Vector3(-.07f, -.23f, 0);
                visual.rightForearm.localPosition = new Vector3(.07f, -.23f, 0);
            }
            var model = zombie ? zombieModel : customerModel;
            if (!IsValid(model))
            {
                model = Build(visual, zombie);
                if (!IsValid(model)) return; // Keep the original art if the GLB failed to import.
                if (zombie) zombieModel = model; else customerModel = model;
            }
            PositionEyes(visual, model);
            var bones = Bones(visual);
            var originals = visual.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var part in model.parts)
            {
                var rendererObject = new GameObject(zombie ? "Corrected zombie skin" : "Corrected customer skin");
                rendererObject.transform.SetParent(visual.transform, false);
                rendererObject.layer = visual.gameObject.layer;
                var renderer = rendererObject.AddComponent<SkinnedMeshRenderer>();
                renderer.sharedMesh = part.mesh;
                renderer.sharedMaterials = part.materials;
                renderer.bones = bones;
                renderer.rootBone = visual.transform;
                renderer.quality = zombie ? SkinQuality.Bone4 : SkinQuality.Bone2;
                renderer.updateWhenOffscreen = false;
                renderer.localBounds = new Bounds(new Vector3(0, .86f, 0), new Vector3(2, 2.1f, 2));
            }
            foreach (var old in originals) old.enabled = false;
            if (!zombie)
                visual.SetLuggageGrip(visual.transform.TransformPoint(GripPoint));
            HotelToonShading.Apply(visual.gameObject);
        }

        static readonly Vector3 Wrist = new Vector3(.49f, .63f, .035f);
        static readonly Quaternion GripTurn = Quaternion.Euler(0, 90, 0);
        static Vector3 GripPoint => Wrist + GripTurn * (new Vector3(.495f, .54f, .055f) - Wrist);

        public static void ApplyCustomerLook(HotelGuestVisual visual, int lookIndex)
        {
            if (!visual || !IsValid(customerModel)) return;
            int index = ((lookIndex % LookNames.Length) + LookNames.Length) % LookNames.Length;
            if (customerLookMaterials == null) customerLookMaterials = new Material[LookNames.Length][];
            if (customerLookMaterials[index] == null || customerLookMaterials[index].Any(m => !m))
            {
                var atlas = Resources.Load<Texture2D>(LookPath + LookNames[index]);
                if (!atlas)
                {
                    Debug.LogError("Missing customer UV atlas: " + LookNames[index]);
                    return;
                }
                var baseMaterials = customerModel.parts[0].materials;
                var materials = (Material[])baseMaterials.Clone();
                for (int i = 0; i < materials.Length; i++)
                {
                    string name = materials[i].name.ToLowerInvariant();
                    if (!name.Contains("shirthairclothes") && name != "skin" && !name.Contains("eyebrow")) continue;
                    var material = new Material(materials[i]) { name = materials[i].name + " / " + LookNames[index] };
                    if (name.Contains("shirthairclothes")) material.mainTexture = atlas;
                    else material.color = name == "skin" ? SkinColors[index] : HairColors[index];
                    materials[i] = material;
                }
                customerLookMaterials[index] = materials;
            }
            foreach (var renderer in visual.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!renderer.enabled || !renderer.name.StartsWith("Corrected customer skin", StringComparison.Ordinal)) continue;
                renderer.sharedMaterials = customerLookMaterials[index];
                for (int i = 1; i < renderer.sharedMesh.blendShapeCount; i++)
                    renderer.SetBlendShapeWeight(i, i == index ? 100 : 0);
            }
            var bun = visual.head.Find("Customer hair bun");
            if (index == 5 && !bun)
            {
                var bunObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bunObject.name = "Customer hair bun";
                bunObject.transform.SetParent(visual.head, false);
                bunObject.transform.localPosition = new Vector3(0, .16f, -.48f);
                bunObject.transform.localScale = new Vector3(.36f, .28f, .31f);
                bunObject.layer = visual.gameObject.layer;
                var collider = bunObject.GetComponent<Collider>();
                if (Application.isPlaying) UnityEngine.Object.Destroy(collider);
                else UnityEngine.Object.DestroyImmediate(collider);
                bunObject.GetComponent<MeshRenderer>().sharedMaterial = customerLookMaterials[index][3];
            }
            else if (bun) bun.gameObject.SetActive(index == 5);
            HotelToonShading.Apply(visual.gameObject);
        }

        static void AddHairStyles(Mesh mesh, Vector3[] vertices, byte[] roles)
        {
            var mask = Resources.Load<Texture2D>(LookPath + "Hair-UV-Mask");
            if (!mask || !mask.isReadable)
            {
                Debug.LogError("Customer hair UV mask is missing or not readable.");
                return;
            }
            var uv = mesh.uv;
            var hair = new float[vertices.Length];
            for (int i = 0; i < hair.Length; i++)
                if (roles[i] == 6 && vertices[i].y > 1.28f)
                    hair[i] = mask.GetPixelBilinear(uv[i].x, uv[i].y).r;
            var normalDelta = new Vector3[vertices.Length];
            for (int style = 1; style < LookNames.Length; style++)
            {
                var delta = new Vector3[vertices.Length];
                for (int i = 0; i < vertices.Length; i++)
                {
                    if (hair[i] < .5f) continue;
                    var p = vertices[i];
                    float top = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1.36f, 1.69f, p.y));
                    float front = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-.03f, .23f, p.z));
                    float side = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.12f, .28f, Mathf.Abs(p.x)));
                    Vector3 offset = style switch
                    {
                        1 => new Vector3(-p.x * .13f * side, .075f * (1 - top) * side - .085f * top, -p.z * .055f * top), // short crop
                        2 => new Vector3(.18f * (.4f + .6f * top) * front, .03f * top, .045f * front * top), // side sweep
                        3 => new Vector3(0, .17f * top * front * (.76f + .24f * Mathf.Cos(p.x * 24)), .065f * top * front), // quiff
                        4 => new Vector3(Mathf.Sign(p.x) * .045f * side, -.16f * (1 - top) * side, 0), // longer sides
                        _ => new Vector3(0, .04f * top * (1 - front), -.17f * top * front) // swept back bun
                    };
                    delta[i] = offset * hair[i];
                }
                mesh.AddBlendShapeFrame("HairStyle" + style, 100, delta, normalDelta, null);
            }
        }

        static void AddLuggageGrip(Mesh mesh, Vector3[] vertices, byte[] roles)
        {
            var closed = (Vector3[])vertices.Clone();
            for (int i = 0; i < vertices.Length; i++)
            {
                var p = vertices[i];
                if (roles[i] != 5 || p.x < .35f || p.y > .73f || p.y < .35f) continue;
                // Curl the fingers toward the palm around their knuckle line.
                float d = Mathf.Max(0, .54f - p.y);
                float angle = Mathf.Min(d / .065f, 1.9f);
                var curled = p;
                if (d > 0)
                {
                    float thickness = p.x - .56f;
                    curled.x = .56f - .065f * (1 - Mathf.Cos(angle)) + thickness * Mathf.Cos(angle);
                    curled.y = .54f - .065f * Mathf.Sin(angle) - thickness * Mathf.Sin(angle);
                    curled = Vector3.Lerp(p, curled, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.47f, .53f, p.x)));
                }
                float thumb = (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.45f, .51f, p.x))) *
                    Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.60f, .48f, p.y));
                curled += new Vector3(.035f, .035f, -.015f) * thumb;
                float turn = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.73f, .60f, p.y));
                closed[i] = Wrist + Quaternion.Slerp(Quaternion.identity, GripTurn, turn) * (curled - Wrist);
            }
            var normalMesh = UnityEngine.Object.Instantiate(mesh);
            normalMesh.vertices = closed;
            normalMesh.RecalculateNormals();
            var normal = normalMesh.normals;
            var originalNormal = mesh.normals;
            var delta = new Vector3[vertices.Length];
            var deltaNormal = new Vector3[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                delta[i] = closed[i] - vertices[i];
                if (delta[i].sqrMagnitude > 0) deltaNormal[i] = normal[i] - originalNormal[i];
            }
            mesh.AddBlendShapeFrame("LuggageGrip", 100, delta, deltaNormal, null);
            if (Application.isPlaying) UnityEngine.Object.Destroy(normalMesh);
            else UnityEngine.Object.DestroyImmediate(normalMesh);
        }

        static Transform[] Bones(HotelGuestVisual visual)
        {
            return new[]
            {
                visual.hips, visual.hips.Find("Torso"), visual.head,
                visual.leftLeg, visual.leftShin, visual.rightLeg, visual.rightShin,
                visual.leftArm, visual.leftForearm, visual.rightArm, visual.rightForearm,
                visual.leftEye, visual.rightEye, visual.leftBrow, visual.rightBrow
            };
        }

        static void PositionEyes(HotelGuestVisual visual, Model model)
        {
            visual.leftEye.position = visual.transform.TransformPoint(model.leftEye);
            visual.rightEye.position = visual.transform.TransformPoint(model.rightEye);
            visual.leftBrow.position = visual.transform.TransformPoint(model.leftEye + Vector3.up * .13f);
            visual.rightBrow.position = visual.transform.TransformPoint(model.rightEye + Vector3.up * .13f);
        }

        static Vector3 Mean(IEnumerable<Vector3> points)
        {
            Vector3 sum = Vector3.zero; int count = 0;
            foreach (var point in points) { sum += point; count++; }
            return count == 0 ? Vector3.zero : sum / count;
        }

        static (Vector3 left, Vector3 right) EyeCenters(Vector3[] points)
        {
            var left = points[0]; var right = points[0];
            foreach (var point in points)
            {
                if (point.x < left.x) left = point;
                if (point.x > right.x) right = point;
            }
            for (int iteration = 0; iteration < 16; iteration++)
            {
                Vector3 leftSum = Vector3.zero, rightSum = Vector3.zero;
                int leftCount = 0, rightCount = 0;
                foreach (var point in points)
                {
                    if ((point - left).sqrMagnitude < (point - right).sqrMagnitude)
                    { leftSum += point; leftCount++; }
                    else { rightSum += point; rightCount++; }
                }
                if (leftCount > 0) left = leftSum / leftCount;
                if (rightCount > 0) right = rightSum / rightCount;
            }
            return left.x < right.x ? (left, right) : (right, left);
        }

        static Model Build(HotelGuestVisual visual, bool zombie)
        {
            var imported = Resources.Load<GameObject>(zombie ? ZombiePath : CustomerPath);
            if (!imported)
            {
                Debug.LogError("Corrected character GLB missing from Resources: " + (zombie ? ZombiePath : CustomerPath));
                return null;
            }
            var filters = imported.GetComponentsInChildren<MeshFilter>(true);
            if (filters.Length == 0)
            {
                Debug.LogError("Corrected character GLB has no MeshFilter: " + imported.name);
                return null;
            }
            var sources = new List<(Mesh mesh, MeshRenderer renderer, Vector3[] vertices, Vector3[] normals)>();
            var all = new List<Vector3>();
            var eyeWhite = new List<Vector3>();
            var pupils = new List<Vector3>();
            foreach (var filter in filters)
            {
                var mesh = filter.sharedMesh;
                var renderer = filter.GetComponent<MeshRenderer>();
                if (!mesh || !renderer) continue;
                var matrix = imported.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                var vertices = mesh.vertices.Select(p => matrix.MultiplyPoint3x4(p)).ToArray();
                var sourceNormals = mesh.normals;
                var normals = sourceNormals.Length == vertices.Length
                    ? sourceNormals.Select(n => matrix.MultiplyVector(n).normalized).ToArray()
                    : Enumerable.Repeat(Vector3.up, vertices.Length).ToArray();
                sources.Add((mesh, renderer, vertices, normals));
                all.AddRange(vertices);
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    string name = renderer.sharedMaterials[sub].name.ToLowerInvariant();
                    var target = name.Contains("pupil") ? pupils : name.Contains("eye_white") ? eyeWhite : null;
                    if (target == null) continue;
                    foreach (int index in mesh.GetIndices(sub)) target.Add(vertices[index]);
                }
            }
            if (all.Count == 0 || eyeWhite.Count == 0 || pupils.Count == 0)
            {
                Debug.LogError("Corrected character GLB is missing geometry or separate eye materials: " + imported.name);
                return null;
            }
            var low = all.Aggregate(Vector3.Min);
            var high = all.Aggregate(Vector3.Max);
            var center = new Vector3((low.x + high.x) * .5f, low.y, (low.z + high.z) * .5f);
            var gazeDifference = Mean(pupils) - Mean(eyeWhite);
            // The customer source is turned about 59 degrees. Its matching
            // pupils and whites give the exact forward axis after glTF import.
            var facing = zombie ? Vector3.forward : Vector3.ProjectOnPlane(gazeDifference, Vector3.up).normalized;
            var turn = Quaternion.FromToRotation(facing, Vector3.forward);
            float scale = 1.72f / (high.y - low.y);
            Vector3 Convert(Vector3 p) => turn * ((p - center) * scale);
            var eyePositions = eyeWhite.Select(Convert).ToArray();
            var eyeCenters = EyeCenters(eyePositions);
            var model = new Model
            {
                leftEye = eyeCenters.left,
                rightEye = eyeCenters.right
            };
            PositionEyes(visual, model);
            var bones = Bones(visual);
            if (bones.Any(b => !b))
            {
                Debug.LogError("The hotel character's original animation skeleton is incomplete: " + visual.name);
                return null;
            }
            var parts = new List<Part>();
            foreach (var source in sources)
            {
                var vertices = source.vertices.Select(Convert).ToArray();
                var mesh = new Mesh { name = imported.name + " animated skin", indexFormat = IndexFormat.UInt32 };
                mesh.vertices = vertices;
                mesh.normals = source.normals.Select(n => (turn * n).normalized).ToArray();
                mesh.uv = source.mesh.uv;
                if (source.mesh.uv2.Length == vertices.Length) mesh.uv2 = source.mesh.uv2;
                if (source.mesh.colors.Length == vertices.Length) mesh.colors = source.mesh.colors;
                var tangent = source.mesh.tangents;
                if (tangent.Length == vertices.Length)
                    mesh.tangents = tangent.Select(t => { var v = turn * new Vector3(t.x, t.y, t.z); return new Vector4(v.x, v.y, v.z, t.w); }).ToArray();
                mesh.subMeshCount = source.mesh.subMeshCount;
                var roles = new byte[vertices.Length];
                for (int sub = 0; sub < source.mesh.subMeshCount; sub++)
                {
                    var indices = source.mesh.GetIndices(sub);
                    mesh.SetIndices(indices, source.mesh.GetTopology(sub), sub, false);
                    string name = source.renderer.sharedMaterials[sub].name.ToLowerInvariant();
                    bool eye = name.Contains("pupil") || name.Contains("eye_white");
                    bool brow = name.Contains("eyebrow");
                    bool skin = name.Equals("skin", StringComparison.OrdinalIgnoreCase);
                    bool clothing = name.Contains("shirthairclothes");
                    if (eye || brow)
                        foreach (int index in indices)
                        {
                            bool left = (vertices[index] - model.leftEye).sqrMagnitude <
                                        (vertices[index] - model.rightEye).sqrMagnitude;
                            roles[index] = eye ? (byte)(left ? 1 : 2) : (byte)(left ? 3 : 4);
                        }
                    else if (clothing)
                        foreach (int index in indices) roles[index] = 6;
                    else if (skin)
                        foreach (int index in indices) if (roles[index] == 0) roles[index] = 5;
                }
                var weights = new BoneWeight[vertices.Length];
                for (int i = 0; i < vertices.Length; i++)
                    weights[i] = Weight(vertices[i], roles[i], zombie);
                mesh.boneWeights = weights;
                mesh.bindposes = bones.Select(b => b.worldToLocalMatrix * visual.transform.localToWorldMatrix).ToArray();
                if (!zombie)
                {
                    AddLuggageGrip(mesh, vertices, roles);
                    AddHairStyles(mesh, vertices, roles);
                }
                mesh.RecalculateBounds();
                parts.Add(new Part { mesh = mesh, materials = source.renderer.sharedMaterials });
            }
            model.parts = parts.ToArray();
            Debug.Log("Corrected " + (zombie ? "zombie" : "customer") + " skinned to the hotel animation rig (" +
                      model.parts.Sum(p => p.mesh.vertexCount) + " vertices).");
            return model;
        }

        static BoneWeight Single(int index) => new BoneWeight { boneIndex0 = index, weight0 = 1 };
        static BoneWeight Blend(int a, int b, float towardB)
        {
            float t = Mathf.Clamp01(towardB);
            return new BoneWeight { boneIndex0 = a, weight0 = 1 - t, boneIndex1 = b, weight1 = t };
        }
        static BoneWeight Weight(Vector3 p, byte role, bool zombie)
        {
            if (role == 1) return Single(11);
            if (role == 2) return Single(12);
            if (role == 3) return Single(13);
            if (role == 4) return Single(14);
            if (zombie) return ZombieWeight(p);
            if (p.y >= (zombie ? 1.00f : 1.18f))
                return Blend(1, 2, Mathf.InverseLerp(zombie ? .99f : 1.17f, zombie ? 1.12f : 1.28f, p.y));
            if (!zombie && role == 6 && p.y > .69f && p.y < 1.1f)
            {
                float across = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.16f, .42f, Mathf.Abs(p.x)));
                float shoulderFade = 1f - .75f * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.94f, 1.1f, p.y));
                return Blend(p.y < .84f ? 0 : 1, p.x < 0 ? 7 : 9, across * shoulderFade);
            }
            // The customer's shorts and shirt sides extend beyond x=.275.
            // Those panels must stay on the torso/hips when the arm reaches back.
            float armEdge = zombie ? .265f : role == 5 ? .28f : .355f;
            if (Mathf.Abs(p.x) > armEdge && p.y > .32f && p.y < (zombie ? 1.2f : role == 5 ? 1.0f : 1.08f))
            {
                bool left = p.x < 0;
                int upper = left ? 7 : 9, lower = left ? 8 : 10;
                if (p.y < .73f) return Blend(upper, lower, Mathf.InverseLerp(.79f, .59f, p.y));
                if (zombie)
                {
                    if (p.y < 1.04f) return Single(upper);
                    return Blend(1, upper, Mathf.InverseLerp(armEdge, armEdge + .09f, Mathf.Abs(p.x)));
                }
                if (p.y < .93f) return Single(upper);
                return Blend(1, upper, Mathf.InverseLerp(armEdge, armEdge + .13f, Mathf.Abs(p.x)));
            }
            if (p.y < .84f)
            {
                bool left = p.x < 0;
                int thigh = left ? 3 : 5, shin = left ? 4 : 6;
                if (p.y < .39f) return Single(shin);
                if (p.y < .52f) return Blend(shin, thigh, Mathf.InverseLerp(.39f, .52f, p.y));
                return Blend(thigh, 0, Mathf.InverseLerp(.73f, .88f, p.y));
            }
            return Blend(0, 1, Mathf.InverseLerp(.84f, 1.05f, p.y));
        }

        static BoneWeight ZombieWeight(Vector3 p)
        {
            if (p.y >= 1f)
                return Blend(1, 2, Mathf.InverseLerp(.99f, 1.12f, p.y));

            var body = ZombieBodyWeight(p);

            if (p.y > .30f)
            {
                // A continuous shoulder boundary keeps the torn sleeves and
                // exposed skin together when the arms reach or strike.
                float inner = p.y < .62f
                    ? Mathf.Lerp(.27f, .205f, Mathf.InverseLerp(.55f, .62f, p.y))
                    : p.y < .72f
                        ? Mathf.Lerp(.205f, .16f, Mathf.InverseLerp(.62f, .72f, p.y))
                        : Mathf.Lerp(.16f, .08f, Mathf.InverseLerp(.72f, .92f, p.y));
                float width = Mathf.Lerp(.035f, .18f, Mathf.InverseLerp(.72f, .94f, p.y));
                float arm = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(inner, inner + width, Mathf.Abs(p.x)));
                arm *= 1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.87f, .98f, p.y));
                if (arm > 0)
                {
                    int upper = p.x < 0 ? 7 : 9, lower = p.x < 0 ? 8 : 10;
                    float elbow = 1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.57f, .73f, p.y));
                    return FourWeights(upper, arm * (1f - elbow), lower, arm * elbow,
                        body.boneIndex0, (1f - arm) * body.weight0,
                        body.boneIndex1, (1f - arm) * body.weight1);
                }
            }
            return body;
        }

        static BoneWeight ZombieBodyWeight(Vector3 p)
        {
            if (p.y < .84f)
            {
                int thigh = p.x < 0 ? 3 : 5, shin = p.x < 0 ? 4 : 6;
                if (p.y < .39f) return Single(shin);
                if (p.y < .52f) return Blend(shin, thigh, Mathf.InverseLerp(.39f, .52f, p.y));
                return Blend(thigh, 0, Mathf.InverseLerp(.73f, .88f, p.y));
            }
            return Blend(0, 1, Mathf.InverseLerp(.84f, 1.05f, p.y));
        }

        static BoneWeight FourWeights(int a, float wa, int b, float wb, int c, float wc, int d, float wd)
        {
            // Unity expects influences in descending order, including at the
            // shoulder where torso, hips, upper arm and forearm can all meet.
            SortPair(ref a, ref wa, ref b, ref wb);
            SortPair(ref c, ref wc, ref d, ref wd);
            SortPair(ref a, ref wa, ref c, ref wc);
            SortPair(ref b, ref wb, ref d, ref wd);
            SortPair(ref b, ref wb, ref c, ref wc);
            return new BoneWeight
            {
                boneIndex0 = a, weight0 = wa, boneIndex1 = b, weight1 = wb,
                boneIndex2 = c, weight2 = wc, boneIndex3 = d, weight3 = wd
            };
        }

        static void SortPair(ref int a, ref float wa, ref int b, ref float wb)
        {
            if (wa >= wb) return;
            int index = a; a = b; b = index;
            float weight = wa; wa = wb; wb = weight;
        }
    }
}
