using System;
using HarmonyLib;

namespace HostRules
{
    // /pet on: a player's pet disappears the moment he dies (killed, voted out, anything), so it can't stay by his body
    // and point the crew to it. Same idea as Town of Host Enhanced's "remove pets at dead players".
    // The host takes the pet off with the game's own message, so everyone's game shows it. It doesn't come back for
    // the rest of that game; in the lobby everyone has his own pet again.
    internal static class PetRules
    {
        public static void OnDeath(PlayerControl pc)
        {
            if (!Plugin.DeadPetRemovalEnabled.Value || pc == null) return;
            if (JesterWin.Running) return;

            var d = pc.Data;
            if (d == null || d.Disconnected || !d.IsDead) return;
            var o = d.DefaultOutfit;
            if (o == null || string.IsNullOrEmpty(o.PetId) || o.PetId == Looks.NoPet) return;

            try
            {
                Looks.SendPet(pc, Looks.NoPet);
                Plugin.L.LogInfo("[pet] a dead player's pet was removed");
            }
            catch (Exception e)
            {
                Plugin.L.LogWarning("[pet] could not remove a dead player's pet: " + e.Message);
            }
        }
    }

    // Every death goes through Die: kills, ejections, and any other way a player dies.
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Die))]
    internal static class Patch_PetOnDeath
    {
        public static void Postfix(PlayerControl __instance)
        {
            try
            {
                if (!Gate.ActiveHost()) return;
                PetRules.OnDeath(__instance);
            }
            catch (Exception e) { Plugin.L.LogError("[pet] " + e); }
        }
    }
}
