using System;

namespace HostRules
{
    // The crew's playable-time budget. Same rules as the old Impostor server build:
    //  - runs from the end of the intro
    //  - PAUSED from the moment a meeting is called, resumed when players regain control after the vote
    //  - on expiry the Impostors win (unless the crew already completed every task)
    //  - if the time runs out DURING a meeting (only possible with pause off), the loss is applied right AFTER
    //    that meeting, so the crew still gets the vote (e.g. to eject the last impostor)
    internal static class TaskTimer
    {
        private static bool _armed;
        private static bool _counting;
        private static bool _fired;
        private static bool _meetingSeen;
        private static bool _meetingActive;           // from the meeting CALL until players can move again
        private static DateTime? _meetingEndedUtc;    // when the last meeting finished
        private static DateTime _lastUtc;
        private static TimeSpan _elapsed;
        private static DateTime? _idleSince;

        public static bool Armed { get { return _armed; } }

        private static TimeSpan Limit
        {
            get { return TimeSpan.FromMinutes(Math.Max(0.5, Plugin.TimeLimitMinutes.Value)); }
        }

        public static void Reset()
        {
            _armed = false;
            _counting = false;
            _fired = false;
            _meetingSeen = false;
            _meetingActive = false;
            _meetingEndedUtc = null;
            _elapsed = TimeSpan.Zero;
            _idleSince = null;
        }

        public static void StartRound()
        {
            Reset();
            _armed = true;
            _counting = true;
            _lastUtc = DateTime.UtcNow;
            Plugin.L.LogInfo($"[timer] started: {Limit.TotalMinutes:0.##} min of playable time");
        }

        private static void Accumulate()
        {
            if (!_counting) return;
            var now = DateTime.UtcNow;
            _elapsed += now - _lastUtc;
            _lastUtc = now;
        }

        public static TimeSpan Remaining()
        {
            Accumulate();
            var left = Limit - _elapsed;
            return left > TimeSpan.Zero ? left : TimeSpan.Zero;
        }

        // Meeting CALLED (button or body report). With pause on the clock stops; with pause off it keeps running.
        public static void Pause()
        {
            if (!_armed) return;

            _meetingActive = true;
            _meetingSeen = false;
            _idleSince = null;

            if (!Plugin.PauseDuringMeetings.Value)
            {
                Plugin.L.LogInfo("[timer] meeting called - clock keeps running (pause is off), " + Fmt(Remaining()) + " left");
                return;
            }

            Accumulate();
            if (_counting) Plugin.L.LogInfo($"[timer] paused (meeting), {Fmt(Remaining())} left");
            _counting = false;
        }

        public static void NoteMeetingScreenOpened()
        {
            if (_armed) _meetingSeen = true;
        }

        // Players regained control after the vote/ejection: the meeting is over (and the clock restarts if it was paused).
        public static void Resume(string why)
        {
            if (!_armed || !_meetingActive) return;

            _meetingActive = false;
            _meetingEndedUtc = DateTime.UtcNow;
            _idleSince = null;

            if (!_counting)
            {
                _counting = true;
                _lastUtc = DateTime.UtcNow;
            }
            Plugin.L.LogInfo($"[timer] meeting over ({why}), {Fmt(Remaining())} left");
        }

        // Called every frame while hosting.
        public static void Tick()
        {
            if (!_armed) return;
            var client = AmongUsClient.Instance;
            if (client == null || !client.IsGameStarted) return;

            if (_meetingActive)
            {
                // Safety net: if the normal "players can move again" hook never fired, end the meeting state once the
                // meeting and ejection screens are gone and the host can walk again.
                var me = PlayerControl.LocalPlayer;
                bool settled = _meetingSeen
                    && MeetingHud.Instance == null
                    && ExileController.Instance == null
                    && me != null && me.moveable;
                if (settled)
                {
                    if (_idleSince == null) _idleSince = DateTime.UtcNow;
                    else if (DateTime.UtcNow - _idleSince.Value > TimeSpan.FromSeconds(1.5)) Resume("fallback");
                }
                else
                {
                    _idleSince = null;
                }
            }

            if (!_counting || _fired) return;
            if (Remaining() > TimeSpan.Zero) return;

            // Never end the round in the middle of a meeting: wait until it is over...
            if (_meetingActive || MeetingHud.Instance != null || ExileController.Instance != null) return;

            // ...and give a vote-based result (last impostor ejected, Jester ejected) a moment to land first.
            if (_meetingEndedUtc.HasValue && DateTime.UtcNow - _meetingEndedUtc.Value < TimeSpan.FromSeconds(1.0)) return;

            Expire();
        }

        private static void Expire()
        {
            _fired = true;

            // A real crew task win always takes precedence.
            var data = GameData.Instance;
            if (data != null && data.TotalTasks > 0 && data.CompletedTasks >= data.TotalTasks)
            {
                Plugin.L.LogInfo("[timer] expired but all tasks are done - leaving the result to the game");
                return;
            }

            var gm = GameManager.Instance;
            if (gm == null) return;

            Plugin.L.LogInfo("[timer] time is up -> Impostors win");
            try
            {
                gm.RpcEndGame(GameOverReason.ImpostorsBySabotage, false);
            }
            catch (Exception e)
            {
                Plugin.L.LogError("[timer] RpcEndGame failed: " + e);
            }
        }

        public static double LimitMinutes { get { return Limit.TotalMinutes; } }

        public static string Fmt(TimeSpan t)
        {
            return $"{(int)t.TotalMinutes}:{t.Seconds:D2}";
        }
    }
}
