using System;
using System.IO;
using AccentGuesser.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AccentGuesser.EditorTools
{
    /// <summary>
    /// One-click builder for the 3D tavern lobby/game room. Instantiates the
    /// Blender-generated FBX assets (Assets/Art/*), lays out the circular table with
    /// exactly four seats + players around it, drops the announcer behind the bar,
    /// wires warm point lighting from the candle/fire/lantern manifest, frames a
    /// camera on all four players, and saves Assets/Scenes/Tavern.unity.
    ///
    /// Menu: Say Again ▸ Build 3D Tavern Scene.
    ///
    /// Everything is authored through Unity's API so references are always valid.
    /// Re-run any time you regenerate the art (blender/build_all.py).
    /// </summary>
    public static class TavernSceneBuilder
    {
        private const string ScenesFolder = "Assets/Scenes";
        private const string ScenePath = ScenesFolder + "/Tavern.unity";
        private const string EnvDir = "Assets/Art/Env";
        private const string CharDir = "Assets/Art/Characters/generated";
        private const string LightsJson = EnvDir + "/tavern_lights.json";

        private const float SeatRadius = 2.4f;     // stool ring radius (metres)
        private static readonly float[] SeatAngles = { 235f, 305f, 25f, 155f };
        private static readonly string[] PlayerModels =
            { "P1_Sphere", "P2_Giraffe", "P3_Boxer", "P4_Slice" };

        [MenuItem("Say Again/Build 3D Tavern Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Tavern");

            // --- Environment ------------------------------------------------ #
            Instantiate("TavernRoom", EnvDir, Vector3.zero, Quaternion.identity, root.transform);
            Instantiate("Table", EnvDir, Vector3.zero, Quaternion.identity, root.transform);

            // --- Table + exactly four seats -------------------------------- #
            var seats = new GameObject("Seats");
            seats.transform.SetParent(root.transform);
            for (int i = 0; i < 4; i++)
            {
                float rad = SeatAngles[i] * Mathf.Deg2Rad;
                var seatPos = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * SeatRadius;
                var faceCentre = Quaternion.LookRotation(FlatDir(Vector3.zero - seatPos));

                Instantiate("Stool", EnvDir, seatPos, faceCentre, seats.transform);
                // player stands just outside the stool so the stool reads as "their seat"
                var playerPos = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * (SeatRadius + 0.25f);
                var player = Instantiate(PlayerModels[i], CharDir, playerPos, faceCentre, seats.transform);

                if (PlayerModels[i] == "P2_Giraffe" && player != null)
                    WireBadBreath(player);
            }

            // --- Announcer behind the bar (-X wall) ------------------------ #
            var hostPos = new Vector3(-3.8f, 0f, -0.5f);
            Instantiate("Announcer_Host", CharDir, hostPos,
                Quaternion.LookRotation(FlatDir(Vector3.zero - hostPos)), root.transform);

            // --- Lighting --------------------------------------------------- #
            BuildLighting(root.transform);

            // --- Camera framing all four players --------------------------- #
            BuildCamera(root.transform);

            // --- Save + register in Build Settings ------------------------- #
            if (!AssetDatabase.IsValidFolder(ScenesFolder))
                AssetDatabase.CreateFolder("Assets", "Scenes");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            RegisterScene(ScenePath);

            EditorUtility.DisplayDialog(
                "3D Tavern ready",
                "Built and opened Assets/Scenes/Tavern.unity:\n" +
                "• Cozy tavern room, circular table, 4 seats + players\n" +
                "• Announcer behind the bar\n" +
                "• Warm candle/fire/lantern point lights\n" +
                "• Camera framing all four players\n\n" +
                "Press B in Play mode to toggle the giraffe's bad breath.\n\n" +
                "If models look untextured: select the FBXs, Materials tab, " +
                "set Material Creation Mode to 'Standard' and Extract Materials.",
                "Nice");
            Debug.Log("[Say Again] Built " + ScenePath);
        }

        // ------------------------------------------------------------------ #
        private static GameObject Instantiate(string name, string dir, Vector3 pos,
                                              Quaternion rot, Transform parent)
        {
            var path = $"{dir}/{name}.fbx";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning($"[Say Again] Missing asset: {path} — run blender/build_all.py first.");
                return null;
            }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name;
            go.transform.SetPositionAndRotation(pos, rot);
            return go;
        }

        private static Vector3 FlatDir(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude < 1e-4f ? Vector3.forward : v.normalized;
        }

        private static void WireBadBreath(GameObject giraffe)
        {
            var toggle = giraffe.AddComponent<BadBreathToggle>();
            var breath = FindDeep(giraffe.transform, "BadBreath");
            if (breath != null)
            {
                toggle.breathMesh = breath.gameObject;
                breath.gameObject.SetActive(false); // off by default; press B to toggle
            }
        }

        private static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t)
            {
                var r = FindDeep(c, name);
                if (r != null) return r;
            }
            return null;
        }

        // ------------------------------------------------------------------ #
        private static void BuildLighting(Transform parent)
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.20f, 0.15f, 0.11f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.06f, 0.04f, 0.03f);
            RenderSettings.fogDensity = 0.012f;

            var lights = new GameObject("Lights");
            lights.transform.SetParent(parent);

            // dim warm fill so shadows aren't pure black
            var fillGo = new GameObject("WarmFill");
            fillGo.transform.SetParent(lights.transform);
            fillGo.transform.rotation = Quaternion.Euler(55f, -30f, 0f);
            var fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(1f, 0.72f, 0.45f);
            fill.intensity = 0.55f;
            fill.shadows = LightShadows.Soft;

            foreach (var d in LoadLights())
                MakePoint(lights.transform, d, 1.6f);

            // cozy key glow above the table centre (also keeps the middle readable for UI)
            MakePoint(lights.transform, new LightDef
            {
                pos = new[] { 0f, 2.4f, 0f },
                color = new[] { 1f, 0.70f, 0.38f },
                intensity = 4.5f, range = 8f
            }, 1f);
            // soft front fill so seated players' faces aren't lost to shadow
            MakePoint(lights.transform, new LightDef
            {
                pos = new[] { 0f, 2.2f, -3.2f },
                color = new[] { 1f, 0.80f, 0.55f },
                intensity = 2.5f, range = 7f
            }, 1f);
        }

        private static void MakePoint(Transform parent, LightDef d, float intensityScale = 1f)
        {
            var go = new GameObject("Point");
            go.transform.SetParent(parent);
            go.transform.position = new Vector3(d.pos[0], d.pos[1], d.pos[2]);
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(d.color[0], d.color[1], d.color[2]);
            l.intensity = d.intensity * intensityScale;
            l.range = d.range;
            l.shadows = LightShadows.None; // many small lights → keep cheap
        }

        private static LightDef[] LoadLights()
        {
            var abs = Path.Combine(Application.dataPath, "..", LightsJson);
            if (!File.Exists(abs))
            {
                Debug.LogWarning($"[Say Again] {LightsJson} not found — using fallback lights only.");
                return Array.Empty<LightDef>();
            }
            var set = JsonUtility.FromJson<LightSet>(File.ReadAllText(abs));
            return set?.lights ?? Array.Empty<LightDef>();
        }

        // ------------------------------------------------------------------ #
        private static void BuildCamera(Transform parent)
        {
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(parent);
            // High 3/4 overview: frames all four players and keeps the table centre
            // readable for future UI, looking in over the open front / roofless top.
            camGo.transform.position = new Vector3(6.2f, 5.6f, -7.4f);
            camGo.transform.rotation = Quaternion.LookRotation(
                (new Vector3(0f, 0.7f, -0.2f) - camGo.transform.position).normalized);
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 46f;
            cam.backgroundColor = new Color(0.03f, 0.025f, 0.02f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 60f;
            camGo.AddComponent<AudioListener>();
        }

        private static void RegisterScene(string path)
        {
            var existing = EditorBuildSettings.scenes;
            foreach (var s in existing)
                if (s.path == path) return;
            var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(existing)
            {
                new EditorBuildSettingsScene(path, true)
            };
            EditorBuildSettings.scenes = list.ToArray();
        }

        [Serializable] private class LightDef
        {
            public float[] pos;
            public float[] color;
            public float intensity;
            public float range;
        }

        [Serializable] private class LightSet { public LightDef[] lights; }
    }
}
