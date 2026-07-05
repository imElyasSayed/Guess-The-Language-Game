using System.Threading.Tasks;
using AccentGuesser.Core;

namespace AccentGuesser.Services
{
    /// <summary>
    /// The Keep. Answers one free-form question about the clip's origin, honestly but
    /// never revealing the identity (brief §11). In single-player the client calls this
    /// directly; in multiplayer only the host does and broadcasts the filtered answer (§9).
    /// </summary>
    public interface IOracleClient
    {
        /// <summary>
        /// Ask the Keep a question about <paramref name="target"/>. Never throws for network
        /// reasons — on failure it returns a gruff in-character fallback so the round stays
        /// playable (brief §11 failure handling).
        /// </summary>
        Task<string> AskAsync(string question, ClipInfo target);
    }
}
