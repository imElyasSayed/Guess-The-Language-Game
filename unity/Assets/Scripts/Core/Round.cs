namespace AccentGuesser.Core
{
    /// <summary>Immutable data for one round: the clip and the correct language to guess.</summary>
    public sealed class Round
    {
        public ClipInfo Clip { get; }
        public Lang Target { get; }

        public Round(ClipInfo clip, Lang target)
        {
            Clip = clip;
            Target = target;
        }
    }
}
