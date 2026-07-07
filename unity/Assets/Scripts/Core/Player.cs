namespace AccentGuesser.Core
{
    /// <summary>
    /// One participant in a <see cref="MatchController"/> roster. Pure POCO, no Unity/netcode
    /// dependency. Persistent fields (score, streak) live across rounds; the per-round fields
    /// are cleared by <see cref="ResetForRound"/> at the start of each LISTEN phase.
    ///
    /// Mutation is internal: only <see cref="MatchController"/> (same assembly) advances a
    /// player's state, so callers can read but never forge score/lock state.
    /// </summary>
    public sealed class Player
    {
        public string Id { get; }
        public string DisplayName { get; internal set; }

        // ---- Persistent across rounds ----
        public int Score { get; internal set; }
        public int Streak { get; internal set; }

        // ---- Per-round (reset each LISTEN) ----
        /// <summary>True once this player has locked a guess this round (first lock is final).</summary>
        public bool HasLocked { get; internal set; }

        /// <summary>The locked guess text, or null if not yet locked.</summary>
        public string Guess { get; internal set; }

        /// <summary>
        /// True if the lock happened BEFORE the asker's hint was broadcast — the "Trust Your Ear"
        /// tier (+15). False means locked after the hint (+10). Meaningless until HasLocked.
        /// </summary>
        public bool LockedBeforeHint { get; internal set; }

        /// <summary>Populated at REVEAL; drives per-player result display.</summary>
        public ScoreResult LastResult { get; internal set; }

        public Player(string id, string displayName)
        {
            Id = id;
            DisplayName = displayName;
        }

        internal void ResetForRound()
        {
            HasLocked = false;
            Guess = null;
            LockedBeforeHint = false;
            LastResult = default;
        }
    }
}
