using System;
using System.Collections.Generic;
using AmongUs.GameOptions;

namespace HostRules
{
    // The Jester's victory screen, done the way the original Town of Host does it so it works for vanilla players too:
    //  1) the Jester's role becomes "Impostor ghost", everyone else's (real impostors included) "Crewmate ghost"
    //  2) after a short delay the players who were alive are revived, so the end screen shows them normally
    //  3) after another short delay the game is ended as an impostor win
    // Every player's own game then works out the winners from those roles: the Jester alone gets "Victory",
    // everyone else gets "Defeat". The delays keep the messages in the right order, like Town of Host's 0.2 s.
    internal static class JesterWin
    {
        private const double StepSeconds = 0.25;

        private static int _stage;            // 0 = idle, 1 = roles set, 2 = revived
        private static DateTime _next;
        private static readonly List<byte> Revive = new List<byte>();

        // True from the moment the sequence starts until the game has ended: the normal end checks must wait.
        public static bool Running { get { return _stage != 0; } }

        public static void Reset()
        {
            _stage = 0;
            Revive.Clear();
        }

        public static void Begin(byte jesterId)
        {
            if (Running) return;
            TaskTimer.Reset(); // the time limit must not end the game in the middle of this

            try
            {
                Revive.Clear();
                var all = PlayerControl.AllPlayerControls;
                for (int i = 0; i < all.Count; i++)
                {
                    var pc = all[i];
                    if (pc == null) continue;
                    var d = pc.Data;
                    if (d == null || d.Disconnected) continue;

                    bool wasDead = d.IsDead;
                    if (!wasDead) Revive.Add(pc.PlayerId);

                    pc.RpcSetRole(pc.PlayerId == jesterId ? RoleTypes.ImpostorGhost : RoleTypes.CrewmateGhost, true);
                    d.IsDead = wasDead; // keep the real state until the revive step (same as Town of Host)
                }

                _stage = 1;
                _next = DateTime.UtcNow.AddSeconds(StepSeconds);
                Plugin.L.LogInfo("[jester] victory: roles set for the end screen");
            }
            catch (Exception e)
            {
                Plugin.L.LogError("[jester] victory setup failed, ending the game directly: " + e);
                EndNow();
            }
        }

        // Runs every frame (from AmongUsClient.Update).
        public static void Tick()
        {
            if (_stage == 0 || DateTime.UtcNow < _next) return;

            if (_stage == 1)
            {
                try
                {
                    var client = AmongUsClient.Instance;
                    var data = GameData.Instance;
                    foreach (var id in Revive)
                    {
                        var info = data != null ? data.GetPlayerById(id) : null;
                        if (info == null) continue;
                        info.IsDead = false;
                        info.MarkDirty();
                        // one player per send, like Town of Host, so the packets stay small
                        if (client != null) client.SendAllStreamedObjects();
                    }
                    Plugin.L.LogInfo("[jester] victory: " + Revive.Count + " player(s) shown alive on the end screen");
                }
                catch (Exception e)
                {
                    Plugin.L.LogWarning("[jester] victory revive step failed: " + e.Message);
                }

                _stage = 2;
                _next = DateTime.UtcNow.AddSeconds(StepSeconds);
                return;
            }

            EndNow();
        }

        private static void EndNow()
        {
            _stage = 0;
            Revive.Clear();
            try
            {
                var gm = GameManager.Instance;
                if (gm != null) gm.RpcEndGame(GameOverReason.ImpostorsByKill, false);
                Plugin.L.LogInfo("[jester] victory: game ended");
            }
            catch (Exception e)
            {
                Plugin.L.LogError("[jester] RpcEndGame failed: " + e);
            }
        }
    }
}
