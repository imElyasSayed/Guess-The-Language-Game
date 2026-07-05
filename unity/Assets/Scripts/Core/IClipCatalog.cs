using System;
using System.Collections.Generic;

namespace AccentGuesser.Core
{
    /// <summary>
    /// Filter applied when listing languages or drawing a clip.
    /// A null field means "no constraint on that axis".
    /// </summary>
    public readonly struct ClipFilter
    {
        /// <summary>"common" | "all" | null (any).</summary>
        public readonly string Difficulty;

        /// <summary>Continent name to restrict to, or null (any). Matched case-insensitively.</summary>
        public readonly string Region;

        public ClipFilter(string difficulty, string region)
        {
            Difficulty = difficulty;
            Region = region;
        }

        public static ClipFilter None => new ClipFilter(null, null);

        public bool Matches(ClipInfo clip)
        {
            if (clip == null) return false;
            if (!string.IsNullOrEmpty(Difficulty) &&
                !string.Equals(clip.difficulty, Difficulty, StringComparison.OrdinalIgnoreCase))
                return false;
            // Region is matched against continent (brief §8 allows region = continent or ISO country).
            if (!string.IsNullOrEmpty(Region) &&
                !string.Equals(clip.continent, Region, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(clip.country, Region, StringComparison.OrdinalIgnoreCase))
                return false;
            return true;
        }
    }

    /// <summary>
    /// The read model the game rules need: which languages are available, and a random
    /// clip for a target language, filterable by difficulty/region.
    ///
    /// The simple shipped-now implementation is <see cref="JsonClipCatalog"/> (reads a
    /// clips.json manifest). Brief §7 prefers SQLite; the production version swaps this
    /// interface's implementation for one that queries game.db via a Unity SQLite plugin
    /// (e.g. SQLite-net) — no changes to the game rules that depend on this interface.
    /// </summary>
    public interface IClipCatalog
    {
        /// <summary>Distinct languages available under the given filter.</summary>
        IReadOnlyList<Lang> GetLanguages(ClipFilter filter);

        /// <summary>
        /// A random clip for <paramref name="langId"/> matching the filter, or null if none.
        /// <paramref name="rng"/> is injected so rounds are deterministic in tests.
        /// </summary>
        ClipInfo GetRandomClip(string langId, ClipFilter filter, Random rng);
    }
}
