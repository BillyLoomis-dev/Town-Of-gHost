using System;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace HostRules
{
    // The Jester and the Judge (host only, while a Jester is in the round).
    //
    // The game's rule: a Judge who overrules an Impostor ejects that Impostor; a Judge who overrules a crewmate is
    // ejected HIMSELF instead. The game doesn't know about the Jester, so to the game he is just a crewmate.
    //  - A Judge overrules the Jester: the game would eject the Judge. Town of gHost ejects the Jester instead,
    //    and he wins (the Judge stays alive; his one overrule is used up).
    //  - The Jester is the Judge and overrules a crewmate: the game ejects him for the wrong judgment. That doesn't
    //    count as a Jester win: he's simply out and the round goes on.
    //
    // At the end of every meeting the host's game decides who leaves (overrules included) and sends that one result
    // to everyone with RpcVotingComplete. That is where this steps in.
    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.RpcVotingComplete))]
    internal static class JudgeRules
    {
        private static bool _resending;

        // The meeting result being sent right now comes from the Jester's own overrule.
        public static bool JestersOwnOverrule { get; private set; }

        [HarmonyPrefix]
        private static bool Prefix(MeetingHud __instance, Il2CppStructArray<MeetingHud.VoterState> states,
            NetworkedPlayerInfo exiled, bool tie, bool wasOverruled, ushort overruleNonce)
        {
            if (_resending) return true;
            JestersOwnOverrule = false;

            try
            {
                if (!wasOverruled || !Gate.ActiveHost() || !Jester.Active) return true;

                byte judge, target;
                if (!TryFindOverrule(__instance, overruleNonce, out judge, out target))
                {
                    Plugin.L.LogWarning("[jester] a Judge overruled the vote, but the overrule was not found - meeting result unchanged");
                    return true;
                }

                var jesterId = Jester.JesterId;

                if (judge == jesterId)
                {
                    JestersOwnOverrule = true;
                    Plugin.L.LogInfo("[jester] this meeting result comes from the Jester's own overrule - it can't make him win");
                    return true;
                }

                if (target == jesterId && exiled != null && exiled.PlayerId == judge)
                {
                    var data = GameData.Instance;
                    var jesterData = data != null ? data.GetPlayerById(jesterId) : null;
                    if (jesterData == null || jesterData.IsDead || jesterData.Disconnected) return true;

                    Plugin.L.LogInfo("[jester] a Judge overruled the Jester: the Jester is ejected instead of the Judge");
                    _resending = true;
                    try
                    {
                        __instance.RpcVotingComplete(states, jesterData, tie, wasOverruled, overruleNonce);
                    }
                    finally
                    {
                        _resending = false;
                    }
                    return false;
                }
            }
            catch (Exception e)
            {
                Plugin.L.LogError("[jester] Judge check failed, meeting result unchanged: " + e);
                _resending = false;
            }
            return true;
        }

        // The overrule behind this meeting result: first from the meeting's overrule list (by its number),
        // otherwise from the game's own "which overrule won" check.
        private static bool TryFindOverrule(MeetingHud meeting, ushort nonce, out byte judge, out byte target)
        {
            judge = 0;
            target = 0;

            var queue = meeting.judgeOverrulesQueue;
            if (queue != null)
            {
                for (int i = 0; i < queue.Count; i++)
                {
                    var entry = queue[i];
                    if (entry == null || entry.OverruleNonce != nonce) continue;
                    judge = entry.JudgePlayerId.Value;
                    target = entry.OverruledPlayerId.Value;
                    return true;
                }
            }

            try
            {
                JudgeOverrule winning;
                NetworkedPlayerInfo judgeInfo;
                NetworkedPlayerInfo targetInfo;
                if (meeting.TryGetWinningOverrule(out winning, out judgeInfo, out targetInfo) && judgeInfo != null && targetInfo != null)
                {
                    judge = judgeInfo.PlayerId;
                    target = targetInfo.PlayerId;
                    return true;
                }
            }
            catch (Exception e)
            {
                Plugin.L.LogWarning("[jester] the game's overrule check failed: " + e.Message);
            }
            return false;
        }
    }
}
