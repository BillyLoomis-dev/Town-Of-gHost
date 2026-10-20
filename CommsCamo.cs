using System;
using System.Collections.Generic;

namespace HostRules
{
    // /comms on: while communications are sabotaged, every living player looks the same: grey, no hat, skin, visor
    // or pet, and no name above the head. Same idea as Town of Host's "comms camouflage".
    //
    // How it works (host only, the other players need nothing):
    //  - The host changes each living player's real look with the game's own messages (color, hat, skin, visor, pet,
    //    name), so every player's game shows it, the host's too, and each player sees himself grey as well. Because
    //    the color itself changes, the color-blind label above the head reads "Gray" for everyone.
    //    The name becomes the real name at zero size: invisible, but still known to the mod.
    //  - Everyone's real look is saved when the round starts and put back when comms is fixed.
    //  - Meetings: the real names come back (chat and votes need them); the looks stay grey while comms is down.
    //    Names are hidden again once players can move.
    //  - Impostors still see each other's names: each impostor gets his partners' real names privately, re-sent every
    //    1.5 s because the game puts the shared name back whenever it refreshes player data. A host who is an
    //    impostor sees them on his own screen.
    //  - A body stays grey: nobody knows who died until the meeting.
    //  - Dead players never get a pet back (a dead player's pet would show up again; Town of Host does the same).
    //  - A shapeshifted player is quietly turned back (no animation) when comms goes down and when it is fixed, so
    //    nobody is left in a look that gives him away.
    //  - Someone in a vent or invisible (Phantom) gets his real look back only once he is out / visible again, so a
    //    hat or pet can't appear out of nowhere.
    //  - The Fungle: nothing is changed while the game's own Mushroom Mixup is running.
    //  - Round over: the real looks are sent first and the ending waits a moment, so the end screen shows them.
    //  - Only a few players are changed per frame, so the server never gets a big burst of messages.
    //  - Looks are sent with Looks.Send (one packet per player, also right for the host's own player) and sent once
    //    more about 1 s later, in case a player's game dropped the first one.
    //  - Meetings: the real names go out the moment a body is reported or the button is pressed, before the meeting
    //    screen is built (its voting cards keep the names they were built with).
    internal static class CommsCamo
    {
        private const byte Gray = 15;

        private const int PlayersPerFrame = 3;
        private const double ConfirmSeconds = 1.0;           // looks are sent once more after this
        private const double ReportTimeoutSeconds = 8;       // a report that never became a meeting
        private const double PartnerResendSeconds = 1.5;
        private const double PrivateNameDelaySeconds = 0.25; // private names go out after the shared ones
        private const double MeetingFallbackSeconds = 20;    // if "players can move" is never reported
        private const double EndHoldSeconds = 0.3;
        private const double EndHoldMaxSeconds = 3;

        private sealed class Look
        {
            public string Name;
            public int Color;
            public string Hat;
            public string Skin;
            public string Visor;
            public string Pet;
        }

        private static readonly Dictionary<byte, Look> Real = new Dictionary<byte, Look>();
        private static readonly HashSet<byte> Grey = new HashSet<byte>();     // players showing the grey look now
        private static readonly HashSet<byte> Restore = new HashSet<byte>();  // grey players whose real look is due
        private static readonly Dictionary<byte, DateTime> Confirm = new Dictionary<byte, DateTime>(); // look re-sent then

        private static bool _started;     // this round was set up
        private static bool _rule;        // /comms was on when this round started
        private static bool _on;          // comms is down and the grey look is being shown
        private static bool _namesShown;  // a meeting is on: the real names are back
        private static bool _final;       // the round is ending: real looks for good, nothing turns grey any more
        private static bool _inMeeting;   // from the report / meeting call until players can move again
        private static bool _meetingSure; // the meeting really started (not just a report)
        private static DateTime _meetingMark;
        private static DateTime _nextPartners = DateTime.MinValue;

        // Holding the end of the round while the real looks go out.
        private static bool _holding;
        private static bool _endingNow;
        private static DateTime _holdStart;
        private static DateTime _endAt;
        private static GameOverReason _endReason;
        private static bool _endShowAd;

        public static bool HoldingEnd { get { return _holding; } }

        public static void Reset()
        {
            Real.Clear();
            Grey.Clear();
            Restore.Clear();
            Confirm.Clear();
            Looks.Reset();
            _started = false;
            _rule = false;
            _on = false;
            _namesShown = false;
            _final = false;
            _inMeeting = false;
            _meetingSure = false;
            _holding = false;
            _endingNow = false;
        }

