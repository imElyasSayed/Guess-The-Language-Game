using System.Threading.Tasks;
using AccentGuesser.Core;

namespace AccentGuesser.Services
{
    /// <summary>
    /// Offline stand-in for the Keep. Returns a canned gruff line so the single-player
    /// core is playable before the relay (brief §16 step 3) exists. Deterministic; no I/O.
    /// </summary>
    public sealed class MockOracleClient : IOracleClient
    {
        private static readonly string[] Lines =
        {
            "Cold as a witch's tit up that way, if you must know. Happy now, feathers?",
            "Aye, there's a coastline. Took you long enough to ask something sensible.",
            "Alphabet, not those little picture-squiggles. Now drink your ale and stop pestering me.",
            "Nice try, piggy — I'm not handing you the answer. Ask me something worth my breath."
        };

        private readonly string _fixed;

        /// <param name="fixedAnswer">If set, always returns this (useful for tests). Otherwise a canned line keyed off the question length.</param>
        public MockOracleClient(string fixedAnswer = null) => _fixed = fixedAnswer;

        public Task<string> AskAsync(string question, ClipInfo target)
        {
            if (_fixed != null) return Task.FromResult(_fixed);
            int idx = ((question?.Length ?? 0)) % Lines.Length;
            return Task.FromResult(Lines[idx]);
        }
    }
}
