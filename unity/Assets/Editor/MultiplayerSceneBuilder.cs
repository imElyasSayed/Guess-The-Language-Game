using AccentGuesser.Net;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AccentGuesser.EditorTools
{
    /// <summary>
    /// One-click builder for the multiplayer test scene. Creates the NetworkManager (with
    /// UnityTransport wired) and the Match object (NetworkObject + MatchNetworkBehaviour +
    /// NetworkBootstrap), saves it to Assets/Scenes/Multiplayer.unity, and adds it to Build
    /// Settings. Done through Unity's API so component references are always valid — no manual
    /// scene wiring, no hand-authored YAML.
    ///
    /// Menu: Say Again ▸ Build Multiplayer Scene.
    /// </summary>
    public static class MultiplayerSceneBuilder
    {
        private const string ScenesFolder = "Assets/Scenes";
        private const string ScenePath = ScenesFolder + "/Multiplayer.unity";

        [MenuItem("Say Again/Build Multiplayer Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // --- NetworkManager + transport ---
            var nmGo = new GameObject("NetworkManager");
            var nm = nmGo.AddComponent<NetworkManager>();
            var utp = nmGo.AddComponent<UnityTransport>();
            nm.NetworkConfig ??= new NetworkConfig();
            nm.NetworkConfig.NetworkTransport = utp;

            // --- Match: rules + lobby/HUD ---
            var matchGo = new GameObject("Match");
            matchGo.AddComponent<NetworkObject>();
            matchGo.AddComponent<MatchNetworkBehaviour>();
            matchGo.AddComponent<NetworkBootstrap>();

            // --- Save + register in Build Settings ---
            if (!AssetDatabase.IsValidFolder(ScenesFolder))
                AssetDatabase.CreateFolder("Assets", "Scenes");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            EditorUtility.DisplayDialog(
                "Multiplayer scene ready",
                "Created and opened Assets/Scenes/Multiplayer.unity with NetworkManager + Match.\n\n" +
                "Press Play, then use the on-screen lobby to Host or Join.",
                "Nice");

            Debug.Log("[Say Again] Built " + ScenePath);
        }
    }
}
