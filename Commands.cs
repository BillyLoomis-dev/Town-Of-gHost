using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace HostRules
{
    // Chat commands typed by the HOST. They are handled locally and never sent to the other players.
    // Whenever a rule is added or changed, its full details are posted in the chat for everyone.
    internal static class Commands
    {
        private const double MinMinutes = 0.5;
        private const double MaxMinutes = 180;

        // True while the plugin itself is sending chat, so its own lines are never re-read as commands.
        public static bool Busy;

        private static readonly Regex Tag = new Regex("</?[a-zA-Z#][^>]*>", RegexOptions.Compiled);

        // Same tag-stripping as the old server: plain "3 < 5" stays, real rich-text tags are removed.
        public static string Strip(string s)
        {
            return string.IsNullOrEmpty(s) ? string.Empty : Tag.Replace(s, string.Empty);
        }

        public static bool IsWord(string text, string cmd)
        {
            return text.Equals(cmd, StringComparison.OrdinalIgnoreCase)
                || text.StartsWith(cmd + " ", StringComparison.OrdinalIgnoreCase);
        }

        public static string Arg(string text, string cmd)
        {
            return text.Length <= cmd.Length ? string.Empty : text.Substring(cmd.Length).Trim();
        }

        // Returns true when the text was a command / secret line and must NOT go out as normal chat.
        public static bool HandleHostText(PlayerControl me, string raw)
        {
            var text = Strip(raw).Trim();
            if (text.Length == 0 || text[0] != '/') return false;

            if (IsWord(text, "/help")) { Help(me); return true; }
            if (IsWord(text, "/settings")) { Announce(Rules.SettingsLines()); return true; }
            if (IsWord(text, "/time")) { Time(me, Arg(text, "/time")); return true; }
            if (IsWord(text, "/vision")) { Vision(me, Arg(text, "/vision")); return true; }
            if (IsWord(text, "/lightvision")) { LightVision(me, Arg(text, "/lightvision")); return true; }
            if (IsWord(text, "/jester")) { Switch(me, "/jester", Arg(text, "/jester"), Plugin.JesterEnabled, Rules.JesterLines); return true; }
            if (IsWord(text, "/impchat")) { Switch(me, "/impchat", Arg(text, "/impchat"), Plugin.ImpostorChatEnabled, Rules.ImpChatLines); return true; }

            // Host as impostor: "// msg" (or "/cmd msg") goes to the impostor team only (when impostor chat is on).
            string secret = null;
            if (text.StartsWith("//", StringComparison.Ordinal)) secret = text.Substring(2);
            else if (IsWord(text, "/cmd")) secret = Arg(text, "/cmd");

            if (secret != null && Plugin.ImpostorChatEnabled.Value && ImpostorChat.IsImpostor(me))
            {
                ImpostorChat.Send(me, secret);
                return true;
            }

            // Anything else (a crewmate host typing "//", impostor chat off...) is just ordinary chat.
            return false;
        }

        // ---------------------------------------------------------------- /help (shown only on the host's screen)
        private static void Help(PlayerControl me)
        {
            Local(me, "Host commands (only you see this list). Everything is OFF in a new lobby and stays as you set it after a game:");
            Local(me, "/time on | /time off - the tasks time limit.  /time 18 sets the minutes (and turns it on).");
            Local(me, "/time pause on | off - should meetings pause the time limit clock.");
            Local(me, "/vision 0.15 - crew vision (" + Range() + ", 0 = pitch black).  /vision off - back to normal.");
            Local(me, "/lightvision 0.05 - crew vision while the lights are out (" + Range() + "). Impostors keep full vision.  /lightvision off - normal.");
            Local(me, "/jester on | off - one random crewmate is the Jester and wins by getting voted out.");
            Local(me, "/impchat on | off - private impostor chat. Impostors type /cmd message; you type // message if you are an impostor.");
            Local(me, "/settings - post the rules that are ON in the chat for everyone.  /help - this list.");
            Local(me, "/time, /vision, /lightvision, /jester and /impchat only work in the lobby. Every change is announced to everyone.");
        }

        // ---------------------------------------------------------------- /time
        private static void Time(PlayerControl me, string arg)
        {
            if (arg.Length == 0)
            {
                foreach (var l in Rules.TimeLines()) Local(me, l);
                Local(me, "In the lobby: /time on | off, /time X (minutes), /time pause on | off.");
                return;
            }
            if (!Gate.InLobby())
            {
                Local(me, "/time only works in the lobby, before the game starts.");
                return;
            }

            if (arg.Equals("on", StringComparison.OrdinalIgnoreCase)) { Plugin.TimeLimitEnabled.Value = true; Announce(Rules.TimeLines()); return; }
            if (arg.Equals("off", StringComparison.OrdinalIgnoreCase)) { Plugin.TimeLimitEnabled.Value = false; Announce(Rules.TimeLines()); return; }

            if (arg.StartsWith("pause", StringComparison.OrdinalIgnoreCase))
            {
                var p = arg.Substring(5).Trim();
                if (p.Equals("on", StringComparison.OrdinalIgnoreCase)) Plugin.PauseDuringMeetings.Value = true;
                else if (p.Equals("off", StringComparison.OrdinalIgnoreCase)) Plugin.PauseDuringMeetings.Value = false;
                else
                {
                    Local(me, "Usage: /time pause on  or  /time pause off");
                    return;
                }
                Announce(Rules.TimeLines());
                return;
            }

            double v;
            if (!TryNum(arg, out v))
            {
                Local(me, "Usage: /time on | off,  /time 18 (minutes),  /time pause on | off");
                return;
            }

            var clamped = Math.Min(MaxMinutes, Math.Max(MinMinutes, v));
            Plugin.TimeLimitMinutes.Value = (float)clamped;
            Plugin.TimeLimitEnabled.Value = true;
            if (Math.Abs(clamped - v) > 0.0001) Local(me, "(allowed range is " + Rules.Num(MinMinutes) + "-" + Rules.Num(MaxMinutes) + " min)");
            Announce(Rules.TimeLines());
        }

        // ---------------------------------------------------------------- /vision
        private static void Vision(PlayerControl me, string arg)
        {
            float lo = Lowest(), hi = Plugin.VisionMax.Value;

            if (arg.Length == 0)
            {
                foreach (var l in Rules.VisionLines()) Local(me, l);
                Local(me, "Usage: /vision 0.15 (allowed " + Rules.Num(lo) + "-" + Rules.Num(hi) + "), or /vision off");
                return;
            }
            if (!Gate.InLobby())
            {
                Local(me, "/vision only works in the lobby, before the game starts.");
                return;
            }
            if (!VisionOverride.Available)
            {
                Local(me, "/vision only works in Classic mode.");
                return;
            }

            if (arg.Equals("off", StringComparison.OrdinalIgnoreCase) || arg.Equals("reset", StringComparison.OrdinalIgnoreCase))
            {
                VisionOverride.Clear();
                Announce(Rules.VisionLines());
                return;
            }

            double v;
            if (!TryNum(arg, out v))
            {
                Local(me, "Usage: /vision 0.15   (or /vision off)");
                return;
            }

            var clamped = (float)Math.Min(hi, Math.Max(lo, v));
            VisionOverride.Set(clamped);
            if (Math.Abs(clamped - v) > 0.0001) Local(me, "(allowed range is " + Rules.Num(lo) + "-" + Rules.Num(hi) + ")");
            Announce(Rules.VisionLines());
        }

        // ---------------------------------------------------------------- /lightvision
        private static void LightVision(PlayerControl me, string arg)
        {
            float lo = Lowest(), hi = Plugin.VisionMax.Value;

            if (arg.Length == 0)
            {
                foreach (var l in Rules.LightVisionLines()) Local(me, l);
                Local(me, "Usage: /lightvision 0.05 (allowed " + Range() + ", 0 = pitch black), or /lightvision off");
                return;
            }
            if (!Gate.InLobby())
            {
                Local(me, "/lightvision only works in the lobby, before the game starts.");
                return;
            }
            if (!VisionOverride.Available)
            {
                Local(me, "/lightvision only works in Classic mode.");
                return;
            }

            if (arg.Equals("off", StringComparison.OrdinalIgnoreCase) || arg.Equals("reset", StringComparison.OrdinalIgnoreCase))
            {
                VisionOverride.ClearLight();
                Announce(Rules.LightVisionLines());
                return;
            }

            double v;
            if (!TryNum(arg, out v))
            {
                Local(me, "Usage: /lightvision 0.05   (or /lightvision off)");
                return;
            }

            var clamped = (float)Math.Min(hi, Math.Max(lo, v));
            VisionOverride.SetLight(clamped);
            if (Math.Abs(clamped - v) > 0.0001) Local(me, "(allowed range is " + Range() + ")");
            Announce(Rules.LightVisionLines());
        }

        // Lowest vision allowed by the settings file (never below 0).
        private static float Lowest()
        {
            return Math.Max(0f, Plugin.VisionMin.Value);
        }

        private static string Range()
        {
            return Rules.Num(Lowest()) + "-" + Rules.Num(Plugin.VisionMax.Value);
        }

        // ---------------------------------------------------------------- /jester and /impchat (plain on/off switches)
        private static void Switch(PlayerControl me, string cmd, string arg, Flag flag, Func<List<string>> lines)
        {
            if (arg.Length == 0)
            {
                foreach (var l in lines()) Local(me, l);
                Local(me, "Usage: " + cmd + " on  or  " + cmd + " off");
                return;
            }
            if (!Gate.InLobby())
            {
                Local(me, cmd + " only works in the lobby, before the game starts.");
                return;
            }

            if (arg.Equals("on", StringComparison.OrdinalIgnoreCase)) flag.Value = true;
            else if (arg.Equals("off", StringComparison.OrdinalIgnoreCase)) flag.Value = false;
            else
            {
                Local(me, "Usage: " + cmd + " on  or  " + cmd + " off");
                return;
            }
            Announce(lines());
        }

        // ---------------------------------------------------------------- helpers
        private static bool TryNum(string s, out double v)
        {
            return double.TryParse(s.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out v)
                && !double.IsNaN(v) && !double.IsInfinity(v);
        }

        // Shown to everyone in the chat (queued so lines go out one after another).
        private static void Announce(List<string> lines)
        {
            foreach (var l in lines) ChatLine.Enqueue(l);
            Plugin.L.LogInfo("[cmd] announced " + lines.Count + " line(s) to everyone");
        }

        // Shown only on the host's own screen.
        private static void Local(PlayerControl me, string text)
        {
            try
            {
                DestroyableSingleton<HudManager>.Instance.Chat.AddChat(me, text, false);
            }
            catch (Exception e)
            {
                Plugin.L.LogWarning("[cmd] could not show: " + e.Message);
            }
        }
    }
}
