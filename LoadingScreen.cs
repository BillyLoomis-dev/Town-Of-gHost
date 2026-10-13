using System;
using HarmonyLib;
using UnityEngine;

namespace HostRules
{
    // The Town of gHost picture while the game loads.
    //
    // The game's start screen plays the Innersloth logo as usual. When the game has finished loading and would normally
    // move on to the main menu, the logo animation is played once more with the Town of gHost picture in place of the
    // Innersloth logo, on a black background. It stays at least ShowSeconds; a mouse click moves on right away.
    //
    // Config: [Look] ShowLoadingPicture. Nothing changes when another mod already customises this screen
    // (GreaterAmongUs, BetterAmongUs...), and if anything goes wrong the normal loading screen simply carries on.
    [HarmonyPatch(typeof(SplashManager))]
    internal static class LoadingScreen
    {
        private const float ShowSeconds = 3f;      // the picture stays at least this long (unless clicked)
        private const float SoundSeconds = 1.8f;   // the replayed animation's sound is stopped after this

        private enum Stage
        {
            Off,      // the game's own loading screen (feature off, another mod, or something failed)
            Waiting,  // Innersloth logo playing, game still loading
            Showing,  // our picture is on screen
            Done      // the main menu is loading
        }

        private static Stage _stage = Stage.Off;
        private static float _shownAt;
        private static bool _soundStopped;

        [HarmonyPatch(nameof(SplashManager.Start))]
        [HarmonyPrefix]
        private static void StartPrefix(SplashManager __instance)
        {
            _stage = Stage.Off;
            try
            {
                if (!Plugin.ShowLoadingPicture.Value) return;

                if (Artwork.PatchedByAnotherMod(AccessTools.Method(typeof(SplashManager), nameof(SplashManager.Update))))
                {
                    Plugin.L.LogInfo("[look] another mod has its own loading screen - the Town of gHost loading picture is not shown");
                    return;
                }

                var anim = __instance.logoAnimFinish;
                var overlay = anim != null ? anim.transform.Find("BlackOverlay") : null;
                if (overlay == null || anim.transform.Find("LogoRoot/ISLogo") == null)
                {
                    Plugin.L.LogWarning("[look] the game's start screen has changed - the loading picture is not shown");
                    return;
                }

                // Read the picture now, while the Innersloth logo plays, so it is ready without a hitch.
                if (Artwork.Loading == null) return;

                // Keep the black background out of the Innersloth logo's run; it is brought back for our picture.
                SetLocalY(overlay, 100f);
                _soundStopped = false;
                _stage = Stage.Waiting;
            }
            catch (Exception e)
            {
                _stage = Stage.Off;
                Plugin.L.LogWarning("[look] loading picture disabled: " + e.Message);
            }
        }

        [HarmonyPatch(nameof(SplashManager.Update))]
        [HarmonyPrefix]
        private static bool UpdatePrefix(SplashManager __instance)
        {
            if (_stage == Stage.Off) return true;

            try
            {
                switch (_stage)
                {
                    case Stage.Waiting:
                        // The moment the game itself would leave the start screen.
                        if (__instance.doneLoadingRefdata && !__instance.startedSceneLoad
                            && Time.time - __instance.startTime > __instance.minimumSecondsBeforeSceneChange)
                        {
                            Show(__instance);
                        }
                        return false;

                    case Stage.Showing:
                        var shown = Time.time - _shownAt;
                        if (!_soundStopped && shown > SoundSeconds) StopSound(__instance);

                        var clicked = Input.GetKeyDown(KeyCode.Mouse0) || Input.GetKeyDown(KeyCode.Mouse1);
                        if (shown >= ShowSeconds || clicked) GoToMainMenu(__instance);
                        return false;

                    default:
                        return false;
                }
            }
            catch (Exception e)
            {
                // Never hold the game up: hand back to the game's own loading screen.
                Plugin.L.LogWarning("[look] loading picture stopped, continuing normally: " + e.Message);
                _stage = Stage.Off;
                return true;
            }
        }

        private static void Show(SplashManager splash)
        {
            var anim = splash.logoAnimFinish;

            // Restart the logo animation.
            anim.gameObject.SetActive(false);
            anim.gameObject.SetActive(true);

            // Put our picture where the Innersloth logo is. A renamed copy is used so the animation, which
            // drives the original logo object, doesn't put the Innersloth logo back.
            var innersloth = anim.transform.Find("LogoRoot/ISLogo").gameObject;
            var picture = UnityEngine.Object.Instantiate(innersloth, innersloth.transform.parent);
            picture.name = "TownOfGhostPicture";
            picture.GetComponent<SpriteRenderer>().sprite = Artwork.Loading;
            UnityEngine.Object.Destroy(innersloth);

            // Black background behind it.
            SetLocalY(anim.transform.Find("BlackOverlay"), 0f);

            splash.startTime = Time.time;
            _shownAt = Time.time;
            _stage = Stage.Showing;
        }

        private static void StopSound(SplashManager splash)
        {
            _soundStopped = true;
            var sound = splash.logoAnimFinish.GetComponent<AudioSource>();
            if (sound != null) UnityEngine.Object.Destroy(sound);
        }

        private static void GoToMainMenu(SplashManager splash)
        {
            _stage = Stage.Done;
            splash.sceneChanger.AllowFinishLoadingScene();
            splash.startedSceneLoad = true;
            splash.loadingObject.SetActive(true);
        }

        private static void SetLocalY(Transform transform, float y)
        {
            var p = transform.localPosition;
            transform.localPosition = new Vector3(p.x, y, p.z);
        }
    }
}