        // ---------------------------------------------------------------- round start

        // Roles have been handed out: remember everyone's real look.
        public static void OnRoundStart()
        {
            Reset();
            _started = true;
            _rule = Plugin.CommsCamoEnabled.Value && Gate.ClassicMode();
            if (!_rule) return;

            var all = PlayerControl.AllPlayerControls;
            if (all != null)
                for (int i = 0; i < all.Count; i++) Capture(all[i]);
            Plugin.L.LogInfo("[comms] comms camouflage is on for this round (" + Real.Count + " real looks saved)");
        }

        // Fallback (intro start): set up the round if it wasn't done when roles were handed out.
        public static void EnsureStarted()
        {
            if (!_started) OnRoundStart();
        }

        private static void Capture(PlayerControl pc)
        {
            if (pc == null || Grey.Contains(pc.PlayerId) || Real.ContainsKey(pc.PlayerId)) return;
            var d = pc.Data;
            var o = d != null ? d.DefaultOutfit : null;
            if (o == null) return;
            Real[pc.PlayerId] = new Look
            {
                Name = o.PlayerName ?? "",
                Color = o.ColorId,
                Hat = o.HatId,
                Skin = o.SkinId,
                Visor = o.VisorId,
                Pet = o.PetId,
            };
        }

        // The player's real color, also while he is grey (for the Jester reveal).
        public static int RealColor(NetworkedPlayerInfo d)
        {
            if (d == null) return -1;
            Look l;
            if (Real.TryGetValue(d.PlayerId, out l)) return l.Color;
            return d.DefaultOutfit != null ? d.DefaultOutfit.ColorId : -1;
        }

        // ---------------------------------------------------------------- every frame (host)

        public static void Tick()
        {
            if (!_rule) return;
            var now = DateTime.UtcNow;

            if (_holding)
            {
                DrainRestores(true);
                if (Restore.Count > 0) _endAt = now.AddSeconds(EndHoldSeconds);
                if ((Restore.Count == 0 && now >= _endAt) || (now - _holdStart).TotalSeconds >= EndHoldMaxSeconds) FinishEnd();
                return;
            }
            if (_final)
            {
                DrainRestores(true);
                return;
            }

            var client = AmongUsClient.Instance;
            if (client == null || !client.IsGameStarted) return;
            var ship = ShipStatus.Instance;
            if (ship == null || JesterWin.Running) return;

            ConfirmDue(now);

            if (MeetingHud.Instance != null || ExileController.Instance != null)
            {
                if (_inMeeting)
                {
                    _meetingMark = now;
                    _meetingSure = true;
                }
                return;
            }
            if (_inMeeting)
            {
                double since = (now - _meetingMark).TotalSeconds;
                if (!_meetingSure && since >= ReportTimeoutSeconds)
                {
                    _inMeeting = false;
                    Plugin.L.LogInfo("[comms] the report did not start a meeting");
                }
                else if (since < MeetingFallbackSeconds) return; // meeting intro, ejection, Airship's spawn choice
                else
                {
                    _inMeeting = false;
                    Plugin.L.LogInfo("[comms] players can move again (fallback)");
                }
            }

            bool down = CommsDown(ship) && !MixupActive(ship);

            // First frame after a meeting: hide the names again if comms is still down.
            if (_namesShown)
            {
                _namesShown = false;
                if (_on && down) HideNamesAgain();
            }

            if (down && !_on) Begin();
            else if (!down && _on) End();

            if (_on)
            {
                GreyMore();
                Partners(now);
            }
            else DrainRestores(false);
        }

        private static bool CommsDown(ShipStatus ship)
        {
            try
            {
                if (ship.Systems == null || !ship.Systems.ContainsKey(SystemTypes.Comms)) return false;
                var system = ship.Systems[SystemTypes.Comms];
                if (system == null) return false;
                var hud = system.TryCast<HudOverrideSystemType>();
                if (hud != null) return hud.IsActive;
                var hq = system.TryCast<HqHudSystemType>(); // Mira HQ
                if (hq != null) return hq.IsActive;
            }
            catch (Exception)
            {
                // treated as "not down"
            }
            return false;
        }

