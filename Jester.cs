using System;

namespace HostRules
{
    // Jester: a third team of one. When Jester mode is on, one random living crewmate (never an impostor) is picked
    // at the start of each round. The host can be picked too (config HostCanBeJester).
    //  - Outside meetings that player sees "Jester" as the name above his own head. Nobody else does.
    //      * a friend: his name is changed with a private message to his client only. The game puts the real name
    //        back whenever it refreshes player data, so the message is re-sent every ~1.5 s outside meetings.
    //      * the host: only the text on the host's own screen is changed (set every frame); the shared player data
    //        is never touched.
    //    In meetings the normal name is shown again.
    //  - He is picked when the intro starts and gets a private chat line telling him he is the Jester.
    //  - His tasks are fake: they never count toward the crew's task win (see TaskCount.cs).
    //  - If he is voted out, everyone's end screen shows him as the only winner (see JesterWin.cs).
    internal static class Jester
    {
        private const string Label = "Jester";
        // Sent privately at his first meeting (a chat line must stay under 100 characters, so it is split).
        private static readonly string[] MeetingLines =
        {
            "You are the JESTER! Get voted out to win.",
            "Your tasks are fake: they do not count for a crewmates win",
            "but will show up in the tasks bar just like a crewmate if you chose to do some.",
        };

        // What he sees above his head outside meetings: "Jester", plus a big "J" moved down onto his body
        // (only he sees it; size and height are adjustable in the config).
        private static string TagText
        {
            get
            {
                if (!Plugin.JesterBadge.Value) return Label;
                var size = Plugin.JesterBadgeSize.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                var off = Plugin.JesterBadgeOffset.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                return Label + "\n<voffset=" + off + "em><size=" + size + "%>J</size></voffset>";
            }
        }
        private const double ResendSeconds = 1.5;
        private static readonly Random Rng = new Random();

        private static bool _active;
        private static byte _id;
        private static bool _ejected;
        private static bool _inMeeting;
        private static DateTime _nextResend = DateTime.MinValue;

        private static bool _hostIsJester;  // the Jester is the host itself
        private static bool _tagShown;      // the host's own screen currently shows "Jester"
        private static string _origText;    // what the host's name text said before the tag
        private static bool _inSet;
        private static bool _warned;
        private static bool _picked;        // a pick was attempted this round
        private static bool _reminded;      // the meeting reminder was sent this round

        public static bool Active { get { return _active; } }
        public static bool HostIsJester { get { return _active && _hostIsJester; } }
        public static bool IsJester(byte playerId) { return _active && playerId == _id; }
        public static byte JesterId { get { return _id; } }

        private static bool Debug { get { return Plugin.JesterDebugLog.Value; } }

        public static void Reset()
        {
            if (_tagShown)
            {
                try { ShowHostTag(false); } catch (Exception) { }
            }
            _active = false;
            _ejected = false;
            _inMeeting = false;
            _hostIsJester = false;
            _tagShown = false;
            _origText = null;
            _picked = false;
            _reminded = false;
        }

        // Fallback (intro start / intro end): pick only if no pick happened yet this round.
        public static void EnsurePicked()
        {
            if (_picked) return;
            OnRoundStart();
        }

        // Roles have been handed out (or the intro is starting): choose the Jester.
        public static void OnRoundStart()
        {
            Reset();
            _picked = true;
            if (!Plugin.JesterEnabled.Value) return;

            var all = PlayerControl.AllPlayerControls;
            if (all == null) return;

            var candidates = new System.Collections.Generic.List<PlayerControl>();
            for (int i = 0; i < all.Count; i++)
            {
                var p = all[i];
                if (p == null) continue;
                var d = p.Data;
                if (d == null || d.Disconnected || d.IsDead) continue;
                if (d.Role == null)
                {
                    // Roles are not all known yet: try again when the intro starts.
                    _picked = false;
                    Plugin.L.LogInfo("[jester] roles not ready yet, picking later");
                    return;
                }
                if (p == PlayerControl.LocalPlayer && !Plugin.HostCanBeJester.Value) continue;
                if (d.Role.IsImpostor) continue;
                candidates.Add(p);
            }

            if (candidates.Count == 0)
            {
                Plugin.L.LogInfo("[jester] no eligible crewmate this round");
                return;
            }

            var chosen = candidates[Rng.Next(candidates.Count)];
            _id = chosen.PlayerId;
            _active = true;
            _hostIsJester = chosen == PlayerControl.LocalPlayer;

            if (_hostIsJester) ShowHostTag(true);
            else
            {
                SetNameFor(chosen, TagText);
                _nextResend = DateTime.UtcNow.AddSeconds(ResendSeconds);
            }
            // His tasks stop counting from now on.
            TaskCount.Refresh();

            // Normally the log does NOT say who it is, so the host can't spoil it by reading the file.
            if (Debug) Plugin.L.LogInfo("[jester][debug] candidates=" + candidates.Count + ", host is the Jester=" + _hostIsJester);
            Plugin.L.LogInfo("[jester] a Jester was assigned (identity hidden)");
        }

