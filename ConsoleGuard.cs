using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using BepInEx;

namespace HostRules
{
    // Closes the BepInEx console window so nothing live (lobby code, player info, game messages) can be seen on screen or
    // on stream. Logging to BepInEx\LogOutput.log keeps working.
    //
    // It does not depend on the game being ready: a background timer starts as soon as the plugin loads and tries to close
    // the console after CloseConsoleAfterSeconds (at most 4 s), then keeps retrying until about 9 seconds have passed.
    // Backups: every frame from the game's own update, and the moment you create a lobby (CloseConsoleWhenHosting).
    // Each attempt uses BepInEx's own detach first and then plain Windows calls, so it works even if BepInEx's own state
    // is out of step with the real window.
    internal static class ConsoleGuard
    {
        [DllImport("kernel32.dll")] private static extern IntPtr GetConsoleWindow();
        [DllImport("kernel32.dll")] private static extern bool FreeConsole();
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private static readonly object Gate = new object();
        private static volatile bool _closed;
        private static volatile string _note;      // written by the timer thread, logged later from the game's thread
        private static DateTime _start = DateTime.UtcNow;
        private static DateTime _nextMainTry = DateTime.MinValue;

        public static void Start()
        {
            _start = DateTime.UtcNow;
            if (Plugin.CloseConsoleAfterSeconds.Value < 0f) return;

            var thread = new Thread(Loop) { IsBackground = true, Name = "TownOfGHost-console" };
            thread.Start();
        }

        private static void Loop()
        {
            try
            {
                double first = Math.Min(4.0, Math.Max(0.0, Plugin.CloseConsoleAfterSeconds.Value));
                var clock = Stopwatch.StartNew();
                Thread.Sleep((int)(first * 1000));
                while (!_closed && clock.Elapsed.TotalSeconds <= 9.0)
                {
                    TryClose("timer");
                    if (_closed) break;
                    Thread.Sleep(800);
                }
            }
            catch (Exception) { }
        }

        // Runs every frame (from AmongUsClient.Update), on the game's main thread.
        public static void Tick()
        {
            var note = _note;
            if (note != null)
            {
                _note = null;
                Plugin.L.LogInfo("[console] " + note);
            }

            if (_closed) return;
            var seconds = Plugin.CloseConsoleAfterSeconds.Value;
            if (seconds < 0f) return;

            var now = DateTime.UtcNow;
            if ((now - _start).TotalSeconds < seconds || now < _nextMainTry) return;
            _nextMainTry = now.AddSeconds(2);
            TryClose("game update");
        }

        public static void CloseForHosting()
        {
            if (_closed || !Plugin.CloseConsoleWhenHosting.Value) return;
            TryClose("lobby is being created");
        }

        private static void TryClose(string why)
        {
            lock (Gate)
            {
                if (_closed) return;

                // 1) BepInEx's own way
                try
                {
                    if (ConsoleManager.ConsoleActive) ConsoleManager.DetachConsole();
                }
                catch (Exception) { }

                // 2) plain Windows calls, in case a window is still attached
                try
                {
                    var window = GetConsoleWindow();
                    if (window != IntPtr.Zero)
                    {
                        ShowWindow(window, 0); // hide it right away
                        FreeConsole();
                        window = GetConsoleWindow();
                    }

                    if (window == IntPtr.Zero)
                    {
                        _closed = true;
                        _note = "console window closed (" + why + "). Logs still go to LogOutput.log";
                    }
                }
                catch (Exception e)
                {
                    _note = "could not close the console (" + why + "): " + e.Message;
                }
            }
        }
    }
}
