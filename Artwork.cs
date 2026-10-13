using System;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HostRules
{
    // The Town of gHost pictures, built into TownOfGHost.dll (folder Resources\ in the source):
    //   TownOfGhost-Loading.png - shown while the game loads (LoadingScreen.cs)
    //   TownOfGhost-Banner.jpg  - shown in place of the Among Us logo in the main menu (MenuBanner.cs)
    // Each picture is read once and kept for the whole session.
    internal static class Artwork
    {
        private const string LoadingResource = "TownOfGHost.Resources.TownOfGhost-Loading.png";
        private const string BannerResource = "TownOfGHost.Resources.TownOfGhost-Banner.jpg";

        // Pixels per world unit. These give the same on-screen size as GreaterAmongUs' pictures of the same dimensions.
        private const float LoadingPixelsPerUnit = 150f;
        private const float BannerPixelsPerUnit = 1f;

        private static Sprite _loading;
        private static Sprite _banner;
        private static bool _loadingFailed;
        private static bool _bannerFailed;

        public static Sprite Loading
        {
            get
            {
                if (_loading == null && !_loadingFailed)
                {
                    _loading = Load(LoadingResource, LoadingPixelsPerUnit);
                    _loadingFailed = _loading == null;
                }
                return _loading;
            }
        }

        public static Sprite Banner
        {
            get
            {
                if (_banner == null && !_bannerFailed)
                {
                    _banner = Load(BannerResource, BannerPixelsPerUnit);
                    _bannerFailed = _banner == null;
                }
                return _banner;
            }
        }

        private static Sprite Load(string resource, float pixelsPerUnit)
        {
            try
            {
                byte[] bytes;
                using (var stream = typeof(Artwork).Assembly.GetManifestResourceStream(resource))
                {
                    if (stream == null)
                    {
                        Plugin.L.LogWarning("[look] picture missing from the plugin: " + resource);
                        return null;
                    }
                    using (var memory = new MemoryStream())
                    {
                        stream.CopyTo(memory);
                        bytes = memory.ToArray();
                    }
                }

                var texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                if (!ImageConversion.LoadImage(texture, bytes, false))
                {
                    Plugin.L.LogWarning("[look] could not read the picture " + resource);
                    return null;
                }
                // Keep them when scenes change (they are reused every time the main menu opens).
                texture.hideFlags |= HideFlags.HideAndDontSave;

                var sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), pixelsPerUnit);
                sprite.hideFlags |= HideFlags.HideAndDontSave;
                return sprite;
            }
            catch (Exception e)
            {
                Plugin.L.LogWarning("[look] could not load the picture " + resource + ": " + e.Message);
                return null;
            }
        }

        // True when a mod other than Town of gHost has patched this game method (for example GreaterAmongUs or
        // BetterAmongUs with their own loading screen). Then Town of gHost leaves it to them instead of fighting over it.
        public static bool PatchedByAnotherMod(MethodBase method)
        {
            if (method == null) return false;
            var info = Harmony.GetPatchInfo(method);
            return info != null && info.Owners.Any(owner => owner != Plugin.Id);
        }
    }
}