        // A meeting was called: give him his real name back before the meeting screen is built.
        public static void OnMeetingCalled()
        {
            if (!_active) return;
            _inMeeting = true;

            if (!_reminded)
            {
                _reminded = true;
                var jr = Find();
                if (jr != null && jr.Data != null && !jr.Data.Disconnected)
                    foreach (var line in MeetingLines) ChatLine.SendPrivate(jr, line);
            }

            if (_hostIsJester)
            {
                ShowHostTag(false);
                return;
            }
            var j = Find();
            if (j == null) return;
            SetNameFor(j, RealName(j));
        }

        // Players can move again: show "Jester" above his head again (if he is still in the round).
        public static void OnControlBack()
        {
            if (!_active) return;
            _inMeeting = false;
            if (_ejected) return;

            if (_hostIsJester)
            {
                var me = PlayerControl.LocalPlayer;
                var md = me != null ? me.Data : null;
                if (md != null && !md.IsDead && !md.Disconnected) ShowHostTag(true);
                return;
            }

            var j = Find();
            if (j == null) return;
            var d = j.Data;
            if (d == null || d.IsDead || d.Disconnected) return;
            SetNameFor(j, TagText);
            _nextResend = DateTime.UtcNow.AddSeconds(ResendSeconds);
        }

        public static void OnVotingComplete(NetworkedPlayerInfo exiled)
        {
            if (!_active || exiled == null) return;
            if (exiled.PlayerId == _id)
            {
                _ejected = true;
                Plugin.L.LogInfo("[jester] the Jester was voted out");
            }
        }

        // After the ejection scene: end the round.
        public static void TryEndGame()
        {
            if (!_active || !_ejected) return;
            _ejected = false;

            if (GameManager.Instance == null) return;

            // The reveal must be built BEFORE the roles are changed for the end screen.
            Reveal.Set(BuildReveal());
            Plugin.L.LogInfo("[jester] ending the round with the Jester as the only winner");
            JesterWin.Begin(_id);
        }

        // "Jester: Red Won" and "Impostors: Blue, Green", posted after the game.
        private static string[] BuildReveal()
        {
            var lines = new System.Collections.Generic.List<string>();
            var j = Find();
            lines.Add("Jester: " + (j != null ? ColorOf(j) : "Unknown") + " Won");

            var imps = new System.Collections.Generic.List<string>();
            var all = PlayerControl.AllPlayerControls;
            if (all != null)
            {
                for (int i = 0; i < all.Count; i++)
                {
                    var p = all[i];
                    if (p == null) continue;
                    var d = p.Data;
                    if (d == null || d.Disconnected || d.Role == null || !d.Role.IsImpostor) continue;
                    imps.Add(ColorOf(p));
                }
            }
            if (imps.Count > 0) lines.Add("Impostors: " + string.Join(", ", imps.ToArray()));
            return lines.ToArray();
        }

        private static string ColorOf(PlayerControl p)
        {
            try
            {
                var name = Palette.GetColorName(p.Data.DefaultOutfit.ColorId);
                return string.IsNullOrEmpty(name) ? "Unknown" : name;
            }
            catch (Exception)
            {
                return "Unknown";
            }
        }

        // Round over: put the real name back and forget everything.
        public static void OnRoundEnd()
        {
            if (_active && !_hostIsJester)
            {
                var j = Find();
                if (j != null) SetNameFor(j, RealName(j));
            }
            Reset(); // also puts the host's own tag back if the host was the Jester
        }

