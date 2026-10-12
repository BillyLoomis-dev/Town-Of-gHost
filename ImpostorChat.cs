using System;
using System.Collections.Generic;

namespace HostRules
{
    // Impostor-only chat, same rules as the old server:
    //  - readers: every impostor (alive or dead) plus every DEAD crewmate; living crewmates never see it
    //  - writers: impostors only (alive or dead). Everyone else's "secret" lines are dropped.
    //  - the sender gets the red line back too, so they can see it went to the team
    //  - each line is delivered as the RECIPIENT'S OWN chat, because a vanilla client hides a dead player's
    //    chat from the living; this way all alive/dead combinations render the same
    // Non-host impostors must type  /cmd message  : Innersloth routes those (and only those) chat lines to the
    // host alone, in lobbies registered with the +25 flag. The host itself types  // message.
    internal static class ImpostorChat
    {
        private const string Red = "#FF5555";
        private static readonly Dictionary<byte, DateTime> Warned = new Dictionary<byte, DateTime>();

        public static void Reset()
        {
            Warned.Clear();
        }

        public static bool IsImpostor(PlayerControl pc)
        {
            if (pc == null) return false;
            var d = pc.Data;
            if (d == null) return false;
            var role = d.Role;
            return role != null && role.IsImpostor;
        }

        // A "/cmd ..." line arrived from another player (only the host receives these).
        public static void FromRemote(PlayerControl sender, string body)
        {
            if (!Plugin.ImpostorChatEnabled.Value) return;
            if (!IsImpostor(sender))
            {
                Plugin.L.LogInfo("[impchat] ignored a /cmd line from a non-impostor");
                return;
            }
            Send(sender, body);
        }

        // An impostor typed "//" but, being a normal client, it already went out to everyone.
        public static void WarnLeak(PlayerControl sender)
        {
            if (!Plugin.ImpostorChatEnabled.Value || sender == null) return;
            var id = sender.PlayerId;
            DateTime last;
            if (Warned.TryGetValue(id, out last) && (DateTime.UtcNow - last).TotalSeconds < 15) return;
            Warned[id] = DateTime.UtcNow;
            Deliver(sender, "That line went to EVERYONE. Impostors: type /cmd your message (not //) to talk privately.");
        }

        public static void Send(PlayerControl sender, string body)
        {
            if (!Plugin.ImpostorChatEnabled.Value) return;

            body = Commands.Strip(body).Trim();
            if (body.Length == 0) return;
            if (body.Length > 100) body = body.Substring(0, 100);

            var name = sender.Data != null ? Commands.Strip(sender.Data.PlayerName) : "?";
            if (string.IsNullOrWhiteSpace(name)) name = "?";
            if (name.Length > 14) name = name.Substring(0, 14);

            var line = "<color=" + Red + ">[IMP] " + name + ": " + body + "</color>";

            var all = PlayerControl.AllPlayerControls;
            if (all == null) return;

            int sent = 0;
            for (int i = 0; i < all.Count; i++)
            {
                var pc = all[i];
                if (pc == null) continue;
                var d = pc.Data;
                if (d == null || d.Disconnected) continue;

                bool imp = d.Role != null && d.Role.IsImpostor;
                if (!imp && !d.IsDead) continue; // living crewmate: never sees the impostor channel

                Deliver(pc, line);
                sent++;
            }
            Plugin.L.LogInfo("[impchat] delivered to " + sent + " player(s)");
        }

        // Show a line only to this one player.
        private static void Deliver(PlayerControl target, string line)
        {
            try
            {
                if (target == PlayerControl.LocalPlayer)
                {
                    DestroyableSingleton<HudManager>.Instance.Chat.AddChat(target, line, false);
                    return;
                }

                var client = AmongUsClient.Instance;
                var writer = client.StartRpcImmediately(target.NetId, (byte)RpcCalls.SendChat, Hazel.SendOption.Reliable, target.OwnerId);
                writer.Write(line);
                client.FinishRpcImmediately(writer);
            }
            catch (Exception e)
            {
                Plugin.L.LogWarning("[impchat] could not deliver: " + e.Message);
            }
        }
    }
}
