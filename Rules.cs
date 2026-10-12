using System.Collections.Generic;
using System.Globalization;

namespace HostRules
{
    // The text shown to everyone whenever a rule is added or changed, and by /settings.
    // Every line is kept short so it fits in one chat message.
    internal static class Rules
    {
        public static string Num(double v)
        {
            return v.ToString("0.##", CultureInfo.InvariantCulture);
        }

        public static List<string> TimeLines()
        {
            var l = new List<string>();
            if (!Plugin.TimeLimitEnabled.Value)
            {
                l.Add("Time limit: OFF.");
                return l;
            }
            l.Add("Time limit for tasks before the Impostor team wins: " + Num(Plugin.TimeLimitMinutes.Value) + " min.");
            l.Add("Timer paused during meetings: " + (Plugin.PauseDuringMeetings.Value ? "ON." : "OFF."));
            return l;
        }

        public static List<string> JesterLines()
        {
            var l = new List<string>();
            if (!Plugin.JesterEnabled.Value)
            {
                l.Add("Jester: OFF.");
                return l;
            }
            l.Add("Jester: ON. One random crewmate is secretly the Jester. Jester wins by getting voted out.");
            l.Add("The Jester's tasks are fake: they never count toward the crew's task win.");
            l.Add("The Jester sees \"Jester\" above his head and a big J on his body.");
            l.Add("After a Jester win, the colors of the Jester and the Impostors are posted in the lobby chat.");
            return l;
        }

        public static List<string> ImpChatLines()
        {
            var l = new List<string>();
            if (!Plugin.ImpostorChatEnabled.Value)
            {
                l.Add("Impostor chat: OFF.");
                return l;
            }
            l.Add("Impostor chat: ON. Only impostors and dead players can read it.");
            l.Add("IMPOSTORS: in a meeting type /cmd your message (not //) to talk to your team.");
            return l;
        }

        public static List<string> VisionLines()
        {
            var l = new List<string>();
            var v = VisionOverride.Value;
            if (!v.HasValue)
            {
                l.Add("Crew vision: default.");
                return l;
            }
            l.Add("Crew vision: " + Num(v.Value) + "x (normal vision is 1x).");
            return l;
        }

        // Only the rules that are switched on, with all their details.
        public static List<string> SettingsLines()
        {
            var body = new List<string>();
            if (Plugin.TimeLimitEnabled.Value) body.AddRange(TimeLines());
            if (Plugin.JesterEnabled.Value) body.AddRange(JesterLines());
            if (Plugin.ImpostorChatEnabled.Value) body.AddRange(ImpChatLines());
            if (VisionOverride.Value.HasValue) body.AddRange(VisionLines());

            var l = new List<string>();
            if (body.Count == 0)
            {
                l.Add("Lobby settings: no special rules are on.");
                return l;
            }
            l.Add("Lobby settings that are ON:");
            l.AddRange(body);
            return l;
        }
    }
}
