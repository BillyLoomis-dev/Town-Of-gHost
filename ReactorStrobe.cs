using System;

namespace HostRules
{
    // /reactor on: during the seismic stabilizers sabotage on Polus, the crew's lights flash hard: pitch black (vision 0)
    // and back to normal, every /reactor X seconds (0.5 - 3), until the sabotage is fixed or a meeting starts.
    // No dimming: the crew's vision setting itself is switched, so it changes at once. Impostors and ghosts keep their
    // own vision. Each flash is one settings message to every player (the same message as a lobby settings change).
    // Polus only: Town of gHost is meant for Polus.
    internal static class ReactorStrobe
    {
        public const float MinSeconds = 0.5f;
        public const float MaxSeconds = 3f;

        private static bool _running;
        private static bool _dark;
        private static int _flashes;
        private static DateTime _next;

        public static void Reset()
        {
            _running = false;
            _dark = false;
            _flashes = 0;
            VisionOverride.SetStrobeDark(false, false);
        }

        public static float Seconds
        {
            get
            {
                var s = Plugin.ReactorFlashSeconds.Value;
                if (float.IsNaN(s)) s = 1f;
                return Math.Min(MaxSeconds, Math.Max(MinSeconds, s));
            }
        }

        // Every frame while hosting.
        public static void Tick()
        {
            bool active = Plugin.ReactorFlashEnabled.Value && SeismicActive();
            var now = DateTime.UtcNow;

            if (active && !_running)
            {
                _running = true;
                _flashes = 0;
                Plugin.L.LogInfo("[reactor] seismic stabilizers sabotaged: the crew's lights flash every " + Rules.Num(Seconds) + " s");
                Flash(true, now);
            }
            else if (!active && _running)
            {
                _running = false;
                if (_dark) VisionOverride.SetStrobeDark(false, true);
                _dark = false;
                Plugin.L.LogInfo("[reactor] seismic sabotage over: lights back to normal (" + _flashes + " flash(es))");
            }
            else if (_running && now >= _next)
            {
                Flash(!_dark, now);
            }
        }

        private static void Flash(bool dark, DateTime now)
        {
            _dark = dark;
            _next = now.AddSeconds(Seconds);
            if (dark) _flashes++;
            VisionOverride.SetStrobeDark(dark, true);
        }

        // Polus' seismic stabilizers are counting down (not during meetings, the Jester's win or the end of the round).
        private static bool SeismicActive()
        {
            try
            {
                var client = AmongUsClient.Instance;
                if (client == null || !client.IsGameStarted) return false;
                if (MeetingHud.Instance != null || ExileController.Instance != null) return false;
                if (JesterWin.Running || CommsCamo.HoldingEnd) return false;

                var ship = ShipStatus.Instance;
                if (ship == null || ship.TryCast<PolusShipStatus>() == null) return false;
                if (ship.Systems == null || !ship.Systems.ContainsKey(SystemTypes.Laboratory)) return false;
                var system = ship.Systems[SystemTypes.Laboratory];
                var reactor = system != null ? system.TryCast<ReactorSystemType>() : null;
                return reactor != null && reactor.IsActive;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
