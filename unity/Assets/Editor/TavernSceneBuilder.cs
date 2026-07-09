using System;
using System.IO;
using AccentGuesser.App;
using AccentGuesser.Net;
using AccentGuesser.World;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
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
        internal const string EnvDir = "Assets/Art/Env";
        internal const string CharDir = "Assets/Art/Characters/generated";
        private const string LightsJson = EnvDir + "/tavern_lights.json";

        // Liar's Bar-style staging: four empty stools in an arc facing the penguin announcer
        // behind the bar (layout from TavernSeating, the single source of truth shared with the
        // runtime camera), plus a five-animal showroom lineup by the hearth that TavernStage
        // clones onto the seats once avatars are picked.

        [MenuItem("Say Again/Build 3D Tavern Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Tavern");

            // --- Environment ------------------------------------------------ #
            Instantiate("TavernRoom", EnvDir, Vector3.zero, Quaternion.identity, root.transform);
            Instantiate("Table", EnvDir, Vector3.zero, Quaternion.identity, root.transform);

            // --- Empty stools at the player arc (avatars are cloned in at match start) - #
            var seats = new GameObject("Seats");
            seats.transform.SetParent(root.transform);
            for (int i = 0; i < TavernSeating.SeatCount; i++)
            {
                var seatPos = TavernSeating.SeatPosition(i);
                var facing = TavernSeating.SeatFacing(i);
                Instantiate("Stool", EnvDir, seatPos - facing * 0.35f,
                    Quaternion.LookRotation(facing), seats.transform);
            }

            // --- The showroom lineup: all five pickable animals by the hearth ------- #
            // TavernStage clones the CHOSEN animal onto each seat at match start (clones,
            // so two players may both be the giraffe). Order matches the HUD's buttons
            // left→right as seen from the selection camera (screen-left = -Z).
            var lineup = new GameObject("AvatarLineup");
            lineup.transform.SetParent(root.transform);
            string[] lineupModels = { "P1_Bulldog", "P2_Giraffe", "P3_Horse", "P4_Fox", "P5_Cat" };
            for (int i = 0; i < lineupModels.Length; i++)
            {
                var pos = new Vector3(-3.1f, 0f, -1.8f + i * 0.9f);
                var model = Instantiate(lineupModels[i], CharDir, pos,
                    CharacterFacing(Vector3.right), lineup.transform);
                if (model == null) continue;
                AddIdle(model, i * 1.1f);
                if (lineupModels[i] == "P2_Giraffe") WireBadBreath(model);
            }

            // --- Announcer behind the bar counter facing the room ------------------- #
            var hostPos = TavernSeating.AnnouncerPos;
            var host = Instantiate("Announcer_Host", CharDir, hostPos,
                CharacterFacing(FlatDir(Vector3.zero - hostPos)), root.transform);
            if (host != null) AddIdle(host, 0.6f);

            // --- Ceiling (the Blender room is roofless for the old top-down cam;
            //     first-person looks across the room, so cap the black void) --- #
            BuildCeiling(root.transform);

            // --- Lighting --------------------------------------------------- #
            BuildLighting(root.transform);

            // --- Camera framing all four players --------------------------- #
            BuildCamera(root.transform);

            // --- Single-player game presenter (drives the round loop here) - #
            BuildGamePresenter(root.transform);

            // --- Save + register in Build Settings ------------------------- #
            if (!AssetDatabase.IsValidFolder(ScenesFolder))
                AssetDatabase.CreateFolder("Assets", "Scenes");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            RegisterScene(ScenePath);

            // Suppressible for scripted/MCP-driven rebuilds (a modal would block automation):
            //   EditorPrefs.SetBool("SayAgain.SuppressDialogs", true)
            if (!EditorPrefs.GetBool("SayAgain.SuppressDialogs", false))
                EditorUtility.DisplayDialog(
                "3D Tavern ready",
                "Built and opened Assets/Scenes/Tavern.unity:\n" +
                "• Cozy tavern room, circular table, 4 seats\n" +
                "• Animal cast: bulldog, giraffe, horse, fox at the table,\n" +
                "  cat chatting with the penguin announcer at the bar\n" +
                "• Warm candle/fire/lantern point lights + framing camera\n" +
                "• Single-player game presenter — press Play to deal a round\n\n" +
                "If models look untextured: select the FBXs, Materials tab, " +
                "set Material Creation Mode to 'Standard' and Extract Materials.",
                "Nice");
            Debug.Log("[Say Again] Built " + ScenePath);
        }

        // ------------------------------------------------------------------ #
        internal static GameObject Instantiate(string name, string dir, Vector3 pos,
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

        internal static Vector3 FlatDir(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude < 1e-4f ? Vector3.forward : v.normalized;
        }

        /// <summary>
        /// Rotation that makes a Meshy character visually face <paramref name="dir"/>. The
        /// GLB→FBX pipeline bakes the models with their faces toward local -Z (Blender's forward
        /// convention), so LookRotation alone leaves them facing backwards — add a 180° yaw.
        /// </summary>
        internal static Quaternion CharacterFacing(Vector3 dir) =>
            Quaternion.LookRotation(FlatDir(dir)) * Quaternion.Euler(0f, 180f, 0f);

        internal static void BuildCeiling(Transform parent)
        {
            var wood = new Material(Shader.Find("Standard"))
            {
                name = "CeilingWood",
                color = new Color(0.16f, 0.10f, 0.06f) // dark rafter wood
            };
            wood.SetFloat("_Glossiness", 0.05f);

            MakeSlab(parent, "Ceiling", wood, new Vector3(0f, 3.25f, 0f), new Vector3(12f, 0.15f, 12f));

            // The Blender room's ±Z sides are only half-height (open for the old exterior camera);
            // first-person looks straight at them, so cap the black void with full wall panels.
            var wall = new Material(Shader.Find("Standard"))
            {
                name = "WallWood",
                color = new Color(0.23f, 0.15f, 0.09f)
            };
            wall.SetFloat("_Glossiness", 0.05f);
            MakeSlab(parent, "WallNorth", wall, new Vector3(0f, 1.7f, 4.66f), new Vector3(11.6f, 3.5f, 0.2f));
            MakeSlab(parent, "WallSouth", wall, new Vector3(0f, 1.7f, -4.66f), new Vector3(11.6f, 3.5f, 0.2f));
        }

        private static void MakeSlab(Transform parent, string name, Material mat, Vector3 pos, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent);
            go.transform.position = pos;
            go.transform.localScale = scale;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            // Must not shadow the room from the warm directional fill above.
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        internal static void AddIdle(GameObject character, float phase)
        {
            var idle = character.AddComponent<TavernIdle>();
            idle.phase = phase;
        }

        private static void WireBadBreath(GameObject giraffe)
        {
            var toggle = giraffe.AddComponent<BadBreathToggle>();
            // No hotkey: the raw key fired even while typing in the HUD's input fields (and,
            // in first-person AS the giraffe, blasted the puff straight into the camera).
            // The puff is game-driven now — TavernStage fires it on wrong answers.
            toggle.toggleKey = KeyCode.None;
            var breath = FindDeep(giraffe.transform, "BadBreath");
            if (breath != null)
            {
                toggle.breathMesh = breath.gameObject;
                breath.gameObject.SetActive(false); // off by default
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
        private static void BuildGamePresenter(Transform parent)
        {
            // Netcode plumbing (idle until the player hosts or joins; solo never starts it):
            // NetworkManager + transport, and the Match object owning the host-authoritative
            // rules seam. Mirrors MultiplayerSceneBuilder so one scene serves solo AND online.
            var nmGo = new GameObject("NetworkManager");
            var nm = nmGo.AddComponent<NetworkManager>();
            var utp = nmGo.AddComponent<UnityTransport>();
            nm.NetworkConfig ??= new NetworkConfig();
            nm.NetworkConfig.NetworkTransport = utp;

            var matchGo = new GameObject("Match");
            matchGo.AddComponent<NetworkObject>();
            var match = matchGo.AddComponent<MatchNetworkBehaviour>();

            // The tavern uses the lobby flow (wait → host starts → avatar select → rounds);
            // the legacy IMGUI test scene keeps auto-start, so this is set per-scene here.
            var so = new SerializedObject(match);
            so.FindProperty("_lobby").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();

            // The tavern's front door: builds the shared HUD, offers Solo / Host / Join, and
            // boots the right presenter. Kept in the scene via the builder so Tavern.unity
            // stays fully regenerable (no hand-edited YAML).
            var go = new GameObject("Game");
            go.transform.SetParent(parent);
            go.AddComponent<TavernBootstrap>();
        }

        // ------------------------------------------------------------------ #
        internal static void BuildLighting(Transform parent)
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

            // cozy key glow above the table centre — kept gentle: the first-person camera sits
            // right at the table edge and a hot light here blows out the whole tabletop.
            MakePoint(lights.transform, new LightDef
            {
                pos = new[] { 0f, 3.0f, 0f },
                color = new[] { 1f, 0.70f, 0.38f },
                intensity = 1.8f, range = 8f
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
            // Startup vantage = the avatar-select overview of the whole table. When the player
            // picks their animal, TavernPresenter swoops this camera down into that seat
            // (first-person, local body hidden) — that runtime step is what makes it MP-ready.
            TavernSeating.SelectionPose(out var camPos, out var camRot);
            camGo.transform.SetPositionAndRotation(camPos, camRot);
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = TavernSeating.SelectionFieldOfView;
            cam.backgroundColor = new Color(0.03f, 0.025f, 0.02f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.nearClipPlane = 0.05f;
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
