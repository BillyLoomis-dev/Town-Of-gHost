using System;
using HarmonyLib;
using UnityEngine;

namespace HostRules
{
    // Town of gHost's name, version and website in the top-right corner, under the game's ping, in the lobby and
    // during games. Only on your own screen.
    //
    // Config: [Look] ShowCornerInfo. When another mod replaces the ping display (GreaterAmongUs, BetterAmongUs...),
    // the game's ping text is switched off and that mod's corner text is shown instead.
    [HarmonyPatch(typeof(PingTracker), nameof(PingTracker.Update))]
    internal static class CornerInfo
    {
        private const string Website = "github.com/BillyLoomis-dev/Town-Of-gHost";

        // Added under the ping line. The game rewrites the ping line itself; ours is only added when it isn't there yet.
        private static readonly string Lines =
            "\n<size=75%><color=#E0242E>Town of gHost v" + Plugin.Version + "</color></size>"
            + "\n<size=52%><color=#8A8A8A>" + Website + "</color></size>";

        private static bool _styled;
        private static bool _warned;

        [HarmonyPostfix]
        private static void Postfix(PingTracker __instance)
        {
            try
            {
                if (!Plugin.ShowCornerInfo.Value) return;

                var text = __instance.text;
                if (text == null) return;

                var current = text.text ?? string.Empty;
                if (!current.EndsWith(Lines, StringComparison.Ordinal)) text.text = current + Lines;

                if (!_styled)
                {
                    text.outlineWidth = 0.3f; // readable on bright parts of the map
                    _styled = true;
                }

                // Top-right, left of the corner buttons; a bit further left while the chat button is there too.
                var position = __instance.aspectPosition;
                if (position != null)
                {
                    position.Alignment = AspectPosition.EdgeAlignments.RightTop;
                    position.DistanceFromEdge = new Vector3(ChatButtonShown() ? 4.6f : 4f, 0.1f, -5f);
                }
            }
            catch (Exception e)
            {
                if (_warned) return;
                _warned = true;
                Plugin.L.LogWarning("[look] could not show the Town of gHost corner text: " + e.Message);
            }
        }

        private static bool ChatButtonShown()
        {
            if (!HudManager.InstanceExists) return false;
            var chat = HudManager.Instance.Chat;
            return chat != null && chat.chatButton != null && chat.chatButton.gameObject.activeInHierarchy;
        }
    }
}
