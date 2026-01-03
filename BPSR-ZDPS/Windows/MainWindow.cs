using BPSR_DeepsLib;
using BPSR_ZDPS.Meters;
using Hexa.NET.ImGui;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using static Zproto.WorldNtfCsharp.Types;
using Zproto;
using Newtonsoft.Json;
using BPSR_ZDPS.DataTypes;
using Hexa.NET.GLFW;

namespace BPSR_ZDPS.Windows
{
    public class MainWindow
    {
        Vector2 MainMenuBarSize;

        static bool RunOnce = true;
        static int RunOnceDelayed = 0;
        static int SettingsRunOnceDelayedPerOpen = 0;

        static int SelectedTabIndex = 0;

        static bool HasPromptedUpdateWindow = false;
        static bool HasPromptedOneTimeEnableUpdateChecks = false;

        static bool ProcessingDbWork = false;
        static bool ResumeFromDbWork = false;

        List<MeterBase> Meters = new();
        public EntityInspector entityInspector = new();
        public bool IsTopMost = false;
        static int LastPinnedOpacity = 100;
        public Vector2 WindowPosition;
        public Vector2 NextWindowPosition = new();
        public Vector2 DefaultWindowSize = new Vector2(550, 600);
        public Vector2 WindowSize;
        public Vector2 NextWindowSize = new();

        private bool _isSingleMeterMode = false;
        private MeterBase _singleMeterModeMeter = null;
        private bool _singleMeterModeWasTopMost = false;
        private bool _singleMeterModeAutoReentryEnabled = false;
        private bool _isInteractingWithWindow = false;
        private bool _justSwitchedMode = false;
        private bool _singleMeterModeEnabled = false;

        public void Draw()
        {
            DrawContent();

            UpdateCheckPromptWindow.Draw(this);
            UpdateAvailableWindow.Draw(this);
            DatabaseMigrationWindow.Draw(this);
            SettingsWindow.Draw(this);
            EncounterHistoryWindow.Draw(this);
            entityInspector.Draw(this);
            NetDebug.Draw();
            DebugDungeonTracker.Draw(this);
            RaidManagerCooldownsWindow.Draw(this);
            DatabaseManagerWindow.Draw(this);
            SpawnTrackerWindow.Draw(this);
            ModuleSolver.Draw();
            EntityCacheViewerWindow.Draw(this);
        }

