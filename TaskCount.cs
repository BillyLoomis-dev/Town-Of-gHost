using System;

namespace HostRules
{
    // Fake tasks for the Jester, the same idea as Town of Host: after the game counts everyone's tasks, the Jester's
    // tasks are taken out of the total. The crew still wins by finishing THEIR tasks; whatever the Jester does
    // never counts. On a vanilla Jester's own game his tasks can still be clicked (Town of Host is the same), but they
    // never move the crew closer to a task win. If the host is the Jester, the host's tasks are greyed out
    // (see Patch_HostJesterTasks).
    internal static class TaskCount
    {
        private static bool _busy;

        // Called after the game's own task count.
        public static void Adjust(GameData data)
        {
            if (!Jester.Active || data == null) return;

            var info = data.GetPlayerById(Jester.JesterId);
            if (info == null || info.Disconnected || info.Tasks == null) return; // the game already leaves out disconnected players

            int total = 0, done = 0;
            for (int i = 0; i < info.Tasks.Count; i++)
            {
                var t = info.Tasks[i];
                if (t == null) continue;
                total++;
                if (t.Complete) done++;
            }
            if (total == 0) return;

            // Only take out what the game actually counted for him (dead or disconnected players may not count).
            if (data.TotalTasks < total) return;

            int newTotal = data.TotalTasks - total;
            int newDone = Math.Max(0, data.CompletedTasks - done);

            // Never leave the crew at "0 of 0", which would read as an instant task win.
            if (newTotal <= 0)
            {
                newTotal = 1;
                newDone = 0;
            }

            data.TotalTasks = newTotal;
            data.CompletedTasks = Math.Min(newDone, newTotal);
        }

        // Re-count now (e.g. right after the Jester was picked, or after a task was completed).
        public static void Refresh()
        {
            if (_busy) return;
            var data = GameData.Instance;
            if (data == null) return;
            _busy = true;
            try { data.RecomputeTaskCounts(); }
            catch (Exception e) { Plugin.L.LogWarning("[tasks] recount failed: " + e.Message); }
            finally { _busy = false; }
        }
    }
}
