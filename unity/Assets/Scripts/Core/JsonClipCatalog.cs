using System;
using System.Collections.Generic;
using System.Linq;

namespace AccentGuesser.Core
{
    /// <summary>
    /// SIMPLE clip catalog backed by an in-memory list of <see cref="ClipInfo"/>, normally
    /// parsed from a StreamingAssets <c>clips.json</c> manifest.
    ///
    /// PRODUCTION NOTE: Brief §7 prefers an embedded SQLite <c>game.db</c> read through a
    /// Unity SQLite plugin (SQLite-net), which allows richer server-side-style filtering.
    /// This JSON-manifest catalog is the sanctioned SIMPLE fallback for the single-player
    /// core. To switch, implement <see cref="IClipCatalog"/> over the DB and swap the
    /// instance handed to <see cref="GameController"/>; nothing else changes.
    ///
    /// Keep clips.json aligned with game.db using unity/tools/db_to_clips_json.sh.
    ///
    /// This class references no Unity types so it stays unit-testable in plain NUnit.
    /// The JSON <em>parsing</em> lives in the App layer (Unity's JsonUtility); this class
    /// takes an already-materialized list.
    /// </summary>
    public sealed class JsonClipCatalog : IClipCatalog
    {
        private readonly List<ClipInfo> _clips;
        private readonly List<Lang> _languages;

        public JsonClipCatalog(IEnumerable<ClipInfo> clips)
        {
            _clips = (clips ?? Enumerable.Empty<ClipInfo>())
                .Where(c => c != null && !string.IsNullOrEmpty(c.langId))
                .ToList();

            // Distinct languages by lang_id, preserving first-seen order.
            _languages = _clips
                .GroupBy(c => c.langId, StringComparer.Ordinal)
                .Select(g => g.First().ToLang())
                .ToList();
        }

        public IReadOnlyList<ClipInfo> AllClips => _clips;

        public IReadOnlyList<Lang> GetLanguages(ClipFilter filter)
        {
            if (string.IsNullOrEmpty(filter.Difficulty) && string.IsNullOrEmpty(filter.Region))
                return _languages;

            return _clips
                .Where(filter.Matches)
                .GroupBy(c => c.langId, StringComparer.Ordinal)
                .Select(g => g.First().ToLang())
                .ToList();
        }

        public ClipInfo GetRandomClip(string langId, ClipFilter filter, Random rng)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            var pool = _clips
                .Where(c => string.Equals(c.langId, langId, StringComparison.Ordinal) && filter.Matches(c))
                .ToList();

            if (pool.Count == 0) return null;
            return pool[rng.Next(pool.Count)];
        }
    }
}
