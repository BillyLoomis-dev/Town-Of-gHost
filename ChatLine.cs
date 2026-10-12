using System;
using System.Collections.Generic;

namespace HostRules
{
    internal static class ChatLine
    {
        // "Tasks time left 12:05" - same wording as the old server. Uses the SAME limit setting as the real timer.
        public static void PostTimeLeft()
        {
            var text = "Tasks time left " + TaskTimer.Fmt(TaskTimer.Remaining());
            PostPublic(text);
            Plugin.L.LogInfo("[chat] time line sent: '" + text + "' (limit " + TaskTimer.LimitMinutes.ToString("0.##") + " min)");
        }

        // Lines for everyone, sent one at a time with a short gap so a burst of chat can't trip the server's rate limits.
        private static readonly Queue<string> Pending = new Queue<string>();
        private static DateTime _nextSend = DateTime.MinValue;

        public static void Enqueue(string text)
        {
            Pending.Enqueue(text);
        }

        public static void ClearQueue()
        {
            Pending.Clear();
        }

        // Called every frame while hosting.
        public static void Pump()
        {
            if (Pending.Count == 0) return;
            var now = DateTime.UtcNow;
            if (now < _nextSend) return;
            _nextSend = now.AddMilliseconds(450);
            PostPublic(Pending.Dequeue());
        }

        // A normal chat line that everyone sees (sent as the host, or as a living player if the host is dead).
        public static void PostPublic(string text)
        {
            var sender = PickSender();
            if (sender == null) return;

            var was = Commands.Busy;
            Commands.Busy = true;
            try
            {
                sender.RpcSendChat(text);
            }
            catch (Exception e)
            {
                Plugin.L.LogWarning("[chat] could not post: " + e.Message);
            }
            finally
            {
                Commands.Busy = was;
            }
        }

        // A chat line shown ONLY to this one player (delivered as his own chat line, the same way the impostor chat
        // works, so it shows whether he is alive or dead). For the host it is just added to the host's own chat.
        public static void SendPrivate(PlayerControl target, string text)
        {
            if (target == null) return;
            try
            {
                if (target == PlayerControl.LocalPlayer)
                {
                    DestroyableSingleton<HudManager>.Instance.Chat.AddChat(target, text, false);
                    return;
                }

                var client = AmongUsClient.Instance;
                var writer = client.StartRpcImmediately(target.NetId, (byte)RpcCalls.SendChat, Hazel.SendOption.Reliable, target.OwnerId);
                writer.Write(text);
                client.FinishRpcImmediately(writer);
            }
            catch (Exception e)
            {
                Plugin.L.LogWarning("[chat] could not send a private line: " + e.Message);
            }
        }

        // Dead players' chat only reaches ghosts, so if the host is dead use the first living player.
        private static PlayerControl PickSender()
        {
            var me = PlayerControl.LocalPlayer;
            if (me == null) return null;
            if (!IsDead(me) || !Plugin.LivingSenderWhenHostDead.Value) return me;

            var all = PlayerControl.AllPlayerControls;
            if (all == null) return me;
            for (int i = 0; i < all.Count; i++)
            {
                var p = all[i];
                if (p != null && !IsDead(p)) return p;
            }
            return me;
        }

        private static bool IsDead(PlayerControl p)
        {
            var d = p.Data;
            return d == null || d.IsDead || d.Disconnected;
        }
    }
}
