using System.Collections.Generic;
using System.IO;
using AccentGuesser.Core;
using UnityEngine;

namespace AccentGuesser.App
{
    /// <summary>
    /// Loads an <see cref="IClipCatalog"/> from a StreamingAssets manifest (clips.json).
    /// Factored out of the placeholder <c>GameManager</c> so the 3D <see cref="TavernPresenter"/>
    /// reuses the exact same, already-proven parsing instead of duplicating it.
    ///
    /// JsonUtility cannot parse a bare top-level array, so a bare "[ ... ]" is wrapped as
    /// { "clips": [ ... ] }; a manifest that is already { "clips": [ ... ] } is passed through.
    /// </summary>
    public static class ClipCatalogLoader
    {
        [System.Serializable]
        private class ClipArrayWrapper { public List<ClipInfo> clips; }

        /// <summary>
        /// Load and parse the manifest. Returns false (with <paramref name="error"/> set) if the
        /// file is missing, unreadable, or yields zero clips.
        /// </summary>
        public static bool TryLoad(string manifestFile, out IClipCatalog catalog, out string error)
        {
            catalog = null;
            error = null;
            string path = Path.Combine(Application.streamingAssetsPath, manifestFile);
            try
            {
                if (!File.Exists(path)) { error = "file not found"; return false; }
                var clips = Parse(File.ReadAllText(path));
                catalog = new JsonClipCatalog(clips);
                if (clips.Count == 0) { error = "no clips in manifest"; return false; }
                return true;
            }
            catch (System.Exception e)
            {
                error = e.Message;
                return false;
            }
        }

        private static List<ClipInfo> Parse(string json)
        {
            string trimmed = json.TrimStart();
            string wrapped = trimmed.StartsWith("[") ? "{\"clips\":" + json + "}" : json;
            var w = JsonUtility.FromJson<ClipArrayWrapper>(wrapped);
            return w?.clips ?? new List<ClipInfo>();
        }
    }
}
