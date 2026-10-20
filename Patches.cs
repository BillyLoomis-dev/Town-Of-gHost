using System;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using InnerNet;

namespace HostRules
{
    // ---------------------------------------------------------------- required by Innersloth's mod policy

    // Show the "modded game" stamp at all times while this plugin is loaded.
    [HarmonyPatch(typeof(ModManager), nameof(ModManager.LateUpdate))]
    internal static class Patch_ModStamp
    {
        public static void Postfix(ModManager __instance)
        {
            try { __instance.ShowModStamp(); }
            catch (Exception e) { Plugin.L.LogError("[stamp] " + e); }
        }
    }

    // Close the console a few seconds after the game starts (see ConsoleGuard).
    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.Update))]
    internal static class Patch_ConsoleTimer
    {
        public static void Postfix()
        {
            ConsoleGuard.Tick();
            try { JesterWin.Tick(); }
            catch (Exception e) { Plugin.L.LogError("[jester] " + e); }
        }
    }

    // ---------------------------------------------------------------- host registration (+25 flag), hosting ONLY

    // The matchmaker requests (token, FindHost) go out BEFORE the connection is opened, and the server compares
    // versions, so the flag must be on from the very moment "Create game" is confirmed.
    [HarmonyPatch(typeof(CreateGameOptions), nameof(CreateGameOptions.Confirm))]
    internal static class Patch_CreateConfirm
    {
        public static void Prefix()
        {
            try
            {
                ConsoleGuard.CloseForHosting();
                HostFlag.Set(Plugin.Enabled.Value && Plugin.RegisterHostOnlyFlag.Value, "Create game confirmed");
            }
            catch (Exception e) { Plugin.L.LogError("[host] " + e); }
        }
    }

    [HarmonyPatch(typeof(CreateGameOptions), nameof(CreateGameOptions.CoStartGame))]
    internal static class Patch_CreateStart
    {
        public static void Prefix()
        {
            try
            {
                ConsoleGuard.CloseForHosting();
                HostFlag.Set(Plugin.Enabled.Value && Plugin.RegisterHostOnlyFlag.Value, "Create game started");
            }
            catch (Exception e) { Plugin.L.LogError("[host] " + e); }
        }
    }

    // Joining someone else's lobby (or any non-hosting connection) must stay completely vanilla.
    [HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.Connect))]
    internal static class Patch_Connect
    {
        public static void Prefix(MatchMakerModes mode)
        {
            try
            {
                var client = AmongUsClient.Instance;
                var net = client != null ? client.NetworkMode.ToString() : "?";
                Plugin.L.LogInfo("[host] Connect(mode=" + mode + ", network=" + net + ")");

                if (mode != MatchMakerModes.HostAndClient)
                {
                    HostFlag.Set(false, "not hosting");
                    return;
                }
                ConsoleGuard.CloseForHosting();
                bool online = client != null && client.NetworkMode == NetworkModes.OnlineGame;
                HostFlag.Set(Plugin.Enabled.Value && Plugin.RegisterHostOnlyFlag.Value && online, "hosting connection");
                HostFlag.LogVersionInfo();
            }
            catch (Exception e) { Plugin.L.LogError("[host] " + e); }
        }
    }

    // Back at the main menu nothing is being hosted any more.
    [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.Start))]
    internal static class Patch_MainMenu
    {
        public static void Postfix()
        {
            try
            {
                HostFlag.Set(false, "main menu");
                TaskTimer.Reset();
                VisionOverride.ResetAtMenu();
                ImpostorChat.Reset();
                Jester.Reset();
                JesterWin.Reset();
                CommsCamo.Reset();
                Plugin.ResetSwitches();
                ChatLine.ClearQueue();
                Reveal.Clear();
            }
            catch (Exception e) { Plugin.L.LogError("[host] " + e); }
        }
    }

    [HarmonyPatch(typeof(Constants), nameof(Constants.GetBroadcastVersion))]
    internal static class Patch_BroadcastVersion
    {
        private static int _logged;

        public static void Postfix(ref int __result)
        {
            try
            {
                // The revision slot is (version % 50); modded = 25 or more. Only add when not already modded.
                if (HostFlag.Active && __result % 50 < HostFlag.Plus) __result += HostFlag.Plus;
                if (_logged < 12)
                {
                    _logged++;
                    Plugin.L.LogInfo("[host] GetBroadcastVersion -> " + __result + " (flag " + (HostFlag.Active ? "on" : "off") + ")");
                }
            }
            catch (Exception e) { Plugin.L.LogError("[host] " + e); }
        }
    }

    // Tell the game itself that this hosted lobby is modded, so its own logic switches to the host-authority path
    // that the server (which sees the +25 flag) is now expecting. Only while hosting; vanilla otherwise.
    [HarmonyPatch(typeof(Constants), nameof(Constants.IsVersionModded))]
    internal static class Patch_IsVersionModded
    {
        private static int _logged;

        public static bool Prefix(ref bool __result)
        {
            try
            {
                if (!HostFlag.Active) return true;
                __result = true;
                if (_logged < 6)
                {
                    _logged++;
                    Plugin.L.LogInfo("[host] IsVersionModded -> true (hosting)");
                }
                return false;
            }
            catch (Exception e)
            {
                Plugin.L.LogError("[host] " + e);
                return true;
            }
        }
    }

    // If the handshake uses the byte form of the version, keep it in step with the int form.
    // Only touches the bytes when they are exactly the plain version, so it can never corrupt anything else.
    [HarmonyPatch(typeof(Constants), nameof(Constants.GetBroadcastVersionBytes))]
    internal static class Patch_BroadcastVersionBytes
    {
        public static void Postfix(ref Il2CppStructArray<byte> __result)
        {
            try
            {
                if (!HostFlag.Active || __result == null || __result.Length < 4) return;

                var raw = new byte[4];
                for (int i = 0; i < 4; i++) raw[i] = __result[i];

                int current = BitConverter.ToInt32(raw, 0);
                int wanted = Constants.GetBroadcastVersion(); // already includes +25 while hosting
                if (current != wanted - HostFlag.Plus) return;

                var fixedBytes = BitConverter.GetBytes(wanted);
                for (int i = 0; i < 4; i++) __result[i] = fixedBytes[i];
                Plugin.L.LogInfo("[host] version bytes adjusted to " + wanted);
            }
            catch (Exception e) { Plugin.L.LogError("[host] " + e); }
        }
    }

    // Diagnostics: say WHY the connection was closed (kick reason etc.).
    [HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.HandleDisconnect))]
    internal static class Patch_DisconnectLog
    {
        public static void Prefix(DisconnectReasons reason, string stringReason)
        {
            try
            {
                Plugin.L.LogWarning("[net] disconnect: reason=" + reason + " text='" + stringReason + "' flag=" + (HostFlag.Active ? "on" : "off"));
            }
            catch (Exception e) { Plugin.L.LogError("[net] " + e); }
        }
    }

    // ---------------------------------------------------------------- chat commands + impostor chat

    // The host's own outgoing chat: /time, /vision and "// secret" are handled here and never sent on.
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSendChat))]
    internal static class Patch_ChatOutgoing
    {
        public static bool Prefix(PlayerControl __instance, string chatText, ref bool __result)
        {
            try
            {
                if (Commands.Busy || !Gate.ActiveHost()) return true;
                if (__instance != PlayerControl.LocalPlayer) return true;
                if (Commands.HandleHostText(__instance, chatText))
                {
                    __result = true; // tell the chat box the line was handled so it clears itself
                    return false;
                }
            }
            catch (Exception e) { Plugin.L.LogError("[cmd] " + e); }
            return true;
        }
    }

    // Chat arriving at the host. "/cmd ..." lines come ONLY to the host (Innersloth's host-only-mod feature) and
    // are the impostors' private channel; they are never shown on the host's own screen as raw text.
    [HarmonyPatch(typeof(ChatController), nameof(ChatController.AddChat))]
    internal static class Patch_ChatInbound
    {
        public static bool Prefix(PlayerControl sourcePlayer, string chatText)
        {
            try
            {
                if (!HostFlag.Active || !Gate.ActiveHost()) return true;
                if (sourcePlayer == null || string.IsNullOrEmpty(chatText)) return true;
                if (sourcePlayer == PlayerControl.LocalPlayer) return true;

                var t = chatText.TrimStart();
                if (Commands.IsWord(t, "/cmd"))
                {
                    ImpostorChat.FromRemote(sourcePlayer, Commands.Arg(t, "/cmd"));
                    return false;
                }

                if (t.StartsWith("//", StringComparison.Ordinal) && ImpostorChat.IsImpostor(sourcePlayer))
                    ImpostorChat.WarnLeak(sourcePlayer);
            }
            catch (Exception e) { Plugin.L.LogError("[impchat] " + e); }
            return true;
        }
    }

    // Keep the /vision override on top whenever settings are sent, and when the game starts.
    [HarmonyPatch(typeof(LogicOptions), nameof(LogicOptions.SyncOptions))]
    internal static class Patch_SettingsSync
    {
        public static void Prefix()
        {
            try { if (Gate.ActiveHost()) VisionOverride.Reassert(); }
            catch (Exception e) { Plugin.L.LogError("[vision] " + e); }
        }
    }

    [HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.BeginGame))]
    internal static class Patch_BeginGame
    {
        public static void Prefix()
        {
            try
            {
                if (!Gate.ActiveHost()) return;
                VisionOverride.OnGameStart();
                VisionOverride.ReassertAndSync();
            }
            catch (Exception e) { Plugin.L.LogError("[vision] " + e); }
        }
    }

    // ---------------------------------------------------------------- the 18-minute timer

    // Intro finished -> players have control -> start the clock.
    [HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.OnDestroy))]
    internal static class Patch_IntroEnd
    {
        public static void Postfix()
        {
            try
            {
                if (!Gate.ActiveHost() || !Gate.ClassicMode()) return;
                if (Plugin.TimeLimitEnabled.Value) TaskTimer.StartRound(); else TaskTimer.Reset();
                Jester.EnsurePicked(); // normally already picked when roles were handed out
                TaskCount.Refresh();   // tasks are surely assigned by now
            }
            catch (Exception e) { Plugin.L.LogError("[timer] " + e); }
        }
    }

    // A meeting was called (emergency button or body report): pause right away.
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.StartMeeting))]
    internal static class Patch_MeetingCalled
    {
        public static void Prefix()
        {
            try
            {
                if (!Gate.ActiveHost()) return;
                TaskTimer.Pause();
                CommsCamo.OnMeetingCalled(); // real names back first, so the Jester gets his real name too
                Jester.OnMeetingCalled();
            }
            catch (Exception e) { Plugin.L.LogError("[timer] " + e); }
        }
    }

    // The meeting screen is up: tell everyone how much time is left.
    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
    internal static class Patch_MeetingScreen
    {
        public static void Postfix()
        {
            try
            {
                if (!Gate.ActiveHost() || !TaskTimer.Armed) return;
                TaskTimer.NoteMeetingScreenOpened();
                if (Plugin.ShowTimeInChat.Value) ChatLine.PostTimeLeft();
            }
            catch (Exception e) { Plugin.L.LogError("[chat] " + e); }
        }
    }

    // Vote/ejection finished and everyone can move again: resume the clock.
    [HarmonyPatch(typeof(ExileController), nameof(ExileController.ReEnableGameplay))]
    internal static class Patch_ControlBack
    {
        public static void Postfix()
        {
            try
            {
                if (!Gate.ActiveHost()) return;
                TaskTimer.Resume("players can move");
                CommsCamo.OnControlBack();
                Jester.TryEndGame();
                Jester.OnControlBack();
            }
            catch (Exception e) { Plugin.L.LogError("[timer] " + e); }
        }
    }

    // Who was voted out? (runs on the host as well as on the clients)
    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.VotingComplete))]
    internal static class Patch_VotingComplete
    {
        public static void Postfix(NetworkedPlayerInfo exiled)
        {
            try
            {
                if (!Gate.ActiveHost()) return;
                Jester.OnVotingComplete(exiled, JudgeRules.JestersOwnOverrule);
            }
            catch (Exception e) { Plugin.L.LogError("[jester] " + e); }
        }
    }

    // The ejection scene is over: if the Jester was voted out, end the round now.
    [HarmonyPatch(typeof(ExileController), nameof(ExileController.WrapUp))]
    internal static class Patch_ExileWrapUp
    {
        public static void Postfix()
        {
            try
            {
                if (!Gate.ActiveHost()) return;
                Jester.TryEndGame();
            }
            catch (Exception e) { Plugin.L.LogError("[jester] " + e); }
        }
    }

    // If the game re-sets the host's own name text while the host is the Jester, put the tag back.
    [HarmonyPatch(typeof(CosmeticsLayer), nameof(CosmeticsLayer.SetName), typeof(string))]
    internal static class Patch_LayerName
    {
        public static void Postfix(CosmeticsLayer __instance, string nameStr)
        {
            try
            {
                if (!Gate.ActiveHost()) return;
                Jester.OnLayerSetName(__instance, nameStr);
            }
            catch (Exception e) { Plugin.L.LogError("[jester] " + e); }
        }
    }

    [HarmonyPatch(typeof(CosmeticsLayer), nameof(CosmeticsLayer.SetName), typeof(string), typeof(UnityEngine.Vector3), typeof(UnityEngine.Color), typeof(float))]
    internal static class Patch_LayerNameFull
    {
        public static void Postfix(CosmeticsLayer __instance, string nameStr)
        {
            try
            {
                if (!Gate.ActiveHost()) return;
                Jester.OnLayerSetName(__instance, nameStr);
            }
            catch (Exception e) { Plugin.L.LogError("[jester] " + e); }
        }
    }

    // Host-as-Jester: the game refreshes the name text on every player's fixed update; put the tag back right after.
    // Same for a host who is an impostor while comms has everyone nameless: his partners' names stay on his screen.
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
    internal static class Patch_PlayerFixed
    {
        public static void Postfix(PlayerControl __instance)
        {
            try { Jester.OnPlayerFixedUpdate(__instance); }
            catch (Exception e) { Plugin.L.LogError("[jester] " + e); }
            try { if (Gate.ActiveHost()) CommsCamo.OnPlayerFixedUpdate(__instance); }
            catch (Exception e) { Plugin.L.LogError("[comms] " + e); }
        }
    }

    // ---------------------------------------------------------------- Jester: pick at intro start, fake tasks, victory

    // Roles have just been handed out: pick the Jester NOW, before anyone's intro starts, so his private
    // "Jester" name reaches his game before his intro builds its player lineup.
    [HarmonyPatch(typeof(RoleManager), nameof(RoleManager.SelectRoles))]
    internal static class Patch_RolesSelected
    {
        public static void Postfix()
        {
            try
            {
                if (!Gate.ActiveHost() || !Gate.ClassicMode()) return;
                CommsCamo.OnRoundStart(); // real looks saved before anything can change them
                Jester.OnRoundStart();
            }
            catch (Exception e) { Plugin.L.LogError("[jester] " + e); }
        }
    }

    // Fallback: if the pick at role selection did not happen, pick when the intro starts.
    [HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.CoBegin))]
    internal static class Patch_IntroBegin
    {
        public static void Prefix()
        {
            try
            {
                if (!Gate.ActiveHost() || !Gate.ClassicMode()) return;
                CommsCamo.EnsureStarted();
                Jester.EnsurePicked();
            }
            catch (Exception e) { Plugin.L.LogError("[jester] " + e); }
        }
    }

    // Fake tasks: after the game counts tasks, take the Jester's out of the crew's total.
    [HarmonyPatch(typeof(GameData), nameof(GameData.RecomputeTaskCounts))]
    internal static class Patch_TaskRecount
    {
        public static void Postfix(GameData __instance)
        {
            try
            {
                if (!Gate.ActiveHost()) return;
                TaskCount.Adjust(__instance);
            }
            catch (Exception e) { Plugin.L.LogError("[tasks] " + e); }
        }
    }

    // A task was completed: recount, so a task the Jester finishes never counts.
    [HarmonyPatch(typeof(GameData), nameof(GameData.CompleteTask))]
    internal static class Patch_TaskCompleted
    {
        public static void Postfix()
        {
            try
            {
                if (!Gate.ActiveHost() || !Jester.Active) return;
                TaskCount.Refresh();
            }
            catch (Exception e) { Plugin.L.LogError("[tasks] " + e); }
        }
    }

    // Host-as-Jester: grey out the host's own tasks, like Town of Host does on a modded game.
    // Sabotage fixes and other usables stay available (AllowImpostor), only tasks are blocked.
    [HarmonyPatch(typeof(global::Console), nameof(global::Console.CanUse))]
    internal static class Patch_HostJesterTasks
    {
        public static bool Prefix(global::Console __instance, [HarmonyArgument(1)] out bool canUse, [HarmonyArgument(2)] out bool couldUse, ref float __result)
        {
            canUse = couldUse = false;
            try
            {
                if (!Jester.HostIsJester || !Gate.ActiveHost()) return true;
                if (__instance.AllowImpostor) return true;
                __result = float.MaxValue;
                return false;
            }
            catch (Exception)
            {
                return true;
            }
        }
    }

    // While the Jester's victory screen is being prepared, the normal end checks must wait (otherwise changing the
    // impostors' roles would look like "no impostors left" and end the game as a crew win).
    // Same while an ending waits for the real looks to go out (comms camouflage).
    [HarmonyPatch(typeof(LogicGameFlowNormal), nameof(LogicGameFlowNormal.CheckEndCriteria))]
    internal static class Patch_EndCheckHold
    {
        public static bool Prefix()
        {
            try { return !((JesterWin.Running || CommsCamo.HoldingEnd) && Gate.ActiveHost()); }
            catch (Exception) { return true; }
        }
    }

    // Every way the game can end a round goes through RpcEndGame (Impostor majority, sabotage, tasks, our time limit).
    //  - Once the Jester has been voted out, any ending is turned into his win. This matters when voting him out
    //    leaves as many Impostors as crewmates: the game then wants to end the round at once as an Impostor win,
    //    and on some maps (Airship) the ejection scene finishes without the moment the Jester's win normally starts.
    //  - While the Jester's win is being shown, any other ending is held back.
    //  - While comms has everyone grey, the real looks are sent first and the ending follows a moment later, so the
    //    end screen shows everyone's real look.
    [HarmonyPatch(typeof(GameManager), nameof(GameManager.RpcEndGame))]
    internal static class Patch_EndGameGuard
    {
        public static bool Prefix(GameOverReason endReason, bool showAd)
        {
            try
            {
                if (!Gate.ActiveHost() || JesterWin.EndingNow) return true;

                if (JesterWin.Running)
                {
                    Plugin.L.LogInfo("[jester] another ending (" + endReason + ") was held back - the Jester's win is being shown");
                    return false;
                }

                if (Jester.EjectionPending)
                {
                    Plugin.L.LogInfo("[jester] the game tried to end the round (" + endReason + ") after the Jester was voted out - it is the Jester's win");
                    Jester.TryEndGame();
                    return false;
                }
            }
            catch (Exception e) { Plugin.L.LogError("[jester] " + e); }

            try
            {
                if (CommsCamo.HoldEnd(endReason, showAd)) return false;
            }
            catch (Exception e) { Plugin.L.LogError("[comms] " + e); }
            return true;
        }
    }

    // ---------------------------------------------------------------- lobby keep-alive

    [HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.Start))]
    internal static class Patch_LobbyStart
    {
        public static void Postfix()
        {
            try { LobbyKeepAlive.OnLobbyStart(); }
            catch (Exception e) { Plugin.L.LogError("[lobby] " + e); }
            try { if (Gate.ActiveHost()) VisionOverride.OnLobbyStart(); }
            catch (Exception e) { Plugin.L.LogError("[vision] " + e); }
        }
    }

    // The server warned that the lobby is about to close: accept the extension automatically.
    [HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.HandleLobbyTimerExtensionRequest))]
    internal static class Patch_LobbyExpiry
    {
        public static void Postfix(LobbyBehaviour __instance,
            [HarmonyArgument(0)] int timeRemainingSeconds,
            [HarmonyArgument(1)] bool isExtensionAvailable,
            [HarmonyArgument(3)] int extensionId,
            [HarmonyArgument(4)] int extendedTimeSeconds)
        {
            try { LobbyKeepAlive.OnExtensionOffer(__instance, timeRemainingSeconds, isExtensionAvailable, extensionId, extendedTimeSeconds); }
            catch (Exception e) { Plugin.L.LogError("[lobby] " + e); }
        }
    }

    [HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.LobbyTimerExtended))]
    internal static class Patch_LobbyExtended
    {
        public static void Postfix()
        {
            try { Plugin.L.LogInfo("[lobby] the server confirmed the extension"); }
            catch (Exception) { }
        }
    }

    // Per-frame check (and the fallback resume).
    [HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
    internal static class Patch_Tick
    {
        public static void Postfix()
        {
            try
            {
                if (!Gate.ActiveHost()) return;
                TaskTimer.Tick();
                VisionOverride.TickLights();
                CommsCamo.Tick();
                Jester.Tick();
                ChatLine.Pump();
                Reveal.Tick();
            }
            catch (Exception e) { Plugin.L.LogError("[timer] " + e); }
        }
    }

    // The round ended for any reason: forget the timer.
    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameEnd))]
    internal static class Patch_RoundEnded
    {
        public static void Prefix()
        {
            try
            {
                TaskTimer.Reset();
                JesterWin.Reset();
                CommsCamo.Reset();
                VisionOverride.OnGameEnd();
                if (Gate.ActiveHost()) Jester.OnRoundEnd(); else Jester.Reset();
                ImpostorChat.Reset();
            }
            catch (Exception e) { Plugin.L.LogError("[timer] " + e); }
        }
    }
}