        // Runs every frame while hosting.
        public static void Tick()
        {
            if (!_active) return;
            var me = PlayerControl.LocalPlayer;
            if (me == null) return;

            if (_hostIsJester)
            {
                if (!_tagShown) return;
                var d = me.Data;
                if (d == null || d.IsDead || d.Disconnected)
                {
                    ShowHostTag(false);
                    return;
                }
                ApplyHostText(me);
                return;
            }

            // A friend is the Jester: keep re-sending his private name while the round is being played.
            if (_inMeeting || _ejected) return;
            if (MeetingHud.Instance != null || ExileController.Instance != null) return;
            var client = AmongUsClient.Instance;
            if (client == null || !client.IsGameStarted) return;

            var now = DateTime.UtcNow;
            if (now < _nextResend) return;

            var j = Find();
            if (j == null) return;
            var jd = j.Data;
            if (jd == null || jd.IsDead || jd.Disconnected) return;

            _nextResend = now.AddSeconds(ResendSeconds);
            SetNameFor(j, TagText);
        }

        // Called for every player's fixed update: for the host-as-Jester this keeps the tag on after the game's own refresh.
        public static void OnPlayerFixedUpdate(PlayerControl pc)
        {
            if (!_tagShown || !_hostIsJester) return;
            if (pc != PlayerControl.LocalPlayer) return;
            ApplyHostText(pc);
        }

        // The game just set a name on some player's cosmetics: if it was the host's own and the tag should be showing, put it back.
        public static void OnLayerSetName(CosmeticsLayer layer, string nameStr)
        {
            if (!_tagShown || _inSet || layer == null) return;
            var me = PlayerControl.LocalPlayer;
            if (me == null || me.cosmetics != layer) return;
            if (nameStr != TagText) _origText = nameStr; // the game's real text for later
            ApplyHostText(me);
        }

        // ---------------------------------------------------------------- helpers

        // The host's own screen only: only the text object is changed, never the shared player data.
        private static void ShowHostTag(bool on)
        {
            var me = PlayerControl.LocalPlayer;
            if (me == null) return;
            var layer = me.cosmetics;
            if (layer == null) return;
            var nt = layer.nameText;
            if (nt == null) return;

            try
            {
                if (on)
                {
                    if (!_tagShown) _origText = nt.text;
                    _tagShown = true;
                    ApplyHostText(me);
                    if (Debug) Plugin.L.LogInfo("[jester][debug] host tag ON, text now '" + nt.text + "'");
                }
                else
                {
                    _tagShown = false;
                    _inSet = true;
                    try { nt.text = string.IsNullOrEmpty(_origText) ? RealName(me) : _origText; }
                    finally { _inSet = false; }
                    if (Debug) Plugin.L.LogInfo("[jester][debug] host tag OFF, text now '" + nt.text + "'");
                }
            }
            catch (Exception e)
            {
                Plugin.L.LogWarning("[jester] could not change the host's tag: " + e.Message);
            }
        }

        private static void ApplyHostText(PlayerControl me)
        {
            var layer = me.cosmetics;
            if (layer == null) return;
            var nt = layer.nameText;
            if (nt == null) return;

            _inSet = true;
            try
            {
                var tag = TagText;
                if (nt.text != tag) nt.text = tag;
            }
            catch (Exception e)
            {
                if (!_warned)
                {
                    _warned = true;
                    Plugin.L.LogWarning("[jester] could not set the host's tag text: " + e.Message);
                }
            }
            finally
            {
                _inSet = false;
            }
        }

        private static PlayerControl Find()
        {
            if (!_active) return null;
            var all = PlayerControl.AllPlayerControls;
            if (all == null) return null;
            for (int i = 0; i < all.Count; i++)
            {
                var p = all[i];
                if (p != null && p.PlayerId == _id) return p;
            }
            return null;
        }

        private static string RealName(PlayerControl p)
        {
            return p.Data != null ? p.Data.PlayerName : "";
        }

        // A name change delivered ONLY to that player's own client (never used for the host itself).
        // Recent game versions put the player-info id in front of the name in this message.
        private static void SetNameFor(PlayerControl target, string name)
        {
            if (target == null || target == PlayerControl.LocalPlayer) return;
            try
            {
                var client = AmongUsClient.Instance;
                var writer = client.StartRpcImmediately(target.NetId, (byte)RpcCalls.SetName, Hazel.SendOption.Reliable, target.OwnerId);
                if (Plugin.JesterSendInfoId.Value && target.Data != null) writer.Write(target.Data.NetId);
                writer.Write(name);
                client.FinishRpcImmediately(writer);
                if (Debug) Plugin.L.LogInfo("[jester][debug] name message sent (" + (name == TagText ? "Jester tag" : "real name") + ")");
            }
            catch (Exception e)
            {
                Plugin.L.LogWarning("[jester] could not change the name: " + e.Message);
            }
        }
    }
}
