using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AccentGuesser.EditorTools
{
    /// <summary>
    /// Editor utility: opens the Tavern scene and renders its main camera to a PNG,
    /// so we can eyeball the in-engine look (lighting + imported materials) without
    /// opening the editor UI. Menu + batch entry point.
    /// </summary>
    public static class TavernCapture
    {
        private const string ScenePath = "Assets/Scenes/Tavern.unity";

        [MenuItem("Say Again/Capture Tavern Screenshot")]
        public static void CaptureShot()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            Camera cam;
            if (System.Environment.GetEnvironmentVariable("TAVERN_BIRDSEYE") == "1")
            {
                // diagnostic overview camera (high external 3/4) to inspect the layout
                var dbg = new GameObject("DebugCam");
                dbg.transform.position = new Vector3(8f, 7f, -9f);
                dbg.transform.rotation = Quaternion.LookRotation(
                    (new Vector3(0f, 0.8f, 0f) - dbg.transform.position).normalized);
                cam = dbg.AddComponent<Camera>();
                cam.fieldOfView = 45f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.1f, 0.12f, 0.15f);
                cam.farClipPlane = 80f;
            }
            else
            {
                cam = Object.FindFirstObjectByType<Camera>();
            }
            if (cam == null)
            {
                Debug.LogError("[Say Again] No camera in Tavern scene.");
                return;
            }

            const int w = 1024, h = 700;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();

            cam.targetTexture = null;
            RenderTexture.active = null;

            var outPath = Path.Combine(Application.dataPath, "..", "..",
                "blender", "out", "previews", "UNITY_tavern.png");
            File.WriteAllBytes(Path.GetFullPath(outPath), tex.EncodeToPNG());
            Debug.Log("[Say Again] Captured " + Path.GetFullPath(outPath));

            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
        }
    }
}