        static bool p_open = true;
        public void DrawContent()
        {
            var io = ImGui.GetIO();
            var main_viewport = ImGui.GetMainViewport();

            bool hasActiveEncounter = EncounterManager.Current != null && EncounterManager.Current.HasStatsBeenRecorded();

            // Auto-reentry: If mode is enabled and encounter starts, switch to single meter view
            // Don't call EnterSingleMeterMode here because we're before ImGui.Begin() (viewport invalid)
            if (_singleMeterModeEnabled && _singleMeterModeAutoReentryEnabled && hasActiveEncounter && !_isSingleMeterMode)
            {
                _isSingleMeterMode = true; // Switch view (must happen before Begin)
                // Window state will be applied after Begin at lines 167-185 based on current state
            }

            ImGui.SetNextWindowSizeConstraints(new Vector2(375, 150), new Vector2(ImGui.GETFLTMAX()));

            if (_isSingleMeterMode)
            {
                if (Settings.Instance.WindowSettings.MainWindow.SingleMeterModeWindowPosition != new Vector2())
                {
                    ImGui.SetNextWindowPos(Settings.Instance.WindowSettings.MainWindow.SingleMeterModeWindowPosition, _justSwitchedMode ? ImGuiCond.Always : ImGuiCond.FirstUseEver);
                }
                if (Settings.Instance.WindowSettings.MainWindow.SingleMeterModeWindowSize != new Vector2())
                {
                    ImGui.SetNextWindowSize(Settings.Instance.WindowSettings.MainWindow.SingleMeterModeWindowSize, _justSwitchedMode ? ImGuiCond.Always : ImGuiCond.FirstUseEver);
                }
            }
            else
            {
                ImGui.SetNextWindowSize(DefaultWindowSize, ImGuiCond.FirstUseEver);
                if (Settings.Instance.WindowSettings.MainWindow.WindowPosition != new Vector2())
                {
                    ImGui.SetNextWindowPos(Settings.Instance.WindowSettings.MainWindow.WindowPosition, _justSwitchedMode ? ImGuiCond.Always : ImGuiCond.FirstUseEver);
                }
                if (Settings.Instance.WindowSettings.MainWindow.WindowSize != new Vector2())
                {
                    ImGui.SetNextWindowSize(Settings.Instance.WindowSettings.MainWindow.WindowSize, _justSwitchedMode ? ImGuiCond.Always : ImGuiCond.FirstUseEver);
                }
            }

            if (NextWindowPosition != new Vector2())
            {
                ImGui.SetNextWindowPos(NextWindowPosition * new Vector2(0.5f, 0.5f), ImGuiCond.Always, new Vector2(0.5f, 0.5f));
                NextWindowPosition = new Vector2();
            }

            if (NextWindowSize != new Vector2())
            {
                ImGui.SetNextWindowSize(NextWindowSize, ImGuiCond.Always);
                NextWindowSize = new Vector2();
            }

            ImGuiWindowFlags exWindowFlags = ImGuiWindowFlags.None;
            if (AppState.MousePassthrough && IsTopMost)
            {
                exWindowFlags |= ImGuiWindowFlags.NoInputs;
            }

            ImGuiWindowFlags window_flags;
            if (_isSingleMeterMode)
            {
                window_flags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.MenuBar | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoScrollbar | exWindowFlags;
            }
            else
            {
                window_flags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.MenuBar | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoDocking | exWindowFlags;
            }
            
            if (!p_open)
            {
                Hexa.NET.GLFW.GLFW.SetWindowShouldClose(HelperMethods.GLFWwindow, 1);
                return;
            }

            if (!ImGui.Begin("ZDPS", ref p_open, window_flags))
            {
                ImGui.End();
                return;
            }

            WindowPosition = ImGui.GetWindowPos();
            WindowSize = ImGui.GetWindowSize();

            // Apply window state based on current conditions (always, not just on transitions)
            // This ensures window state is consistent regardless of how we got here

            bool shouldShowSingleMeter = _isSingleMeterMode && hasActiveEncounter;
            bool shouldSendToBack = _isSingleMeterMode && !hasActiveEncounter;

            if (shouldShowSingleMeter && !IsTopMost)
            {
                // Single meter mode with active encounter: set topmost + opacity
                Utils.SetWindowTopmost();
                Utils.SetWindowOpacity(Settings.Instance.WindowSettings.MainWindow.Opacity * 0.01f);
                IsTopMost = true;
            }
            else if (!shouldShowSingleMeter && IsTopMost)
            {
                // Not showing single meter with encounter: unset topmost, reset opacity
                Utils.UnsetWindowTopmost();
                Utils.SetWindowOpacity(1.0f);
                IsTopMost = false;

                if (shouldSendToBack)
                {
                    // Single meter mode but no encounter: send to back
                    Utils.SendWindowToBack();
                }
            }

            if (_justSwitchedMode)
                _justSwitchedMode = false;

            if (ImGui.IsWindowHovered() && ImGui.IsMouseDragging(ImGuiMouseButton.Left, 0))
            {
                _isInteractingWithWindow = true;
                if (_isSingleMeterMode)
                {
                    Settings.Instance.WindowSettings.MainWindow.SingleMeterModeWindowPosition = WindowPosition;
                    Settings.Instance.WindowSettings.MainWindow.SingleMeterModeWindowSize = WindowSize;
                }
                else
                {
                    Settings.Instance.WindowSettings.MainWindow.WindowPosition = WindowPosition;
                    Settings.Instance.WindowSettings.MainWindow.WindowSize = WindowSize;
                }
                Settings.Save();
            }
            else if (!ImGui.IsMouseDragging(ImGuiMouseButton.Left, 0))
            {
                _isInteractingWithWindow = false;
            }

            DrawMenuBar();

            if (RunOnce)
            {
                RunOnce = false;

                AppState.LoadDataTables();

                Settings.Instance.Apply();

                ModuleSolver.Init();

                if (string.IsNullOrEmpty(MessageManager.NetCaptureDeviceName))
                {
                    var bestDefaultDevice = MessageManager.TryFindBestNetworkDevice();
                    if (bestDefaultDevice != null)
                    {
                        MessageManager.NetCaptureDeviceName = bestDefaultDevice.Name;
                        Settings.Instance.NetCaptureDeviceName = bestDefaultDevice.Name;
                    }
                }

                if (DB.CheckIfMigrationsNeeded())
                {
                    ProcessingDbWork = true;
                }
                else
                {
                    if (MessageManager.NetCaptureDeviceName != "")
                    {
                        MessageManager.InitializeCapturing();
                    }
                }

                Meters.Add(new DpsMeter());
                Meters.Add(new HealingMeter());
                Meters.Add(new TankingMeter());
                Meters.Add(new TakenMeter());
            }
            if (RunOnceDelayed == 0)
            {
                RunOnceDelayed++;
            }
            else if (RunOnceDelayed == 1)
            {
                RunOnceDelayed++;
                unsafe
                {
                    HelperMethods.MainWindowPlatformHandleRaw = (IntPtr)ImGui.GetWindowViewport().PlatformHandleRaw;
                }

                HotKeyManager.SetWndProc();
                //HotKeyManager.SetHookProc();

                Settings.Instance.ApplyHotKeys(this);

                Utils.SetCurrentWindowIcon();
                Utils.BringWindowToFront();

                if (Settings.Instance.External.BPTimerSettings.ExternalBPTimerEnabled)
                {
                    if (Settings.Instance.External.BPTimerSettings.ExternalBPTimerFieldBossHpReportsEnabled)
                    {
                        Managers.External.BPTimerManager.FetchSupportedMobList();
                    }
                }

                if (ProcessingDbWork)
                {
                    DatabaseMigrationWindow.Open();
                    Task.Run(() =>
                    {
                        DB.CheckAndRunMigrations();
                    });
                }
                else
                {
                    if (Settings.Instance.CheckForZDPSUpdatesOnStartup)
                    {
                        Web.WebManager.CheckForZDPSUpdates();
                    }

                    if (!Settings.Instance.HasPromptedEnableUpdateChecks && !HasPromptedOneTimeEnableUpdateChecks && !Settings.Instance.CheckForZDPSUpdatesOnStartup)
                    {
                        HasPromptedOneTimeEnableUpdateChecks = true;
                        UpdateCheckPromptWindow.Open();
                    }
                }
            }

            if (ResumeFromDbWork)
            {
                ProcessingDbWork = false;
                ResumeFromDbWork = false;
                Utils.BringWindowToFront();
                if (MessageManager.NetCaptureDeviceName != "")
                {
                    MessageManager.InitializeCapturing();
                }

                if (Settings.Instance.CheckForZDPSUpdatesOnStartup)
                {
                    Web.WebManager.CheckForZDPSUpdates();
                }
            }

            if (AppState.IsUpdateAvailable && !HasPromptedUpdateWindow)
            {
                HasPromptedUpdateWindow = true;
                UpdateAvailableWindow.Open();
            }

            if (_isSingleMeterMode)
            {
                if (_singleMeterModeMeter != null)
                {
                    ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarSize, 0f);
                    _singleMeterModeMeter.Draw(this);
                    ImGui.PopStyleVar();
                }
            }
            else
            {
                ImGuiTableFlags table_flags = ImGuiTableFlags.SizingStretchSame;
                if (ImGui.BeginTable("##MetersTable", Meters.Count, table_flags))
                {
                    ImGui.TableSetupColumn("##TabBtn", ImGuiTableColumnFlags.WidthStretch | ImGuiTableColumnFlags.DefaultHide | ImGuiTableColumnFlags.NoResize, 1f, 0);

                    for (int i = 0; i < Meters.Count; i++)
                    {
                        ImGui.TableNextColumn();

                        bool isSelected = (SelectedTabIndex == i);

                        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 0);

                        if (isSelected)
                        {
                            ImGui.PushStyleColor(ImGuiCol.Button, Colors.DimGray);
                        }

                        if (ImGui.Button($"{Meters[i].Name}##TabBtn_{i}", new Vector2(-1, 0)))
                        {
                            SelectedTabIndex = i;
                        }

                        if (ImGui.IsItemHovered() && ImGui.IsMouseReleased(ImGuiMouseButton.Right))
                        {
                            EnterSingleMeterMode(Meters[i]);
                        }

                        if (isSelected)
                        {
                            ImGui.PopStyleColor();
                        }

                        ImGui.PopStyleVar();
                    }

                    ImGui.EndTable();
                }

