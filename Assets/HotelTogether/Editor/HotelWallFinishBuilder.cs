using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEditor;
using Object = UnityEngine.Object;

namespace HotelTogether.Editor
{
    // Saved URP materials and UV meshes; no runtime generator or extra colliders.
    public static class HotelWallFinishBuilder
    {
        const string Art = "Assets/HotelTogether/Art/WallFinishes/";
        const string Prefix = "Finish - ";
        static string OriginalName(Material m) => m.name.StartsWith(Prefix) ? m.name.Substring(Prefix.Length) : m.name;
        static string Kind(Material material, MeshRenderer renderer)
        {
            string name = OriginalName(material);
            if (new[]{"Honey cream","Warm plaster","Upstairs ivory plaster","Stair ivory guard","Elevator ivory","Terracotta accents"}.Contains(name)) return "Limewash";
            if (new[]{"Deep teal panels","Upstairs deep teal panels","Teal structure","Elevator teal","Stair teal wall panel","Stair floor 1 accent","Stair floor 2 accent","Stair floor 3 accent","Cafe deep teal"}.Contains(name)) return "Paint";
            if (new[]{"Honey wood","Cafe walnut","Bathroom walnut","Elevator warm walnut","Rooftop honey timber","Rooftop warm timber"}.Contains(name)) return "Walnut";
            // The porcelain material is also used on fixtures: finish only the walls.
            if (name == "Ivory porcelain" && renderer.name.ToLowerInvariant().Contains("wall")) return "Limestone";
            return null;
        }
        static Texture2D Texture(string kind, string channel)
        {
            string path = Art + "Textures/" + kind + " " + channel + ".png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = channel == "Normal" ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = channel == "Albedo";
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 8;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.alphaSource = channel == "Roughness" ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        static Material Finish(Material source, string kind, Dictionary<string, Texture2D[]> textures)
        {
            string name = OriginalName(source);
            string path = Art + Prefix + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.color = name == "Deep teal panels" ? new Color(.075f,.225f,.235f) : source.color;
            m.SetTexture("_BaseMap", textures[kind][0]);
            m.SetTexture("_BumpMap", textures[kind][1]);
            m.SetTexture("_MetallicGlossMap", textures[kind][2]);
            m.SetFloat("_BumpScale", kind == "Limewash" ? .85f : .65f);
            m.SetFloat("_Metallic", 0);
            m.SetFloat("_Smoothness", 1);
            m.SetFloat("_SmoothnessTextureChannel", 0);
            m.SetTextureScale("_BaseMap", Vector2.one);
            m.SetTextureOffset("_BaseMap", Vector2.zero);
            m.EnableKeyword("_NORMALMAP");
            m.EnableKeyword("_METALLICSPECGLOSSMAP");
            m.DisableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", Color.black);
            EditorUtility.SetDirty(m);
            return m;
        }
        static void MapMesh(MeshRenderer renderer, HashSet<int> slots)
        {
            var filter = renderer.GetComponent<MeshFilter>();
            var original = filter.sharedMesh;
            if (!original || original.vertexCount == 0) return;
            var mesh = Object.Instantiate(original);
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            if (normals.Length != vertices.Length) { mesh.RecalculateNormals(); normals = mesh.normals; }
            var uv = mesh.uv;
            if (uv.Length != vertices.Length) uv = new Vector2[vertices.Length];
            var selected = new HashSet<int>();
            foreach (int slot in slots) if (slot < mesh.subMeshCount) foreach (int vertex in mesh.GetIndices(slot)) selected.Add(vertex);
            // Object-relative, metre-scaled projection follows moving cabins and never stretches on scaled walls.
            var normalMatrix = renderer.transform.localToWorldMatrix.inverse.transpose;
            foreach (int i in selected)
            {
                var p = renderer.transform.TransformVector(vertices[i]);
                var n = normalMatrix.MultiplyVector(normals[i]).normalized;
                Vector2 projected;
                if (Mathf.Abs(n.y) > Mathf.Abs(n.x) && Mathf.Abs(n.y) > Mathf.Abs(n.z)) projected = new Vector2(p.x, p.z);
                else if (Mathf.Abs(n.x) > Mathf.Abs(n.z)) projected = new Vector2(p.z, p.y);
                else projected = new Vector2(p.x, p.y);
                uv[i] = projected / 1.6f;
            }
            mesh.uv = uv;
            mesh.RecalculateTangents();
            string identity = GlobalObjectId.GetGlobalObjectIdSlow(renderer).ToString();
            string hash;
            using (var md5 = MD5.Create()) hash = BitConverter.ToString(md5.ComputeHash(Encoding.UTF8.GetBytes(identity))).Replace("-", "");
            string path = Art + "Meshes/Wall " + hash + ".asset";
            mesh.name = "Metre mapped " + renderer.name;
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing) { EditorUtility.CopySerialized(mesh, existing); Object.DestroyImmediate(mesh); mesh = existing; }
            else AssetDatabase.CreateAsset(mesh, path);
            filter.sharedMesh = mesh;
            EditorUtility.SetDirty(filter);
        }
        [MenuItem("Hotel Together/Art/Apply textured wall finishes")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.name != "HotelLobby") throw new InvalidOperationException("Open HotelLobby first.");
            Directory.CreateDirectory(Art + "Meshes");
            AssetDatabase.Refresh();
            var textures = new Dictionary<string, Texture2D[]>();
            foreach (string kind in new[]{"Limewash","Paint","Walnut","Limestone"}) textures[kind] = new[]{Texture(kind,"Albedo"),Texture(kind,"Normal"),Texture(kind,"Roughness")};
            int count = 0;
            var materials = new Dictionary<string, Material>();
            var renderers = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshRenderer>(true)).Where(r => r.GetComponent<MeshFilter>() && !r.GetComponentInParent<HotelPlayer>() && !r.GetComponentInParent<HotelGuestBot>()).ToArray();
            foreach (var renderer in renderers)
            {
                var shared = renderer.sharedMaterials;
                var slots = new HashSet<int>();
                for (int i = 0; i < shared.Length; i++)
                {
                    if (!shared[i]) continue;
                    string kind = Kind(shared[i], renderer);
                    if (kind == null) continue;
                    string key = OriginalName(shared[i]) + kind;
                    if (!materials.TryGetValue(key, out var finish)) materials[key] = finish = Finish(shared[i], kind, textures);
                    shared[i] = finish;
                    slots.Add(i);
                }
                if (slots.Count == 0) continue;
                MapMesh(renderer, slots);
                renderer.sharedMaterials = shared;
                EditorUtility.SetDirty(renderer);
                count++;
            }
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            HotelEmployeeBuilder.Save();
            Debug.Log("WALL_FINISHES_APPLIED: " + count + " renderers, " + materials.Count + " finishes.");
        }
    }
}
