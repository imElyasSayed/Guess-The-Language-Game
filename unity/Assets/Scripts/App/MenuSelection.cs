namespace AccentGuesser.App
{
    /// <summary>
    /// Tiny cross-scene carrier for what the player chose on the main menu. The menu scene writes
    /// it; <see cref="TavernBootstrap"/> consumes it once on arrival in Tavern.unity.
    ///
    /// Only SOLO needs an explicit flag — networked arrivals are detected from the live
    /// <c>NetworkManager</c> (it persists across the scene switch). The join code rides along so
    /// the in-game roster chip keeps showing the host's shareable address, and the notice lets a
    /// disconnect explain itself after the menu scene reloads.
    /// </summary>
    public static class MenuSelection
    {
        /// <summary>Set by the menu's Play Solo; consumed by the tavern to skip its mode panel.</summary>
        public static bool SoloPending;

        /// <summary>The host's shareable address/join code, carried into the tavern HUD.</summary>
        public static string JoinCode = "";

        /// <summary>One-shot message shown on the next menu visit (e.g. "the host left").</summary>
        public static string Notice = "";

        public static bool ConsumeSolo()
        {
            bool solo = SoloPending;
            SoloPending = false;
            return solo;
        }

        public static string ConsumeJoinCode()
        {
            string code = JoinCode ?? "";
            JoinCode = "";
            return code;
        }

        public static string ConsumeNotice()
        {
            string notice = Notice ?? "";
            Notice = "";
            return notice;
        }
    }
}
