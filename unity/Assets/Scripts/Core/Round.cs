using System.Collections.Generic;

namespace AccentGuesser.Core
{
    /// <summary>Immutable data for one round: the clip, the correct language, and the shuffled choices.</summary>
    public sealed class Round
    {
        public ClipInfo Clip { get; }
        public Lang Target { get; }
        public IReadOnlyList<Lang> Choices { get; }

        public Round(ClipInfo clip, Lang target, IReadOnlyList<Lang> choices)
        {
            Clip = clip;
            Target = target;
            Choices = choices;
        }
    }
}
