using System;
using AmongUs.GameOptions;

namespace HostRules
{
    // Crewmate vision set by the host (lobby only):
    //   /vision X       crew vision for the whole game
    //   /lightvision X  crew vision while the lights are out (the lights sabotage), independent of /vision
    // Impostors and dead players are never affected (the game gives them their own vision).
    //
    // How it works: every player's game works out its own vision from the shared settings. CrewLightMod is just a
    // number in those settings, so the host can set values below the slider's own minimum. When the lights are out,
    // the game multiplies crew vision by MinLightRadius / MaxLightRadius of the map (about 1/5). For /lightvision the
    // host changes CrewLightMod once the lights have faded out, so that what the game shows equals the chosen value,
    // and changes it back the moment the lights are fixed: 2 settings updates per sabotage.
    //
    // 0 means pitch black. The smallest value above 0 is sent instead of 0, in case the game ever divides by it;
    // on screen it is the same.
    //
    // The override is re-applied every time settings are synced and when the game starts, so nudging a slider can't
    // silently undo it.
    internal static class VisionOverride
    {
        private const float Darkest = 0.001f;           // sent for 0
        private const float DarkLevel = 0.02f;          // lights count as "out" at or below this brightness
        private const double FadeTimeoutSeconds = 5;    // ... or this long after the sabotage started (fallback)

        private static float? _value;    // /vision (null = the host's slider wins)
        private static float? _light;    // /lightvision (null = the game's own darkening)
        private static float? _saved;    // the host's own slider value before the mod changed it
        private static bool _dark;       // lights are out and /lightvision is applied right now
        private static bool _sabotaged;
        private static DateTime _sabotageSince;
        private static bool _syncAtLobby;
        private static bool _applying;

        public static float? Value { get { return _value; } }
        public static float? LightValue { get { return _light; } }

        private static NormalGameOptionsV12 Options()
        {
            var m = GameOptionsManager.Instance;
            return m == null ? null : m.currentNormalGameOptions;
        }

        public static bool Available { get { return Options() != null; } }

        // ---------------------------------------------------------------- commands

        public static void Set(float v)
        {
            _value = v;
            Apply(true);
        }

        public static void Clear()
        {
            _value = null;
            Apply(true);
        }

        // Only used during games, so in the lobby nothing has to be sent yet.
        public static void SetLight(float v)
        {
            _light = v;
            Apply(true);
        }

        public static void ClearLight()
        {
            _light = null;
            _dark = false;
            Apply(true);
        }

        // ---------------------------------------------------------------- game flow

        public static void OnGameStart()
        {
            _dark = false;
            _sabotaged = false;
            _syncAtLobby = false;
        }

        // Round over: put the normal (lights-on) value back. It is sent when the lobby opens.
        public static void OnGameEnd()
        {
            _sabotaged = false;
            if (!_dark) return;
            _dark = false;
            if (Apply(false)) _syncAtLobby = true;
        }

        public static void OnLobbyStart()
        {
            if (!_syncAtLobby) return;
            _syncAtLobby = false;
            Sync();
        }

        // Every frame while hosting: switch to the lights-out vision once the lights have faded out, and back
        // the moment they are fixed.
        public static void TickLights()
        {
            if (!_light.HasValue)
            {
                if (_dark) SetDark(false, "/lightvision off");
                return;
            }

            var client = AmongUsClient.Instance;
            var ship = ShipStatus.Instance;
            var lights = client != null && client.IsGameStarted ? Lights(ship) : null;
            if (lights == null)
            {
                _sabotaged = false;
                if (_dark) SetDark(false, "no lights");
                return;
            }

            if (lights.ActualSwitches == lights.ExpectedSwitches)
            {
                _sabotaged = false;
                if (_dark) SetDark(false, "lights fixed");
                return;
            }

            if (!_sabotaged)
            {
                _sabotaged = true;
                _sabotageSince = DateTime.UtcNow;
            }
            if (_dark) return;

            if (lights.Level <= DarkLevel || (DateTime.UtcNow - _sabotageSince).TotalSeconds >= FadeTimeoutSeconds)
                SetDark(true, "lights out");
        }

        // ---------------------------------------------------------------- applying

        // What CrewLightMod must be right now (null = the host's own slider value).
        private static float? Target()
        {
            if (_dark && _light.HasValue) return Sendable(_light.Value * DarkRatio());
            if (_value.HasValue) return Sendable(_value.Value);
            return null;
        }

        private static float Sendable(float v)
        {
            return v < Darkest ? Darkest : v;
        }

        // How much the game shrinks crew vision when the lights are fully out (MaxLightRadius / MinLightRadius).
        private static float DarkRatio()
        {
            try
            {
                var ship = ShipStatus.Instance;
                if (ship != null && ship.MinLightRadius > 0.0001f && ship.MaxLightRadius > ship.MinLightRadius)
                    return ship.MaxLightRadius / ship.MinLightRadius;
            }
            catch (Exception)
            {
                // fall back below
            }
            return 5f;
        }

        private static SwitchSystem Lights(ShipStatus ship)
        {
            if (ship == null || ship.Systems == null || !ship.Systems.ContainsKey(SystemTypes.Electrical)) return null;
            var system = ship.Systems[SystemTypes.Electrical];
            return system == null ? null : system.TryCast<SwitchSystem>();
        }

        private static void SetDark(bool dark, string why)
        {
            _dark = dark;
            Apply(true);
            var o = Options();
            Plugin.L.LogInfo("[vision] " + why + ": crew vision setting now " + (o != null ? Rules.Num(o.CrewLightMod) : "?")
                + (dark ? " (lights-out vision " + Rules.Num(_light.GetValueOrDefault()) + ", map factor " + Rules.Num(DarkRatio()) + ")" : ""));
        }

        // Puts CrewLightMod to the value it must have right now. Returns true when it changed.
        private static bool Apply(bool sync)
        {
            var o = Options();
            if (o == null) return false;

            var target = Target();
            float want;
            if (target.HasValue)
            {
                if (!_saved.HasValue) _saved = o.CrewLightMod;
                want = target.Value;
            }
            else
            {
                if (!_saved.HasValue) return false;
                want = _saved.Value;
                _saved = null;
            }

            if (Math.Abs(o.CrewLightMod - want) <= 0.00001f) return false;
            o.CrewLightMod = want;
            if (sync) Sync();
            return true;
        }

        // Make sure the override wins over whatever the slider says right now (called before every settings sync).
        public static void Reassert()
        {
            if (_applying) return;
            var o = Options();
            if (o == null) return;
            var target = Target();
            if (!target.HasValue) return;
            if (!_saved.HasValue) _saved = o.CrewLightMod;
            if (Math.Abs(o.CrewLightMod - target.Value) > 0.00001f) o.CrewLightMod = target.Value;
        }

        public static void ReassertAndSync()
        {
            if (!Target().HasValue) return;
            Reassert();
            Sync();
        }

        // Leaving the lobby: put the host's own value back so it isn't left changed.
        public static void ResetAtMenu()
        {
            try
            {
                var o = Options();
                if (o != null && _saved.HasValue) o.CrewLightMod = _saved.Value;
            }
            catch (Exception) { }
            _value = null;
            _light = null;
            _saved = null;
            _dark = false;
            _sabotaged = false;
            _syncAtLobby = false;
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
