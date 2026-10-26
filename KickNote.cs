using System;

namespace HostRules
{
    // /kick and /ban: everyone is told in the chat that the host removed the player, so it isn't mistaken for a normal
    // disconnect. If that kick ends the round (the last Impostor, or too few crewmates, left), the chat of the round is
    // gone, so the line is posted again in the lobby chat 5 seconds after the lobby opens.
    internal static class KickNote
    {
        private const double EndWindowSeconds = 15; // the round ended this soon after the kick: the kick ended it
        private const double LobbyDelaySeconds = 5;

        private static string _lastLine;            // "The game ended because the host kicked Red (Bob)."
        private static DateTime _lastAt;
        private static string _lobbyLine;           // waiting for the lobby
        private static DateTime? _lobbySince;

        public static void Clear()
        {
            _lastLine = null;
            _lobbyLine = null;
            _lobbySince = null;
        }

        // The host just kicked or banned someone (who = "Red (Bob)").
        public static void OnKick(string who, bool ban)
        {
            ChatLine.Enqueue(who + " was " + (ban ? "banned" : "kicked") + " by the host.");

            var client = AmongUsClient.Instance;
            if (client == null || !client.IsGameStarted) return; // in the lobby there's no round to end
            _lastLine = "The game ended because the host " + (ban ? "banned " : "kicked ") + who + ".";
            _lastAt = DateTime.UtcNow;
        }

        // The round is over: if a kick just ended it (a disconnect ending), keep the line for the lobby.
        public static void OnGameEnd(GameOverReason reason)
        {
            var line = _lastLine;
            _lastLine = null;
            if (line == null) return;
            if (reason != GameOverReason.ImpostorDisconnect && reason != GameOverReason.CrewmateDisconnect) return;
            if ((DateTime.UtcNow - _lastAt).TotalSeconds > EndWindowSeconds) return;

            _lobbyLine = line;
            _lobbySince = null;
            Plugin.L.LogInfo("[cmd] the round ended because of a kick; it will be posted in the lobby");
        }

        // Every frame while hosting: post the line 5 s after the lobby opens.
        public static void Tick()
        {
            if (_lobbyLine == null) return;
            if (!Gate.InLobby())
            {
                _lobbySince = null;
                return;
            }
            if (_lobbySince == null)
            {
                _lobbySince = DateTime.UtcNow;
                return;
            }
            if ((DateTime.UtcNow - _lobbySince.Value).TotalSeconds < LobbyDelaySeconds) return;

            ChatLine.Enqueue(_lobbyLine);
            _lobbyLine = null;
            _lobbySince = null;
        }
    }
}
