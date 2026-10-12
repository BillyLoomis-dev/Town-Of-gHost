using InnerNet;

namespace HostRules
{
    internal static class Gate
    {
        // True only while the local player is the host of an ONLINE lobby (never practice/freeplay).
        public static bool ActiveHost()
        {
            if (!Plugin.Enabled.Value) return false;
            var client = AmongUsClient.Instance;
            if (client == null) return false;
            if (client.NetworkMode != NetworkModes.OnlineGame) return false;
            return client.AmHost;
        }

        // In the lobby, before the game has started.
        public static bool InLobby()
        {
            var client = AmongUsClient.Instance;
            return client != null && !client.IsGameStarted && LobbyBehaviour.Instance != null;
        }

        // Classic mode only (not Hide & Seek).
        public static bool ClassicMode()
        {
            var gm = GameManager.Instance;
            return gm != null && gm.IsNormal();
        }
    }

    // Innersloth's "+25 modded flag" for host-only mods: adding 25 to the protocol version we advertise
    // while HOSTING puts the lobby in host-authority mode (the host decides things like game end).
    // Players who join are completely unaffected and need nothing installed.
    internal static class HostFlag
    {
        public const int Plus = 25;
        private static bool _active;

        public static bool Active { get { return _active; } }

        public static void Set(bool on, string why)
        {
            if (_active == on) return;
            _active = on;
            Plugin.L.LogInfo("[host] +25 flag " + (on ? "ON" : "OFF") + " (" + why + ")");
        }

        // What the game itself says about its version / modded state (helps diagnose server kicks).
        public static void LogVersionInfo()
        {
            try
            {
                var compat = Constants.CompatVersions;
                var cv = "";
                if (compat != null)
                    for (int i = 0; i < compat.Length; i++) cv += (i > 0 ? "," : "") + compat[i];

                Plugin.L.LogInfo("[host] version info: date=" + Constants.Year + "-" + Constants.Month + "-" + Constants.Day
                    + " rev=" + Constants.Revision + " visualRev=" + Constants.VisualRevision
                    + " MODDER_VERSION=" + Constants.MODDER_VERSION
                    + " MODDED_REVISION_MODIFIER_VALUE=" + Constants.MODDED_REVISION_MODIFIER_VALUE
                    + " IsVersionModded=" + Constants.IsVersionModded()
                    + " compat=[" + cv + "]");
            }
            catch (System.Exception e)
            {
                Plugin.L.LogError("[host] version info failed: " + e.Message);
            }
        }
    }
}
