using AccentGuesser.App;
using AccentGuesser.Net;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AccentGuesser.EditorTools
{
    /// <summary>
    /// One-click builder for the MAIN MENU scene: a deliberately blank, page-dark screen (no 3D —
    /// the tavern is only ever seen after a choice is made and Tavern.unity loads) holding the
    /// networking objects the menu lobby needs (NetworkManager + Match) and the
    /// <see cref="MainMenuController"/> that builds and drives the card UI at runtime.
    ///
    /// Menu: Say Again ▸ Build Main Menu Scene. Saves Assets/Scenes/MainMenu.unity and pins the
    /// Build Settings order to MainMenu first, Tavern second (NetworkSceneManager needs both).
    ///
    /// Fully programmatic like <see cref="TavernSceneBuilder"/> so the scene is regenerable —
    /// never hand-edit MainMenu.unity.
    /// </summary>
    public static class MenuSceneBuilder
    {
        private const string ScenesFolder = "Assets/Scenes";
        private const string ScenePath = ScenesFolder + "/MainMenu.unity";
        private const string TavernScenePath = ScenesFolder + "/Tavern.unity";

        /// <summary>The design's page background (#0c0a08) — all the camera ever shows.</summary>
        private static readonly Color PageDark = new Color(0.047f, 0.039f, 0.031f, 1f);

        [MenuItem("Say Again/Build Main Menu Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("MainMenu");

            // --- Camera: nothing to frame, just the solid page-dark clear color ---- #
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(root.transform);
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = PageDark;
            cam.cullingMask = 0; // renders no scene geometry, ever
            camGo.AddComponent<AudioListener>();

            // --- Networking starts in the menu ------------------------------------ #
            // NetworkManager persists across the scene switch (Tavern's duplicate self-destructs);
            // the Match here runs the menu lobby only — the tavern's own Match takes over on load.
            var nmGo = new GameObject("NetworkManager");
            var nm = nmGo.AddComponent<NetworkManager>();
            var utp = nmGo.AddComponent<UnityTransport>();
            nm.NetworkConfig ??= new NetworkConfig();
            nm.NetworkConfig.NetworkTransport = utp;

            var matchGo = new GameObject("Match");
            matchGo.AddComponent<NetworkObject>();
            var match = matchGo.AddComponent<MatchNetworkBehaviour>();
            var so = new SerializedObject(match);
            so.FindProperty("_lobby").boolValue = true; // waiting room; never auto-deal here
            so.ApplyModifiedPropertiesWithoutUndo();

            // --- The menu itself (card UI is built at runtime by the controller) --- #
            var menuGo = new GameObject("Menu");
            menuGo.transform.SetParent(root.transform);
            menuGo.AddComponent<MainMenuController>();

            // --- Save + pin Build Settings order (menu first, tavern second) ------- #
            if (!AssetDatabase.IsValidFolder(ScenesFolder))
                AssetDatabase.CreateFolder("Assets", "Scenes");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            PinSceneOrder();

            if (!EditorPrefs.GetBool("SayAgain.SuppressDialogs", false))
                EditorUtility.DisplayDialog(
                    "Main menu ready",
                    "Built and opened Assets/Scenes/MainMenu.unity:\n" +
                    "• Blank page-dark screen with the left card UI\n" +
                    "• Networking starts here; the tavern only appears after a choice\n\n" +
                    "Build Settings order pinned: MainMenu, Tavern.",
                    "Nice");
            Debug.Log("[Say Again] Built " + ScenePath);
        }

        /// <summary>MainMenu must be scene 0 and Tavern scene 1; anything else keeps its slot after.</summary>
        private static void PinSceneOrder()
        {
            var ordered = new System.Collections.Generic.List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(ScenePath, true),
                new EditorBuildSettingsScene(TavernScenePath, true)
            };
            foreach (var s in EditorBuildSettings.scenes)
                if (s.path != ScenePath && s.path != TavernScenePath)
                    ordered.Add(s);
            EditorBuildSettings.scenes = ordered.ToArray();
        }
    }
}
