using System;
using System.Collections.Generic;
using AmongUs.GameOptions;

namespace HostRules
{
    // The Jester's victory screen, done the way the original Town of Host does it so it works for vanilla players too:
    //  1) the Jester gets a ghost role of one team, everyone else a ghost role of the other team
    //  2) after a short delay the players who were alive are revived, so the end screen shows them normally
    //  3) after another short delay the game is ended as a win for the Jester's team
    // Every player's own game then works out the winners from those roles: the Jester alone gets "Victory",
    // everyone else gets "Defeat". The delays keep the messages in the right order, like Town of Host's 0.2 s.
    //
    // Players who LEFT the game can't be given a new role: everyone's game still has their real one. So the Jester is
    // put on the side that makes them lose:
    //  - normally (nobody left, or only crewmates left): the Jester is on the Impostor side, end = Impostor win (red);
    //  - when more Impostors than crewmates left: the Jester is on the crew side, end = crew win (blue).
    // Otherwise an Impostor who left would show up on the end screen as a winner next to the Jester.
    internal static class JesterWin
    {
        private const double StepSeconds = 0.25;

        private static int _stage;            // 0 = idle, 1 = roles set, 2 = revived
        private static DateTime _next;
        private static bool _crewSide;        // the Jester wins on the crew side (see above)
        private static bool _ending;          // our own end-of-game call is being made
        private static readonly List<byte> Revive = new List<byte>();

        // True from the moment the sequence starts until the game has ended: the normal end checks must wait.
        public static bool Running { get { return _stage != 0; } }

        // True only while the Jester's own end-of-game call is being made (other endings are held back meanwhile).
        public static bool EndingNow { get { return _ending; } }

        public static void Reset()
        {
            _stage = 0;
            _crewSide = false;
            Revive.Clear();
        }

        public static void Begin(byte jesterId)
        {
            if (Running) return;
            TaskTimer.Reset(); // the time limit must not end the game in the middle of this
            CommsCamo.RestoreForEnd(); // real looks back first, so the end screen shows them

            try
            {
                _crewSide = ChooseCrewSide(jesterId);

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

                    bool jester = pc.PlayerId == jesterId;
                    RoleTypes role;
                    if (_crewSide) role = jester ? RoleTypes.CrewmateGhost : RoleTypes.ImpostorGhost;
                    else role = jester ? RoleTypes.ImpostorGhost : RoleTypes.CrewmateGhost;

                    pc.RpcSetRole(role, true);
                    d.IsDead = wasDead; // keep the real state until the revive step (same as Town of Host)
                }

                _stage = 1;
                _next = DateTime.UtcNow.AddSeconds(StepSeconds);
                Plugin.L.LogInfo("[jester] victory: roles set for the end screen (" + (_crewSide ? "crew" : "Impostor") + " side)");
            }
            catch (Exception e)
            {
                Plugin.L.LogError("[jester] victory setup failed, ending the game directly: " + e);
                EndNow();
            }
        }

        // Counts the players who left, by team, and picks the side where they all lose (or as many as possible).
        private static bool ChooseCrewSide(byte jesterId)
        {
            int impostorsLeft = 0, crewLeft = 0;
            try
            {
                var data = GameData.Instance;
                if (data == null) return false;
                var players = data.AllPlayers;
                for (int i = 0; i < players.Count; i++)
                {
                    var info = players[i];
                    if (info == null || !info.Disconnected || info.PlayerId == jesterId) continue;
                    if (Teams.IsImpostor(info)) impostorsLeft++;
                    else crewLeft++;
                }
            }
            catch (Exception e)
            {
                Plugin.L.LogWarning("[jester] could not check who left the game: " + e.Message);
                return false;
            }

            if (impostorsLeft > 0 || crewLeft > 0)
                Plugin.L.LogInfo("[jester] players who left the game: " + impostorsLeft + " Impostor(s), " + crewLeft + " crewmate(s)");
            return impostorsLeft > crewLeft;
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
            var reason = _crewSide ? GameOverReason.CrewmatesByVote : GameOverReason.ImpostorsByKill;
            _stage = 0;
            Revive.Clear();
            _ending = true;
            try
            {
                var gm = GameManager.Instance;
                if (gm != null) gm.RpcEndGame(reason, false);
                Plugin.L.LogInfo("[jester] victory: game ended");
            }
            catch (Exception e)
            {
                Plugin.L.LogError("[jester] RpcEndGame failed: " + e);
            }
            finally
            {
                _ending = false;
            }
        }
    }
}