        private static bool MixupActive(ShipStatus ship)
        {
            try
            {
                if (ship.Systems == null || !ship.Systems.ContainsKey(SystemTypes.MushroomMixupSabotage)) return false;
                var system = ship.Systems[SystemTypes.MushroomMixupSabotage];
                var mixup = system != null ? system.TryCast<MushroomMixupSabotageSystem>() : null;
                return mixup != null && mixup.IsActive;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void Begin()
        {
            _on = true;
            Restore.Clear(); // anyone still waiting for his real look simply stays grey
            Plugin.L.LogInfo("[comms] communications are down: everyone turns grey");
        }

        private static void End()
        {
            _on = false;
            foreach (var id in Grey) Restore.Add(id);
            Plugin.L.LogInfo("[comms] communications are fixed: real looks are coming back");
        }

        // Living players who aren't grey yet (a few per frame).
        private static void GreyMore()
        {
            var all = PlayerControl.AllPlayerControls;
            if (all == null) return;
            int n = 0;
            for (int i = 0; i < all.Count && n < PlayersPerFrame; i++)
            {
                var pc = all[i];
                if (pc == null || Grey.Contains(pc.PlayerId)) continue;
                var d = pc.Data;
                if (d == null || d.IsDead || d.Disconnected) continue;
                if (MakeGrey(pc)) n++;
            }
            if (n > 0) PrivateNamesSoon();
        }

        // Shared names were just sent: the private ones (impostor partners, the Jester's tag) go out again shortly
        // after, so they arrive last and stay.
        private static void PrivateNamesSoon()
        {
            _nextPartners = DateTime.UtcNow.AddSeconds(PrivateNameDelaySeconds);
            Jester.ResendSoon();
        }

        private static bool MakeGrey(PlayerControl pc)
        {
            Capture(pc);
            Look l;
            if (!Real.TryGetValue(pc.PlayerId, out l)) return false; // no real look to come back to: leave him alone
            Grey.Add(pc.PlayerId);
            try
            {
                if (pc.CurrentOutfitType == PlayerOutfitType.Shapeshifted) pc.RpcShapeshift(pc, false);
                Looks.Send(pc, Gray, Looks.NoHat, Looks.NoSkin, Looks.NoVisor, Looks.NoPet, Hidden(l.Name), true);
                Confirm[pc.PlayerId] = DateTime.UtcNow.AddSeconds(ConfirmSeconds);
            }
            catch (Exception e)
            {
                Plugin.L.LogWarning("[comms] could not make a player grey: " + e.Message);
            }
            return true;
        }

        // About 1 s after a change: send the look that player should have now once more (never the name, so the
        // private names - impostor partners, the Jester's tag - aren't overwritten).
        private static void ConfirmDue(DateTime now)
        {
            if (Confirm.Count == 0) return;
            int n = 0;
            foreach (var id in new List<byte>(Confirm.Keys))
            {
                if (n >= PlayersPerFrame) break;
                if (now < Confirm[id]) continue;
                Confirm.Remove(id);
                var pc = Find(id);
                Look l;
                if (pc == null || pc.Data == null || pc.Data.Disconnected || !Real.TryGetValue(id, out l)) continue;
                try
                {
                    if (Grey.Contains(id))
                    {
                        Looks.Send(pc, Gray, Looks.NoHat, Looks.NoSkin, Looks.NoVisor, Looks.NoPet, null, false, Looks.ResendLead);
                    }
                    else
                    {
                        bool dead = pc.Data.IsDead;
                        Looks.Send(pc, (byte)l.Color, l.Hat, l.Skin, l.Visor, dead ? Looks.NoPet : l.Pet, null, false, Looks.ResendLead);
                    }
                }
                catch (Exception e)
                {
                    Plugin.L.LogWarning("[comms] could not re-send a look: " + e.Message);
                }
                n++;
            }
        }

        // Real looks back (a few per frame). Not during meetings, and not for someone in a vent or invisible,
        // unless the round is ending.
        private static void DrainRestores(bool final)
        {
            if (Restore.Count == 0) return;
            if (!final && (MeetingHud.Instance != null || ExileController.Instance != null)) return;

            int budget = final ? PlayersPerFrame * 2 : PlayersPerFrame;
            int n = 0;
            foreach (var id in new List<byte>(Restore))
            {
                if (n >= budget) break;
                var pc = Find(id);
                if (pc == null || pc.Data == null)
                {
                    Restore.Remove(id);
                    Grey.Remove(id);
                    continue;
                }
                if (!final && OutOfSight(pc)) continue;
                RestoreOne(pc, final);
                n++;
            }
            if (n > 0) Jester.ResendSoon();
        }

        private static bool OutOfSight(PlayerControl pc)
        {
            try
            {
                var d = pc.Data;
                if (d == null || d.IsDead) return false;
                return pc.inVent || pc.walkingToVent || pc.shouldAppearInvisible;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void RestoreOne(PlayerControl pc, bool final)
        {
            var id = pc.PlayerId;
            Restore.Remove(id);
            Grey.Remove(id);

            Look l;
            if (!Real.TryGetValue(id, out l)) return;
            var d = pc.Data;
            bool dead = d == null || d.IsDead || d.Disconnected;
            try
            {
                if (!final && !dead && pc.CurrentOutfitType == PlayerOutfitType.Shapeshifted) pc.RpcShapeshift(pc, false);
                Looks.Send(pc, (byte)l.Color, l.Hat, l.Skin, l.Visor, dead ? Looks.NoPet : l.Pet, l.Name, true);
                if (final) Confirm.Remove(id);
                else Confirm[id] = DateTime.UtcNow.AddSeconds(ConfirmSeconds);
            }
            catch (Exception e)
            {
                Plugin.L.LogWarning("[comms] could not give a player his real look back: " + e.Message);
            }
        }

        // ---------------------------------------------------------------- meetings

        // A body was reported or the button pressed (host side, before anything of the meeting exists): real names
        // back right away, so every player's voting cards are built with them. Looks stay grey.
        public static void OnReport()
        {
            if (!_rule || _final || MeetingHud.Instance != null) return;
            if (!_inMeeting)
            {
                _inMeeting = true;
                _meetingSure = false;
            }
            _meetingMark = DateTime.UtcNow;
            ShowRealNames("report");
        }

        // The meeting really starts (backup, in case the report wasn't seen).
        public static void OnMeetingCalled()
        {
            if (!_rule || _final) return;
            _inMeeting = true;
            _meetingSure = true;
            _meetingMark = DateTime.UtcNow;
            ShowRealNames("meeting");
        }

        private static void ShowRealNames(string why)
        {
            if (_namesShown || Grey.Count == 0) return;
            var pcs = new List<PlayerControl>();
            var names = new List<string>();
            foreach (var id in Grey)
            {
                var pc = Find(id);
                Look l;
                if (pc == null || !Real.TryGetValue(id, out l)) continue;
                pcs.Add(pc);
                names.Add(l.Name);
            }
            try { Looks.SendNames(pcs, names); }
            catch (Exception e) { Plugin.L.LogWarning("[comms] could not show the names for the meeting: " + e.Message); }
            _namesShown = true;
            Plugin.L.LogInfo("[comms] " + why + ": real names shown for the meeting (the looks stay grey while comms is down)");
        }

        // Players can move again after the meeting (the next frame decides what to show).
        public static void OnControlBack()
        {
            _inMeeting = false;
        }

        // Players can move again and comms is still down: names hidden again (the living; ghosts keep theirs).
        private static void HideNamesAgain()
        {
            var pcs = new List<PlayerControl>();
            var names = new List<string>();
            foreach (var id in Grey)
            {
                var pc = Find(id);
                Look l;
                if (pc == null || !Real.TryGetValue(id, out l)) continue;
                var d = pc.Data;
                if (d == null || d.IsDead || d.Disconnected) continue;
                pcs.Add(pc);
                names.Add(Hidden(l.Name));
            }
            try { Looks.SendNames(pcs, names); }
            catch (Exception e) { Plugin.L.LogWarning("[comms] could not hide the names: " + e.Message); }
            PrivateNamesSoon();
            Plugin.L.LogInfo("[comms] comms is still down after the meeting: names hidden again");
        }

        // ---------------------------------------------------------------- impostors see each other's names

        private static void Partners(DateTime now)
        {
            if (_namesShown || now < _nextPartners) return;
            _nextPartners = now.AddSeconds(PartnerResendSeconds);

            var all = PlayerControl.AllPlayerControls;
            if (all == null) return;
            var imps = new List<PlayerControl>();
            for (int i = 0; i < all.Count; i++)
            {
                var pc = all[i];
                var d = pc != null ? pc.Data : null;
                if (d == null || d.Disconnected || !Teams.IsImpostor(d)) continue;
                imps.Add(pc);
            }
            if (imps.Count < 2) return;

            var me = PlayerControl.LocalPlayer;
            foreach (var viewer in imps)
            {
                if (viewer == me) continue; // the host's own screen is done in OnPlayerFixedUpdate
                foreach (var subject in imps)
                {
                    if (subject == viewer || subject.Data.IsDead || !Grey.Contains(subject.PlayerId)) continue;
                    Look l;
                    if (!Real.TryGetValue(subject.PlayerId, out l)) continue;
                    SendNameTo(subject, viewer, l.Name);
                }
            }
        }

        // The subject's name, delivered only to the viewer's game.
        private static void SendNameTo(PlayerControl subject, PlayerControl viewer, string name)
        {
            try
            {
                var client = AmongUsClient.Instance;
                var writer = client.StartRpcImmediately(subject.NetId, (byte)RpcCalls.SetName, Hazel.SendOption.Reliable, viewer.OwnerId);
                if (Plugin.JesterSendInfoId.Value && subject.Data != null) writer.Write(subject.Data.NetId);
                writer.Write(name);
                client.FinishRpcImmediately(writer);
            }
            catch (Exception e)
            {
                Plugin.L.LogWarning("[comms] could not send a partner's name: " + e.Message);
            }
        }

        // Host who is an impostor: show his partners' real names on his own screen (the game refreshes the name text
        // on every player's fixed update, so this runs right after it).
        public static void OnPlayerFixedUpdate(PlayerControl pc)
        {
            if (!_on || _namesShown || pc == null) return;
            if (!Grey.Contains(pc.PlayerId)) return;
            var me = PlayerControl.LocalPlayer;
            if (me == null || pc == me) return;

            var md = me.Data;
            var d = pc.Data;
            if (md == null || d == null || d.IsDead || d.Disconnected) return;
            if (!Teams.IsImpostor(md) || !Teams.IsImpostor(d)) return;

            Look l;
            if (!Real.TryGetValue(pc.PlayerId, out l)) return;
            var layer = pc.cosmetics;
            var nt = layer != null ? layer.nameText : null;
            if (nt != null && nt.text != l.Name) nt.text = l.Name;
        }

        // ---------------------------------------------------------------- end of the round

        // The Jester's win is starting: real looks back for good.
        public static void RestoreForEnd()
        {
            if (!_rule || _final) return;
            _final = true;
            _on = false;
            foreach (var id in Grey) Restore.Add(id);
            if (Restore.Count > 0) Plugin.L.LogInfo("[comms] the round is ending: real looks are sent back");
            DrainRestores(true);
        }

        // The game wants to end the round. Returns true when the ending must wait: the real looks go out first, then
        // the same ending is sent again a moment later.
        public static bool HoldEnd(GameOverReason reason, bool showAd)
        {
            if (_endingNow) return false;
            if (_holding) return true;
            if (!_rule) return false;

            _final = true;
            _on = false;
            foreach (var id in Grey) Restore.Add(id);
            if (Restore.Count == 0) return false;

            var now = DateTime.UtcNow;
            _holding = true;
            _holdStart = now;
            _endAt = now.AddSeconds(EndHoldSeconds);
            _endReason = reason;
            _endShowAd = showAd;
            Plugin.L.LogInfo("[comms] the round is ending (" + reason + "): real looks are sent first, the ending follows in a moment");
            DrainRestores(true);
            return true;
        }

        private static void FinishEnd()
        {
            _holding = false;
            var gm = GameManager.Instance;
            if (gm == null) return;
            _endingNow = true;
            try
            {
                gm.RpcEndGame(_endReason, _endShowAd);
                Plugin.L.LogInfo("[comms] round ended (" + _endReason + ")");
            }
            catch (Exception e)
            {
                Plugin.L.LogError("[comms] RpcEndGame failed: " + e);
            }
            finally
            {
                _endingNow = false;
            }
        }

        // ---------------------------------------------------------------- helpers

        private static string Hidden(string name)
        {
            return "<size=0>" + name + "</size>";
        }

        private static PlayerControl Find(byte id)
        {
            var all = PlayerControl.AllPlayerControls;
            if (all == null) return null;
            for (int i = 0; i < all.Count; i++)
            {
                var p = all[i];
                if (p != null && p.PlayerId == id) return p;
            }
            return null;
        }
    }
}
