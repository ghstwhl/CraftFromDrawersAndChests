using System.Diagnostics;

namespace CraftFromDrawersAndChests.Chest {
    /// <summary>
    /// Round-trip timing for a routed request.
    /// <para>
    /// DIVERGENCE FROM UPSTREAM: upstream compiled this whole class out of Release builds
    /// (<c>#if DEBUG</c>), so its callers had to be guarded too. It is compiled in here and gated on
    /// <see cref="Log.IsDebug"/> at run time instead, which keeps the call sites clean and makes the
    /// timing available in a published build when a player turns Debug on.
    /// </para>
    /// </summary>
    public class Timer {
        private static readonly Stopwatch stopwatch = new Stopwatch();

        public static void Start(IPackage request) {
            if (!Log.IsDebug) {
                return;
            }

            request.PrintDebug();
            stopwatch.Restart();
        }

        public static void Stop(string method) {
            if (!Log.IsDebug) {
                return;
            }

            Log.LogDebug($"{method}: {stopwatch.ElapsedMilliseconds}ms");
            stopwatch.Restart();
        }
    }
}
