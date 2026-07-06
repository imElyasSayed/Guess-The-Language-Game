using System;

namespace AccentGuesser.Core
{
    /// <summary>
    /// Builds a <see cref="Round"/>: picks a target language + clip for the player to
    /// free-text guess (brief §8, superseded: multiple-choice replaced by typed guesses).
    ///
    /// All randomness comes from an injected <see cref="System.Random"/> so rounds are
    /// deterministic in tests.
    /// </summary>
    public static class RoundFactory
    {
        /// <summary>
        /// Create a round from the catalog under the given filter.
        /// Throws if no languages are available, or if the chosen target has no clip
        /// matching the filter.
        /// </summary>
        public static Round CreateRound(IClipCatalog catalog, ClipFilter filter, Random rng)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            var languages = catalog.GetLanguages(filter);
            if (languages.Count < 1)
                throw new InvalidOperationException("No languages available under the current filter.");

            var target = languages[rng.Next(languages.Count)];

            var clip = catalog.GetRandomClip(target.Id, filter, rng);
            if (clip == null)
                throw new InvalidOperationException($"No clip found for target language '{target.Id}'.");

            return new Round(clip, target);
        }
    }
}
