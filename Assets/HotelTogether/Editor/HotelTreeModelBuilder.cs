using System;
using UnityEditor;
using UnityEngine;

public static class HotelTreeModelBuilder
{
    const string SourcePath = "Assets/HotelTogether/Models/Environment/Trees/CartoonTree.glb";
    public const string PrefabPath = "Assets/HotelTogether/Resources/HotelTogetherEnvironment/CartoonTree.prefab";

    [MenuItem("Hotel Together/Environment/Prepare cartoon tree model")]
    public static void Prepare()
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
        if (!source) throw new InvalidOperationException("The supplied cartoon tree GLB was not imported.");

        var root = new GameObject("Cartoon tree");
        try
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(source);
            model.transform.SetParent(root.transform, false);
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException("The cartoon tree has no renderers.");

            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            const float targetHeight = 6f;
            float scale = targetHeight / bounds.size.y;
            // Keep the imported model's own rotation and scale; only normalize its height and pivot.
            model.transform.localScale *= scale;
            model.transform.localPosition = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z) * scale;
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
        AssetDatabase.SaveAssets();
    }
}
