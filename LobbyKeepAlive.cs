using System;

namespace HostRules
{
    // Keeps the lobby from being closed for inactivity by using the game's own extension feature:
    // when the server warns the host that the lobby is about to expire, the game normally shows a popup and
    // waits for the host to click OK. The mod clicks it automatically.
    // How many extensions the server offers and how much time each one grants is decided by the server, not by us;
    // every offer is written to LogOutput.log so you can see the real numbers.
    // Config: LobbyKeepAlive (on/off) and LobbyKeepAliveMaxMinutes (0 = unlimited, i.e. as long as the server allows).
    internal static class LobbyKeepAlive
    {
        private static DateTime _lobbySince = DateTime.UtcNow;
        private static int _extensions;

        // A lobby (new or after a game) has just loaded.
        public static void OnLobbyStart()
        {
            _lobbySince = DateTime.UtcNow;
            _extensions = 0;
        }

        public static void OnExtensionOffer(LobbyBehaviour lobby, int timeRemaining, bool available, int extensionId, int grantedSeconds)
        {
            var client = AmongUsClient.Instance;
            if (client == null || !client.AmHost) return;

            Plugin.L.LogInfo("[lobby] expiry warning from the server: " + timeRemaining + " s left, extension available="
                + available + ", it would add " + grantedSeconds + " s (offer #" + extensionId + ")");

            if (!Plugin.Enabled.Value || !Plugin.LobbyKeepAlive.Value) return;
            if (!available)
            {
                Plugin.L.LogWarning("[lobby] the server offers no more extensions - the lobby will close when its time runs out");
                return;
            }

            var max = Plugin.LobbyKeepAliveMaxMinutes.Value;
            var waited = (DateTime.UtcNow - _lobbySince).TotalMinutes;
            if (max > 0 && waited >= max)
            {
                Plugin.L.LogInfo("[lobby] not extending: the lobby has been waiting " + waited.ToString("0") + " min (limit " + max + " min)");
                return;
            }

            try
            {
                lobby.RpcExtendLobbyTimer();
                _extensions++;
                Plugin.L.LogInfo("[lobby] extension requested automatically (#" + _extensions + " for this lobby)");
            }
            catch (Exception e)
            {
                Plugin.L.LogWarning("[lobby] could not request the extension: " + e.Message);
            }
        }
    }
}
