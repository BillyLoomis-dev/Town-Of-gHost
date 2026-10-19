using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace HostRules
{
    // A simple on/off switch that lives only in memory (not saved to the config file).
    internal sealed class Flag
    {
        public bool Value;
    }

    // Town of GHost: host-only rules for private Among Us games. Only the HOST needs this plugin; it never acts when you are not the host.
    // Every special rule (time limit, Jester, impostor chat, vision) is OFF in a new lobby and is switched on by the host
    // with chat commands (type /help in the lobby). The choices stay on after a game is over.
    [BepInPlugin(Id, "Town of GHost", Version)]
    [BepInProcess("Among Us.exe")]
    public class Plugin : BasePlugin
    {
        public const string Id = "local.townofghost";
        public const string Version = "2.8.1";

        internal static ManualLogSource L;

        // Saved in the config file (details, not on/off switches)
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<bool> CloseConsoleWhenHosting;
        internal static ConfigEntry<float> CloseConsoleAfterSeconds;
        internal static ConfigEntry<float> TimeLimitMinutes;
        internal static ConfigEntry<bool> PauseDuringMeetings;
        internal static ConfigEntry<bool> ShowTimeInChat;
        internal static ConfigEntry<bool> LivingSenderWhenHostDead;
        internal static ConfigEntry<bool> RegisterHostOnlyFlag;
        internal static ConfigEntry<bool> HostCanBeJester;
        internal static ConfigEntry<bool> JesterSendInfoId;
        internal static ConfigEntry<bool> JesterDebugLog;
        internal static ConfigEntry<bool> JesterBadge;
        internal static ConfigEntry<float> JesterBadgeSize;
        internal static ConfigEntry<float> JesterBadgeOffset;
        internal static ConfigEntry<float> VisionMin;
        internal static ConfigEntry<float> VisionMax;
        internal static ConfigEntry<int> SettingsVersion;
        internal static ConfigEntry<bool> LobbyKeepAlive;
        internal static ConfigEntry<float> LobbyKeepAliveMaxMinutes;
        internal static ConfigEntry<bool> ShowLoadingPicture;
        internal static ConfigEntry<bool> ShowMenuBanner;
        internal static ConfigEntry<bool> ShowCornerInfo;

        // Feature switches: in memory only, OFF every time a new lobby is created, kept on between games of the same lobby.
        internal static readonly Flag TimeLimitEnabled = new Flag();
        internal static readonly Flag JesterEnabled = new Flag();
        internal static readonly Flag ImpostorChatEnabled = new Flag();

        internal static void ResetSwitches()
        {
            TimeLimitEnabled.Value = false;
            JesterEnabled.Value = false;
            ImpostorChatEnabled.Value = false;
        }

        public override void Load()
        {
            L = Log;

            Enabled = Config.Bind("General", "Enabled", true,
                "Master switch. Everything in this plugin only ever acts while YOU are the host of an online lobby.");
            CloseConsoleAfterSeconds = Config.Bind("General", "CloseConsoleAfterSeconds", 3f,
                "Close the BepInEx console window this many seconds after the plugin loads (at most 4; it keeps retrying until about 9 seconds), to protect live information. "
                + "BepInEx's console is also switched off for the following launches, so no window opens at all. "
                + "-1 = keep the console (it opens normally again from the next launch), for debugging. Logs always go to LogOutput.log.");
            CloseConsoleWhenHosting = Config.Bind("General", "CloseConsoleWhenHosting", true,
                "Close the BepInEx console window as soon as you create a lobby, to protect live information (lobby code, player info). Logs still go to LogOutput.log. Set to false when you need the console for debugging.");
            RegisterHostOnlyFlag = Config.Bind("Network", "RegisterHostOnlyFlag", true,
                "Add Innersloth's +25 host-only flag while hosting (needed so the host can end the game and for impostor chat). Set to false to test whether hosting works without it.");

            TimeLimitMinutes = Config.Bind("Timer", "TimeLimitMinutes", 18f,
                "Minutes of playable time the crew gets to finish all tasks (when the time limit is switched on with /time on). /time X changes this value and saves it.");
            PauseDuringMeetings = Config.Bind("Timer", "PauseDuringMeetings", true,
                "true = the time limit clock stops during meetings; false = it keeps running. Switch it in the lobby with /time pause on  or  /time pause off.");
            ShowTimeInChat = Config.Bind("Timer", "ShowTimeInChat", true,
                "Post 'Tasks time left M:SS' in chat at the start of every meeting (when the time limit is on).");
            LivingSenderWhenHostDead = Config.Bind("Timer", "LivingSenderWhenHostDead", true,
                "If the host is dead, dead players' chat only reaches ghosts, so send the line as the first living player instead.");

            HostCanBeJester = Config.Bind("Jester", "HostCanBeJester", true,
                "If true the host can also be picked as the Jester (the host's tag is changed on the host's own screen only).");

            JesterSendInfoId = Config.Bind("Jester", "SendPlayerInfoId", true,
                "Put the player-info id in front of the name in the private name message (needed by recent game versions). Set to false only for testing.");
            JesterDebugLog = Config.Bind("Jester", "DebugLog", false,
                "Write extra Jester details to the log. WARNING: it can reveal whether the host is the Jester, so keep it false when you play.");

            JesterBadge = Config.Bind("Jester", "BigJOnBody", true,
                "Show a big 'J' on the Jester's own body (only he sees it), under the 'Jester' above his head.");
            JesterBadgeSize = Config.Bind("Jester", "BigJSizePercent", 250f,
                "Size of the big 'J' in percent of the normal name size. Aim for about half the player's height.");
            JesterBadgeOffset = Config.Bind("Jester", "BigJOffsetEm", -9f,
                "How far the big 'J' is moved down from the name line (in name-line heights; more negative = lower). Aim for the middle of the body.");

            VisionMin = Config.Bind("Vision", "VisionMin", 0f, "Lowest crewmate vision the host can set with /vision and /lightvision. 0 = pitch black.");
            VisionMax = Config.Bind("Vision", "VisionMax", 0.5f, "Highest crewmate vision the host can set with /vision and /lightvision.");

            LobbyKeepAlive = Config.Bind("Lobby", "LobbyKeepAlive", true,
                "When the server warns that the lobby is about to close for inactivity, accept the extension automatically (the game's own 'extend' feature). The server decides how many extensions it offers.");
            LobbyKeepAliveMaxMinutes = Config.Bind("Lobby", "LobbyKeepAliveMaxMinutes", 0f,
                "Stop extending after the lobby has been waiting this many minutes. 0 = unlimited (keep extending as long as the server offers).");

            ShowLoadingPicture = Config.Bind("Look", "ShowLoadingPicture", true,
                "Show the Town of gHost picture while the game loads (after the Innersloth logo, at least 3 seconds; click to skip). Not shown when another mod has its own loading screen.");
            ShowMenuBanner = Config.Bind("Look", "ShowMenuBanner", true,
                "Show the Town of gHost banner in place of the Among Us logo in the main menu. Not shown when another mod already puts its own logo there.");
            ShowCornerInfo = Config.Bind("Look", "ShowCornerInfo", true,
                "Show 'Town of gHost' with its version and website in the top-right corner, under the ping (lobby and games, only on your screen).");

            // One-time updates of settings files made by older versions.
            SettingsVersion = Config.Bind("General", "SettingsVersion", 0,
                "Used by Town of gHost to update settings from older versions once. Don't change it.");
            if (SettingsVersion.Value < 280)
            {
                // 2.8.0: vision can go down to 0. Move the old default (0.05) along; a value you chose stays.
                if (Math.Abs(VisionMin.Value - 0.05f) < 0.0001f) VisionMin.Value = 0f;
                SettingsVersion.Value = 280;
            }

            ConsoleGuard.Start();
            new Harmony(Id).PatchAll();
            L.LogInfo($"Town of GHost {Version} loaded (enabled={Enabled.Value}); all special rules start OFF - type /help in your lobby");
        }
    }
}
