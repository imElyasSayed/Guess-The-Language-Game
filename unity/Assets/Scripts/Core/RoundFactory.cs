using System;
using System.Collections.Generic;
using System.Linq;

namespace AccentGuesser.Core
{
    /// <summary>
    /// Builds a <see cref="Round"/>: picks a target language + clip, then assembles
    /// 4 shuffled choices = target + 3 random non-target distractors from the pool
    /// (brief §8: "3 random non-target languages from the active pool, shuffled with the target").
    ///
    /// All randomness comes from an injected <see cref="System.Random"/> so rounds are
    /// deterministic in tests.
    /// </summary>
    public static class RoundFactory
    {
        public const int ChoiceCount = 4;

        /// <summary>
        /// Create a round from the catalog under the given filter.
        /// Throws if fewer than <see cref="ChoiceCount"/> languages are available, or if the
        /// chosen target has no clip matching the filter.
        /// </summary>
        public static Round CreateRound(IClipCatalog catalog, ClipFilter filter, Random rng)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            var languages = catalog.GetLanguages(filter);
            if (languages.Count < ChoiceCount)
                throw new InvalidOperationException(
                    $"Need at least {ChoiceCount} languages under the current filter, found {languages.Count}.");

            var target = languages[rng.Next(languages.Count)];

            var clip = catalog.GetRandomClip(target.Id, filter, rng);
            if (clip == null)
                throw new InvalidOperationException($"No clip found for target language '{target.Id}'.");

            // 3 distractors: shuffle the non-target languages, take the first 3.
            var distractors = languages
                .Where(l => !l.Equals(target))
                .ToList();
            Shuffle(distractors, rng);

            var choices = new List<Lang> { target };
            choices.AddRange(distractors.Take(ChoiceCount - 1));

            Shuffle(choices, rng);

            return new Round(clip, target, choices);
        }

        /// <summary>In-place Fisher-Yates shuffle driven by the injected rng.</summary>
        internal static void Shuffle<T>(IList<T> list, Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
