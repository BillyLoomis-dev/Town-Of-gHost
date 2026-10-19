using System;

namespace HostRules
{
    // Which team a player is on, also for players who already left the game or are dead.
    internal static class Teams
    {
        public static bool IsImpostor(NetworkedPlayerInfo info)
        {
            if (info == null) return false;
            try
            {
                if (info.Role != null) return info.Role.IsImpostor;
            }
            catch (Exception)
            {
                // fall back to the role numbers below
            }

            if (RoleManager.IsImpostorRole(info.RoleType)) return true;
            var alive = info.RoleWhenAlive;
            return alive.HasValue && RoleManager.IsImpostorRole(alive.Value);
        }
    }
}
