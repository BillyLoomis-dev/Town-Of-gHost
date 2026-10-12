using System;
using AmongUs.GameOptions;

namespace HostRules
{
    // Crewmate vision override set by the host with /vision X (lobby only).
    // CrewLightMod is just a float that every client reads from the synced game settings, so the host can
    // set values below the slider's own minimum. The override is re-applied every time settings are synced
    // and when the game starts, so nudging a slider can't silently undo it.
    internal static class VisionOverride
    {
        private static float? _value;   // null = the host's slider wins
        private static float? _saved;   // the host's own value before the override
        private static bool _applying;

        public static float? Value { get { return _value; } }

        private static NormalGameOptionsV12 Options()
        {
            var m = GameOptionsManager.Instance;
            return m == null ? null : m.currentNormalGameOptions;
        }

        public static bool Available { get { return Options() != null; } }

        public static void Set(float v)
        {
            var o = Options();
            if (o == null) return;
            if (_value == null) _saved = o.CrewLightMod;
            _value = v;
            o.CrewLightMod = v;
            Sync();
        }

        public static void Clear()
        {
            _value = null;
            var o = Options();
            if (o != null && _saved.HasValue)
            {
                o.CrewLightMod = _saved.Value;
                Sync();
            }
            _saved = null;
        }

        // Make sure the override wins over whatever the slider says right now.
        public static void Reassert()
        {
            if (_value == null || _applying) return;
            var o = Options();
            if (o == null) return;
            if (Math.Abs(o.CrewLightMod - _value.Value) > 0.0001f) o.CrewLightMod = _value.Value;
        }

        public static void ReassertAndSync()
        {
            if (_value == null) return;
            Reassert();
            Sync();
        }

        // Leaving the lobby: put the host's own value back so it isn't left changed.
        public static void ResetAtMenu()
        {
            try
            {
                var o = Options();
                if (o != null && _value != null && _saved.HasValue) o.CrewLightMod = _saved.Value;
            }
            catch (Exception) { }
            _value = null;
            _saved = null;
        }

        private static void Sync()
        {
            _applying = true;
            try
            {
                var gm = GameManager.Instance;
                if (gm == null) return;
                var logic = gm.LogicOptions;
                if (logic == null) return;
                logic.SyncOptions();
            }
            catch (Exception e)
            {
                Plugin.L.LogWarning("[vision] sync failed: " + e.Message);
            }
            finally
            {
                _applying = false;
            }
        }
    }
}