                ImGui.BeginChild("MeterChild", new Vector2(0, - ImGui.GetFrameHeightWithSpacing()));

                if (SelectedTabIndex > -1)
                {
                    Meters[SelectedTabIndex].Draw(this);
                }

                ImGui.EndChild();

                DrawStatusBar();
            }

            ImGui.End();
        }

        static float settingsWidth = 0.0f;
        void DrawMenuBar()
        {
            if (ImGui.BeginMenuBar())
            {
                MainMenuBarSize = ImGui.GetWindowSize();

                if (_isSingleMeterMode)
                {
                    DrawSingleMeterModeMenuBar();
                }
                else
                {
                    DrawFullModeMenuBar_Part1();
                }

                ImGui.EndMenuBar();
            }
        }

        void DrawSingleMeterModeMenuBar()
        {
            var current = EncounterManager.Current;
            if (current != null)
            {
                ImGui.Text("Status:");
                ImGui.SameLine();
                ImGui.Text($"[{AppState.PlayerMeterPlacement}]");
                ImGui.SameLine();

                string duration = "00:00:00";
                if (current.GetDuration().TotalSeconds > 0)
                    duration = current.GetDuration().ToString("hh\\:mm\\:ss");
                ImGui.Text(duration);

                if (!string.IsNullOrEmpty(current.SceneName))
                {
                    ImGui.SameLine();
                    ImGui.TextUnformatted($"- {current.SceneName}");
                }

                ImGui.SameLine();
                string currentValuePerSecond = $"{Utils.NumberToShorthand(AppState.PlayerTotalMeterValue)} ({Utils.NumberToShorthand(AppState.PlayerMeterValuePerSecond)})";
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + float.Max(0.0f, ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize(currentValuePerSecond).X));
                ImGui.Text(currentValuePerSecond);
            }
            else
            {
                ImGui.Text("Status: (No encounter)");
            }

            if (ImGui.IsWindowHovered() && ImGui.IsMouseReleased(ImGuiMouseButton.Right))
            {
                ExitSingleMeterMode(restoreTopmost: true, cancelAutoReentry: true);
            }
        }

        void DrawFullModeMenuBar_Part1()
        {
            ImGui.Text("ZDPS - BPSR Damage Meter");

            if (Utils.AppVersion != null)
            {
                ImGui.TextDisabled($"v{Utils.AppVersion}");
            }

            ImGui.SetCursorPosX(MainMenuBarSize.X - (settingsWidth * 4));
            ImGui.PushFont(HelperMethods.Fonts["FASIcons"], ImGui.GetFontSize());
            if (ImGui.MenuItem($"{FASIcons.WindowMinimize}##MinimizeBtn"))
            {
                Utils.MinimizeWindow();
            }
            ImGui.PopFont();

            ImGui.SetCursorPosX(MainMenuBarSize.X - (settingsWidth * 3));
            ImGui.PushFont(HelperMethods.Fonts["FASIcons"], ImGui.GetFontSize());
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, AppState.MousePassthrough ? 0.0f : 1.0f, AppState.MousePassthrough ? 0.0f : 1.0f, IsTopMost ? 1.0f : 0.5f));
            if (ImGui.MenuItem($"{FASIcons.Thumbtack}##TopMostBtn"))
            {
                if (!IsTopMost)
                {
                    Utils.SetWindowTopmost();
                    Utils.SetWindowOpacity(Settings.Instance.WindowSettings.MainWindow.Opacity * 0.01f);
                    LastPinnedOpacity = Settings.Instance.WindowSettings.MainWindow.Opacity;
                    IsTopMost = true;
                }
                else
                {
                    Utils.UnsetWindowTopmost();
                    Utils.SetWindowOpacity(1.0f);
                    IsTopMost = false;
                }
            }
            if (IsTopMost && LastPinnedOpacity != Settings.Instance.WindowSettings.MainWindow.Opacity)
            {
                Utils.SetWindowOpacity(Settings.Instance.WindowSettings.MainWindow.Opacity * 0.01f);
                LastPinnedOpacity = Settings.Instance.WindowSettings.MainWindow.Opacity;
            }
            ImGui.PopStyleColor();
            ImGui.PopFont();
            ImGui.SetItemTooltip("Pin Window As Top Most");

            ImGui.SetCursorPosX(MainMenuBarSize.X - (settingsWidth * 2));
            ImGui.PushFont(HelperMethods.Fonts["FASIcons"], ImGui.GetFontSize());
            if (ImGui.MenuItem($"{FASIcons.Rotate}##StartNewEncounterBtn"))
            {
                CreateNewEncounter();
            }
            ImGui.PopFont();
            ImGui.SetItemTooltip("Start New Encounter");

            ImGui.SetCursorPosX(MainMenuBarSize.X - settingsWidth);
            ImGui.PushFont(HelperMethods.Fonts["FASIcons"], ImGui.GetFontSize());
            if (ImGui.BeginMenu($"{FASIcons.Gear}##OptionsMenu"))
            {
                if (SettingsRunOnceDelayedPerOpen == 0)
                {
                    SettingsRunOnceDelayedPerOpen++;
                }
                else if (SettingsRunOnceDelayedPerOpen == 2)
                {
                    SettingsRunOnceDelayedPerOpen++;

                    if (IsTopMost)
                    {
                        Utils.SetWindowTopmost();
                    }
                    else
                    {
                        Utils.UnsetWindowTopmost();
                    }
                }
                else
                {
                    SettingsRunOnceDelayedPerOpen++;
                }

                ImGui.PopFont();

                if (ImGui.MenuItem("Encounter History"))
                {
                    EncounterHistoryWindow.Open();
                }

                if (ImGui.MenuItem("Database Manager"))
                {
                    DatabaseManagerWindow.Open();
                }
                ImGui.SetItemTooltip("Manage the ZDatabase.db contents.");

                if (ImGui.BeginMenu("Raid Manager"))
                {
                    if (ImGui.MenuItem("Cooldown Priority Tracker"))
                    {
                        RaidManagerCooldownsWindow.Open();
                    }

                    ImGui.EndMenu();
                }

                if (ImGui.BeginMenu("Benchmark"))
                {
                    ImGui.TextUnformatted("Enter how many seconds you want to run a Benchmark session for:");
                    ImGui.SetNextItemWidth(-1);
                    int benchmarkTime = AppState.BenchmarkTime;
                    ImGui.BeginDisabled(AppState.IsBenchmarkMode);
                    if (ImGui.InputInt("##BenchmarkTimeInput", ref benchmarkTime, 1, 10))
                    {
                        if (benchmarkTime < 0)
                        {
                            benchmarkTime = 0;
                        }
                        else if (benchmarkTime > 1200)
                        {
                            benchmarkTime = 1200;
                        }
                        AppState.BenchmarkTime = benchmarkTime;
                    }

                    bool benchmarkSingleTarget = AppState.BenchmarkSingleTarget;
                    ImGui.AlignTextToFramePadding();
                    ImGui.Text("Only Track First Target Hit: ");
                    ImGui.SameLine();
                    ImGui.Checkbox("##BenchmarkSingleTarget", ref benchmarkSingleTarget);
                    AppState.BenchmarkSingleTarget = benchmarkSingleTarget;

                    ImGui.EndDisabled();

                    ImGui.TextUnformatted("Note: The Benchmark time will start after the next attack.\nOnly data for your character will be processed.");
                    if (AppState.IsBenchmarkMode)
                    {
                        if (ImGui.Button("Stop Benchmark Early", new Vector2(-1, 0)))
                        {
                            AppState.HasBenchmarkBegun = false;
                            AppState.IsBenchmarkMode = false;
                            EncounterManager.StartEncounter(false, EncounterStartReason.BenchmarkEnd);
                        }
                        ImGui.SetItemTooltip("Stops the current Benchmark before the time limit is reached.");
                    }
                    else
                    {
                        if (ImGui.Button("Start Benchmark", new Vector2(-1, 0)))
                        {
                            AppState.BenchmarkSingleTargetUUID = 0;
                            AppState.IsBenchmarkMode = true;
                            CreateNewEncounter();
                        }
                    }

                    ImGui.EndMenu();
                }

                if (ImGui.BeginMenu("Integrations"))
                {
                    bool isBPTimerEnabled = Settings.Instance.External.BPTimerSettings.ExternalBPTimerEnabled;
                    if (ImGui.BeginMenu("BPTimer", isBPTimerEnabled))
                    {
                        if (ImGui.MenuItem("Spawn Tracker"))
                        {
                            SpawnTrackerWindow.Open();
                        }
                        ImGui.SetItemTooltip("View Field Boss and Magical Creature spawns.\nUses the data from BPTimer.com website.");
                        ImGui.EndMenu();
                    }
                    if (!isBPTimerEnabled)
                    {
                        ImGui.SetItemTooltip("[BPTimer] must be Enabled in the [Settings > Integrations] menu.");
                    }
                    ImGui.EndMenu();
                }

                if (ImGui.MenuItem("Module Optimizer"))
                {
                    ModuleSolver.Open();
                }
                ImGui.SetItemTooltip("Find the best module combos for your build.");

                ImGui.Separator();
                if (ImGui.MenuItem("Settings"))
                {
                    SettingsWindow.Open();
                }
                ImGui.Separator();
                if (ImGui.BeginMenu("Debug"))
                {
                    if (ImGui.MenuItem("Net Debug"))
                    {
                        NetDebug.Open();
                    }
                    if (ImGui.MenuItem("Dungeon Tracker"))
                    {
                        DebugDungeonTracker.Open();
                    }
                    if (ImGui.MenuItem("Entity Cache Viewer"))
                    {
                        EntityCacheViewerWindow.Open();
                    }
                    ImGui.EndMenu();
                }
                ImGui.Separator();
                if (ImGui.MenuItem("Exit"))
                {
                    Settings.Instance.WindowSettings.MainWindow.WindowPosition = WindowPosition;
                    Settings.Instance.WindowSettings.MainWindow.WindowSize = WindowSize;
                    p_open = false;
                }
                ImGui.EndMenu();
            }
            else
            {
                SettingsRunOnceDelayedPerOpen = 0;
                ImGui.PopFont();
            }
            settingsWidth = ImGui.GetItemRectSize().X;
        }

        void DrawStatusBar()
        {
            ImGui.BeginChild("StatusChild", new Vector2(0, -1));

            ImGui.Text("Status:");

            ImGui.SameLine();
            ImGui.Text($"[{AppState.PlayerMeterPlacement}]");

            ImGui.SameLine();
            string duration = "00:00:00";
            if (EncounterManager.Current?.GetDuration().TotalSeconds > 0)
            {
                duration = EncounterManager.Current.GetDuration().ToString("hh\\:mm\\:ss");
            }

            if (AppState.IsBenchmarkMode && !AppState.HasBenchmarkBegun)
            {
                duration = "00:00:00";
            }

            ImGui.Text(duration);

            if (!string.IsNullOrEmpty(EncounterManager.Current.SceneName))
            {
                ImGui.SameLine();
                string subName = "";
                if (!string.IsNullOrEmpty(EncounterManager.Current.SceneSubName))
                {
                    subName = $" ({EncounterManager.Current.SceneSubName})";
                }

                ImGui.TextUnformatted($"- {EncounterManager.Current.SceneName}{subName}");
            }

            if (AppState.IsBenchmarkMode)
            {
                ImGui.SameLine();
                ImGui.TextUnformatted($"[BENCHMARK ({AppState.BenchmarkTime}s)]");
            }

            ImGui.SameLine();
            string currentValuePerSecond = $"{Utils.NumberToShorthand(AppState.PlayerTotalMeterValue)} ({Utils.NumberToShorthand(AppState.PlayerMeterValuePerSecond)})";
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + float.Max(0.0f, ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize(currentValuePerSecond).X));
            ImGui.Text(currentValuePerSecond);

            ImGui.EndChild();
        }

        public void CreateNewEncounter()
        {
            EncounterManager.StopEncounter();
            Log.Information($"Starting new manual encounter at {DateTime.Now}");
            EncounterManager.StartEncounter(true);
        }

        public void EnterSingleMeterMode(MeterBase meter, bool enableAutoReentry = true)
        {
            if (meter == null) return;

            Settings.Instance.WindowSettings.MainWindow.WindowPosition = WindowPosition;
            Settings.Instance.WindowSettings.MainWindow.WindowSize = WindowSize;

            _singleMeterModeWasTopMost = IsTopMost;
            Settings.Instance.WindowSettings.MainWindow.SingleMeterModeWasTopMost = IsTopMost;

            // Enable single meter mode (user preference)
            _singleMeterModeEnabled = true;
            _singleMeterModeMeter = meter;
            _singleMeterModeAutoReentryEnabled = enableAutoReentry;
            _justSwitchedMode = true;

            Settings.Instance.WindowSettings.MainWindow.IsSingleMeterMode = true;
            Settings.Instance.WindowSettings.MainWindow.SingleMeterModeMeterName = meter.Name;
            Settings.Instance.WindowSettings.MainWindow.SingleMeterModeAutoReentryEnabled = enableAutoReentry;
            Settings.Save();

            // Determine which view to show based on whether encounter exists
            bool hasActiveEncounter = EncounterManager.Current != null && EncounterManager.Current.HasStatsBeenRecorded();
            _isSingleMeterMode = hasActiveEncounter;

            // NOTE: Window state changes (topmost, opacity, send to back) are now handled
            // by the main loop after ImGui.Begin() at lines 158-183, based on current state
        }

        public void ExitSingleMeterMode(bool restoreTopmost = false, bool cancelAutoReentry = false)
        {
            if (!_singleMeterModeEnabled) return;

            Settings.Instance.WindowSettings.MainWindow.SingleMeterModeWindowPosition = WindowPosition;
            Settings.Instance.WindowSettings.MainWindow.SingleMeterModeWindowSize = WindowSize;

            // Apply window state BEFORE switching modes (while viewport is still valid)
            if (restoreTopmost)
            {
                // Restore to the state before entering single meter mode
                if (_singleMeterModeWasTopMost)
                {
                    Utils.SetWindowTopmost();
                    Utils.SetWindowOpacity(Settings.Instance.WindowSettings.MainWindow.Opacity * 0.01f);
                    IsTopMost = true;
                }
                else
                {
                    Utils.UnsetWindowTopmost();
                    Utils.SetWindowOpacity(1.0f);
                    IsTopMost = false;
                }
            }
            else
            {
                // Always unset topmost and reset opacity when not restoring
                Utils.UnsetWindowTopmost();
                Utils.SetWindowOpacity(1.0f);
                IsTopMost = false;
                // Send window to back so it doesn't block the game
                Utils.SendWindowToBack();
            }

            // Now switch modes
            _isSingleMeterMode = false;
            _singleMeterModeEnabled = false; // Also disable the mode
            _singleMeterModeMeter = null;
            _justSwitchedMode = true;

            if (cancelAutoReentry)
            {
                _singleMeterModeAutoReentryEnabled = false;
                Settings.Instance.WindowSettings.MainWindow.SingleMeterModeAutoReentryEnabled = false;
            }

            Settings.Instance.WindowSettings.MainWindow.IsSingleMeterMode = false;
            Settings.Instance.WindowSettings.MainWindow.SingleMeterModeMeterName = "";
            Settings.Save();
        }

        public void ToggleMouseClickthrough()
        {
            AppState.MousePassthrough = !AppState.MousePassthrough;
        }

        public void SetDbWorkComplete()
        {
            ResumeFromDbWork = true;
        }
    }

    public class MainWindowWindowSettings : WindowSettingsBase
    {
        public float MeterBarScale = 1.0f;

        public bool IsSingleMeterMode = false;
        public string SingleMeterModeMeterName = "";
        public Vector2 SingleMeterModeWindowPosition = new();
        public Vector2 SingleMeterModeWindowSize = new();
        public bool SingleMeterModeWasTopMost = false;
        public bool SingleMeterModeAutoReentryEnabled = false;
    }
}
