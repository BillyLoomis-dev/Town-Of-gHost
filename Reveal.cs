using System;

namespace HostRules
{
    // After a Jester win: once everyone is back in the lobby, post who the Jester and the Impostors were.
    internal static class Reveal
    {
        private const double DelaySeconds = 8; // players return to the lobby at different times
        private static string[] _lines;
        private static DateTime? _lobbySince;

        public static void Set(string[] lines)
        {
            _lines = lines;
            _lobbySince = null;
        }

        public static void Clear()
        {
            _lines = null;
            _lobbySince = null;
        }

        public static void Tick()
        {
            if (_lines == null) return;
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
            if ((DateTime.UtcNow - _lobbySince.Value).TotalSeconds < DelaySeconds) return;

            foreach (var line in _lines) ChatLine.Enqueue(line);
            Plugin.L.LogInfo("[jester] reveal posted in the lobby chat");
            Clear();
        }
    }
}
