namespace CraftFromDrawersAndChests.Logic
{
    /// <summary>
    /// How often this mod may <b>observe</b> a chest for its <c>Debug</c> diagnostics.
    /// <para>
    /// This throttles logging and nothing else: no request leaves the client, no chest is written, and
    /// no pull decision reads it. It exists because a chest's contents are a local copy loaded from its
    /// ZDO, so "the count dropped" cannot be attributed from the outside — the observation must record
    /// what the local copy and the ZDO say at the moment it changes, without becoming a per-frame flood.
    /// </para>
    /// </summary>
    public static class StorageRefreshRules
    {
        /// <summary>
        /// How long to wait before describing the same chest again. Short enough to catch a change made
        /// during a two-player test, long enough that a base of a few dozen chests costs a handful of log
        /// lines per second at worst.
        /// </summary>
        public const float ChestIntervalSeconds = 10f;

        /// <summary>
        /// How often an observation pass may run at all. Collection happens several times per frame
        /// (pullable list, diagnostic list, blocked list), so this is what turns "observed per collection"
        /// into one pass every couple of seconds.
        /// </summary>
        public const float PassIntervalSeconds = 2f;

        /// <summary>
        /// Whether something last seen at <paramref name="lastSeenAt"/> may be reported again at
        /// <paramref name="now"/>.
        /// <para>
        /// <c>lastSeenAt &lt;= 0</c> means "never", which is due — game time starts at zero and the first
        /// pass must not be suppressed. A non-positive <paramref name="minIntervalSeconds"/>
        /// <b>disables</b> the report rather than making it every frame: if this ever becomes
        /// configurable, a zero must not turn into a log flood.
        /// </para>
        /// </summary>
        public static bool IsDue(float now, float lastSeenAt, float minIntervalSeconds)
        {
            if (minIntervalSeconds <= 0f)
                return false;

            if (lastSeenAt <= 0f)
                return true;

            return now - lastSeenAt >= minIntervalSeconds;
        }
    }
}
