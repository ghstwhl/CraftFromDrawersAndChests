using System;
using BepInEx.Logging;
using UnityEngine;

namespace CraftFromDrawersAndChests.Chest {
    /// <summary>
    /// Helper class for properly logging from static contexts.
    /// <para>
    /// DIVERGENCE FROM UPSTREAM: upstream gates its verbose output on the compile-time <c>DEBUG</c>
    /// symbol, so a shipped build can only ever be silent or slow - its own changelog notes that a
    /// debug build "has slowed down the mod a lot". Here the same call sites are gated on a runtime
    /// probe instead, so a player running our published Release build can turn the diagnostic detail
    /// on from the mod's own Debug setting and back off again without a different download.
    /// </para>
    /// </summary>
    public static class Log {
        private static ManualLogSource logSource;
        private static Func<bool> isDebugEnabled;

        internal static void Init(ManualLogSource manualLogSource) {
            logSource = manualLogSource;
        }

        /// <summary>
        /// Supplies the runtime switch for <see cref="LogDebug"/> and <see cref="LogCodeInstruction"/>.
        /// Kept as a delegate so this class does not depend on the config model.
        /// </summary>
        internal static void SetDebugProbe(Func<bool> probe) {
            isDebugEnabled = probe;
        }

        /// <summary>Whether verbose chest-layer logging is on right now.</summary>
        public static bool IsDebug => isDebugEnabled != null && isDebugEnabled();

        public static void LogCodeInstruction(object data) {
            if (!IsDebug) {
                return;
            }

            if (logSource != null) {
                logSource.LogInfo("[chest-il] " + data);
            } else {
                Debug.Log(data);
            }
        }

        public static void LogDebug(string data) {
            if (!IsDebug) {
                return;
            }

            if (logSource != null) {
                // LogInfo, not LogDebug: BepInEx's default [Logging.Disk] levels exclude Debug, so a
                // LogDebug line never reaches LogOutput.log - the file a support report sends us.
                logSource.LogInfo(data);
            } else {
                Debug.Log(data);
            }
        }

        public static void LogError(string data) {
            if (logSource != null) {
                logSource.LogError(data);
            } else {
                Debug.LogError(data);
            }
        }

        public static void LogFatal(string data) {
            if (logSource != null) {
                logSource.LogFatal(data);
            } else {
                Debug.LogError(data);
            }
        }

        public static void LogInfo(string data) {
            if (logSource != null) {
                logSource.LogInfo(data);
            } else {
                Debug.Log(data);
            }
        }

        public static void LogWarning(string data) {
            if (logSource != null) {
                logSource.LogWarning(data);
            } else {
                Debug.LogWarning(data);
            }
        }
    }
}
