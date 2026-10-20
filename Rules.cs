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
            l.Add("Judge: overruling the Jester ejects HIM (he wins). The Jester can't win by his own overrule.");
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

        public static List<string> CommsLines()
        {
            var l = new List<string>();
            if (!Plugin.CommsCamoEnabled.Value)
            {
                l.Add("Comms camouflage: OFF.");
                return l;
            }
            l.Add("Comms camouflage: ON. While comms is down, everyone is grey with no name, hat, skin or pet.");
            l.Add("Names come back in meetings. Impostors still see each other's names. Bodies stay grey.");
            l.Add("Everyone's real look comes back when comms is fixed.");
            return l;
        }

        public static List<string> PetLines()
        {
            var l = new List<string>();
            if (!Plugin.DeadPetRemovalEnabled.Value)
            {
                l.Add("Pets of dead players: normal.");
                return l;
            }
            l.Add("Pets of dead players: removed. A pet disappears the moment its owner dies.");
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
            l.Add("Crew vision: " + VisionText(v.Value) + " (normal vision is 1x).");
            return l;
        }

        public static List<string> LightVisionLines()
        {
            var l = new List<string>();
            var v = VisionOverride.LightValue;
            if (!v.HasValue)
            {
                l.Add("Crew vision when the lights are out: default.");
                return l;
            }
            l.Add("Crew vision when the lights are out: " + VisionText(v.Value) + ". Impostors keep full vision.");
            return l;
        }

        private static string VisionText(float v)
        {
            return v <= 0f ? "0x (pitch black)" : Num(v) + "x";
        }

        // Only the rules that are switched on, with all their details.
        public static List<string> SettingsLines()
        {
            var body = new List<string>();
            if (Plugin.TimeLimitEnabled.Value) body.AddRange(TimeLines());
            if (Plugin.JesterEnabled.Value) body.AddRange(JesterLines());
            if (Plugin.ImpostorChatEnabled.Value) body.AddRange(ImpChatLines());
            if (Plugin.CommsCamoEnabled.Value) body.AddRange(CommsLines());
            if (Plugin.DeadPetRemovalEnabled.Value) body.AddRange(PetLines());
            if (VisionOverride.Value.HasValue) body.AddRange(VisionLines());
            if (VisionOverride.LightValue.HasValue) body.AddRange(LightVisionLines());

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
