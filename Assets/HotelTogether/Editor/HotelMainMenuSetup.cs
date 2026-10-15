using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HotelTogether.EditorTools
{
    /// <summary>
    /// Keeps the main menu wired into the project:
    /// - menu art in Resources/HotelTogetherMenu imports with real transparency (no halos, no NPOT stretching);
    /// - Assets/HotelTogether/Scenes/HotelMainMenu.unity exists (created once, never overwritten automatically);
    /// - Build Settings start with HotelMainMenu, followed by HotelLobby.
    /// </summary>
    [InitializeOnLoad]
    public static class HotelMainMenuSetup
    {
        public const string ScenePath = "Assets/HotelTogether/Scenes/HotelMainMenu.unity";
        public const string LobbyPath = "Assets/HotelTogether/Scenes/HotelLobby.unity";
        public const string ArtFolder = "Assets/HotelTogether/Resources/HotelTogetherMenu";
        const string PlayFromMenuPref = "HotelTogether.PlayFromMainMenu";

        static HotelMainMenuSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                ConfigureArt();
                if (!File.Exists(ScenePath)) CreateScene(false);
                EnsureBuildSettings();
                ApplyPlayModeStartScene();
            };
        }

        // ------------------------------------------------------------ art import
        public static bool Configure(TextureImporter importer, bool hasAlpha)
        {
            bool changed = false;
            void Set<T>(T current, T wanted, System.Action<T> apply) { if (!EqualityComparer<T>.Default.Equals(current, wanted)) { apply(wanted); changed = true; } }
            Set(importer.textureType, TextureImporterType.Default, v => importer.textureType = v);
            Set(importer.sRGBTexture, true, v => importer.sRGBTexture = v);
            Set(importer.alphaSource, hasAlpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None, v => importer.alphaSource = v);
            Set(importer.alphaIsTransparency, hasAlpha, v => importer.alphaIsTransparency = v);
            Set(importer.npotScale, TextureImporterNPOTScale.None, v => importer.npotScale = v);
            Set(importer.mipmapEnabled, true, v => importer.mipmapEnabled = v);
            Set(importer.wrapMode, TextureWrapMode.Clamp, v => importer.wrapMode = v);
            Set(importer.filterMode, FilterMode.Trilinear, v => importer.filterMode = v);
            Set(importer.maxTextureSize, 2048, v => importer.maxTextureSize = v);
            Set(importer.textureCompression, TextureImporterCompression.CompressedHQ, v => importer.textureCompression = v);
            Set(importer.isReadable, false, v => importer.isReadable = v);
            return changed;
        }

        public static bool IsMenuArt(string path) => path.Replace('\\', '/').StartsWith(ArtFolder + "/") && path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase);
        public static bool HasAlpha(string path) => !Path.GetFileNameWithoutExtension(path).Equals("background", System.StringComparison.OrdinalIgnoreCase);

        static void ConfigureArt()
        {
            if (!AssetDatabase.IsValidFolder(ArtFolder)) return;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ArtFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!IsMenuArt(path) || !(AssetImporter.GetAtPath(path) is TextureImporter importer)) continue;
                if (Configure(importer, HasAlpha(path))) importer.SaveAndReimport();
            }
        }

        // ------------------------------------------------------------ scene
        [MenuItem("Hotel Together/Main Menu/Open Main Menu Scene", priority = 0)]
        static void OpenScene()
        {
            if (!File.Exists(ScenePath)) CreateScene(true);
            if (File.Exists(ScenePath) && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Hotel Together/Main Menu/Rebuild Main Menu Scene", priority = 1)]
        static void RebuildScene()
        {
            if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("Rebuild main menu scene", "Replace HotelMainMenu.unity with a fresh copy? Inspector changes made to its Main Menu object will be lost.", "Rebuild", "Cancel")) return;
            ConfigureArt();
            CreateScene(true);
            EnsureBuildSettings();
        }

        static void CreateScene(bool interactive)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var open = SceneManager.GetSceneAt(i);
                if (open.path == ScenePath) { if (interactive) Debug.LogWarning("Close HotelMainMenu before rebuilding it."); return; }
                if (string.IsNullOrEmpty(open.path))
                {
                    // Unity cannot add a scene next to an unsaved Untitled scene.
                    Debug.LogWarning("Hotel Together: save the open Untitled scene, then run Hotel Together > Main Menu > Rebuild Main Menu Scene.");
                    return;
                }
            }
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var camera = new GameObject("Menu Camera");
            var cam = camera.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black; cam.cullingMask = 0; cam.tag = "MainCamera";
            camera.AddComponent<AudioListener>();
            SceneManager.MoveGameObjectToScene(camera, scene);
            var menu = new GameObject("Main Menu");
            menu.AddComponent<HotelMainMenu>();
            SceneManager.MoveGameObjectToScene(menu, scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorSceneManager.CloseScene(scene, true);
            AssetDatabase.Refresh();
            Debug.Log("Hotel Together: created " + ScenePath);
        }

        static void EnsureBuildSettings()
        {
            if (!File.Exists(ScenePath)) return;
            var scenes = EditorBuildSettings.scenes.ToList();
            bool changed = false;
            int menuIndex = scenes.FindIndex(s => s.path == ScenePath);
            if (menuIndex != 0 || !scenes[0].enabled)
            {
                if (menuIndex >= 0) scenes.RemoveAt(menuIndex);
                scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
                changed = true;
            }
            if (File.Exists(LobbyPath) && !scenes.Any(s => s.path == LobbyPath)) { scenes.Add(new EditorBuildSettingsScene(LobbyPath, true)); changed = true; }
            int lobby = scenes.FindIndex(s => s.path == LobbyPath);
            if (lobby >= 0 && !scenes[lobby].enabled) { scenes[lobby] = new EditorBuildSettingsScene(LobbyPath, true); changed = true; }
            if (changed) { EditorBuildSettings.scenes = scenes.ToArray(); Debug.Log("Hotel Together: Build Settings now start with the main menu."); }
        }

        // ------------------------------------------------------------ play mode
        [MenuItem("Hotel Together/Main Menu/Start Play Mode From Main Menu", priority = 20)]
        static void TogglePlayFromMenu()
        {
            EditorPrefs.SetBool(PlayFromMenuPref, !EditorPrefs.GetBool(PlayFromMenuPref, true));
            ApplyPlayModeStartScene();
        }

        [MenuItem("Hotel Together/Main Menu/Start Play Mode From Main Menu", true)]
        static bool TogglePlayFromMenuValidate()
        {
            Menu.SetChecked("Hotel Together/Main Menu/Start Play Mode From Main Menu", EditorPrefs.GetBool(PlayFromMenuPref, true));
            return File.Exists(ScenePath);
        }

        static void ApplyPlayModeStartScene()
        {
            EditorSceneManager.playModeStartScene = EditorPrefs.GetBool(PlayFromMenuPref, true) ? AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) : null;
        }
    }

    /// <summary>Applies the menu art import settings the first time the PNGs are imported.</summary>
    sealed class HotelMainMenuArtImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!HotelMainMenuSetup.IsMenuArt(assetPath)) return;
            HotelMainMenuSetup.Configure((TextureImporter)assetImporter, HotelMainMenuSetup.HasAlpha(assetPath));
        }
    }
}
