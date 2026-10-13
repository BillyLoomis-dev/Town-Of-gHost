using System;
using HarmonyLib;
using UnityEngine;

namespace HostRules
{
    // The Town of gHost banner in place of the Among Us logo in the main menu.
    //
    // Config: [Look] ShowMenuBanner. When another mod has already put its own picture there (GreaterAmongUs,
    // BetterAmongUs...), theirs is kept.
    [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.Start))]
    internal static class MenuBanner
    {
        private const string LogoPath = "LeftPanel/Sizer/LOGO-AU";

        // The Among Us logo's picture before any mod touched it this time the menu opened.
        private static int _originalSpriteId;

        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void Prefix()
        {
            _originalSpriteId = 0;
            try
            {
                var logo = GameObject.Find(LogoPath);
                var renderer = logo != null ? logo.GetComponent<SpriteRenderer>() : null;
                if (renderer != null && renderer.sprite != null) _originalSpriteId = renderer.sprite.GetInstanceID();
            }
            catch (Exception)
            {
                // checked again in the postfix
            }
        }

        // Runs after the other mods, so it can see whether one of them already replaced the logo.
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix()
        {
            try
            {
                if (!Plugin.ShowMenuBanner.Value) return;

                var logo = GameObject.Find(LogoPath);
                var renderer = logo != null ? logo.GetComponent<SpriteRenderer>() : null;
                if (renderer == null)
                {
                    Plugin.L.LogWarning("[look] the Among Us logo was not found in the main menu - the banner is not shown");
                    return;
                }

                // Another mod patches the main menu and the logo picture changed since it opened: that mod put its own there.
                var replaced = renderer.sprite != null && renderer.sprite.GetInstanceID() != _originalSpriteId;
                if (replaced && Artwork.PatchedByAnotherMod(AccessTools.Method(typeof(MainMenuManager), nameof(MainMenuManager.Start))))
                {
                    Plugin.L.LogInfo("[look] another mod already shows its own logo in the main menu - the Town of gHost banner is not shown");
                    return;
                }

                var banner = Artwork.Banner;
                if (banner == null) return;

                // Slightly lower and in front of the menu, scaled so the whole banner fits in the left panel.
                var sizer = logo.transform.parent;
                var local = sizer.localPosition;
                sizer.localPosition = new Vector3(local.x, local.y - 0.035f, local.z);
                var world = sizer.position;
                sizer.position = new Vector3(world.x, world.y, -0.5f);
                logo.transform.localScale = new Vector3(0.003f, 0.00225f, 1f);

                renderer.sprite = banner;
            }
            catch (Exception e)
            {
                Plugin.L.LogWarning("[look] could not show the main menu banner: " + e.Message);
            }
        }
    }
}
