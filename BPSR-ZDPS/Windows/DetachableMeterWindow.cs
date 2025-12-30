using BPSR_ZDPS.Meters;
using Hexa.NET.ImGui;
using System.Numerics;

namespace BPSR_ZDPS.Windows
{
    public static class DetachableMeterWindow
    {
        public const string LAYER = "DetachableMeterWindowLayer";
        public const string TITLE_ID = "###DetachableMeterWindow";
        public static bool IsOpened = false;
        public static bool IsTopMost = true; // Always on top

        private static MeterBase _detachedMeter = null;
        private static int RunOnceDelayed = 0;
        private static Vector2 DefaultWindowSize = new Vector2(400, 500);

        // Per-meter position/size storage (keyed by meter name)
        private static Dictionary<string, (Vector2 pos, Vector2 size)> _meterWindowSettings = new();

        public static void ToggleDetachMeter(MeterBase meter, MainWindow mainWindow)
        {
            if (_detachedMeter == meter && IsOpened)
            {
                // Save current position/size before closing
                SaveCurrentWindowSettings();
                IsOpened = false;
                _detachedMeter = null;
            }
            else
            {
                // Save previous meter's settings if switching
                if (_detachedMeter != null && IsOpened)
                {
                    SaveCurrentWindowSettings();
                }
                // Detach new meter
                _detachedMeter = meter;
                IsOpened = true;
                RunOnceDelayed = 0;
            }
        }

        private static void SaveCurrentWindowSettings()
        {
            if (_detachedMeter == null) return;
            var pos = ImGui.GetWindowPos();
            var size = ImGui.GetWindowSize();
            _meterWindowSettings[_detachedMeter.Name] = (pos, size);
        }

        private static bool TryGetSavedSettings(string meterName, out Vector2 pos, out Vector2 size)
        {
            if (_meterWindowSettings.TryGetValue(meterName, out var settings))
            {
                pos = settings.pos;
                size = settings.size;
                return true;
            }
            pos = Vector2.Zero;
            size = Vector2.Zero;
            return false;
        }

        public static void Draw(MainWindow mainWindow)
        {
            if (!IsOpened || _detachedMeter == null) return;

            // Restore saved position/size for this meter
            if (TryGetSavedSettings(_detachedMeter.Name, out var savedPos, out var savedSize))
            {
                if (savedPos != Vector2.Zero)
                    ImGui.SetNextWindowPos(savedPos, ImGuiCond.Always);
                if (savedSize != Vector2.Zero)
                    ImGui.SetNextWindowSize(savedSize, ImGuiCond.Always);
            }
            else
            {
                ImGui.SetNextWindowSize(DefaultWindowSize, ImGuiCond.FirstUseEver);
            }

            // Window flags - no title bar, no scrollbar, always on top
            ImGuiWindowFlags exWindowFlags = ImGuiWindowFlags.None;
            if (AppState.MousePassthrough)
            {
                exWindowFlags |= ImGuiWindowFlags.NoInputs;
            }

            ImGuiWindowFlags windowFlags =
                ImGuiWindowFlags.NoTitleBar |
                ImGuiWindowFlags.NoCollapse |
                ImGuiWindowFlags.NoDocking |
                ImGuiWindowFlags.NoScrollbar |
                exWindowFlags;

            // Hide scrollbar with style var
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0, 0));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);

            if (ImGui.Begin($"Detached Meter{TITLE_ID}", ref IsOpened, windowFlags))
            {
                // Run-once initialization for topmost
                if (RunOnceDelayed == 0)
                {
                    RunOnceDelayed++;
                }
                else if (RunOnceDelayed == 1)
                {
                    RunOnceDelayed++;
                    Utils.SetWindowTopmost();
                }

                // Also hide child window scrollbars
                ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarSize, 0f);

                // Draw the meter content directly - no menu bar
                _detachedMeter.Draw(mainWindow);

                ImGui.PopStyleVar(); // ScrollbarSize
            }

            ImGui.PopStyleVar(2); // WindowPadding, WindowBorderSize
            ImGui.End();

            if (!IsOpened)
            {
                _detachedMeter = null;
            }
        }
    }
}
