using BPSR_ZDPS.DataTypes;
using BPSR_ZDPS.Managers;
using Hexa.NET.ImGui;
using System.Collections.Concurrent;
using System.Numerics;
using System.Threading.Tasks;
using ZLinq;

namespace BPSR_ZDPS.Windows
{
    public static class RaidManagerRaidWarningWindow
    {
        public const string LAYER = "RaidManagerRaidWarningWindowLayer";
        public static string TITLE_ID = "###RaidManagerRaidWarningWindow";
        public static bool IsOpened = false;

        public static ConcurrentDictionary<ulong, RaidWarningMessage> RaidWarningMessages = new();
        public static ulong LastWarningId = 0;

        static int RunOnceDelayed = 0;
        static bool HasInitBindings = false;
        static ulong RenderClearTime = 0;

        static bool IsEditMode = false;
        static Vector2? NewWarningWindowLocation = null;
        static Vector2? NewWarningWindowSize = null;

        static float LineHeight = 0.0f;

        static string EntityNameFilter = "";
        static KeyValuePair<long, EntityCacheLine>[]? EntityFilterMatches;

        static ImGuiWindowClassPtr NotifyMsgClass = ImGui.ImGuiWindowClass();
        static ImGuiWindowClassPtr EditModeClass = ImGui.ImGuiWindowClass();

        public static void Open()
        {
            RunOnceDelayed = 0;
            ImGuiP.PushOverrideID(ImGuiP.ImHashStr(LAYER));
            ImGui.OpenPopup(TITLE_ID);
            IsOpened = true;
            InitializeBindings();
            ImGui.PopID();
        }

        public static void InitializeBindings()
        {
            if (HasInitBindings == false)
            {
                HasInitBindings = true;

                NotifyMsgClass.ClassId = ImGuiP.ImHashStr("RaidWarningNotificationsClass");
                NotifyMsgClass.ViewportFlagsOverrideSet = ImGuiViewportFlags.TopMost | ImGuiViewportFlags.NoTaskBarIcon | ImGuiViewportFlags.NoInputs | ImGuiViewportFlags.NoRendererClear;

                EditModeClass.ClassId = ImGuiP.ImHashStr("RaidWarningEditorClass");
                EditModeClass.ViewportFlagsOverrideSet = ImGuiViewportFlags.TopMost;

                ChatManager.OnChatMessage += RaidManager_OnChatMessage;
            }
        }

        private static void RaidManager_OnChatMessage(DataTypes.Chat.User arg1, DataTypes.Chat.ChatMessage arg2, Zproto.ChitChatNtf.Types.NotifyNewestChitChatMsgs arg3)
        {
            if (Settings.Instance.WindowSettings.RaidManagerRaidWarning.AllowRaidWarnings)
            {
                bool isAllowed = !Settings.Instance.WindowSettings.RaidManagerRaidWarning.PlayerUIDBlacklist.Contains(arg1.Info.CharId);
                if (isAllowed && arg2.Msg.MsgType == Zproto.ChitChatMsgType.ChatMsgTextMessage && arg2.Msg.MsgText.Length > 5)
                {
                    if (Settings.Instance.WindowSettings.RaidManagerRaidWarning.ChatChannels.Contains(arg2.Channel))
                    {
                        if (arg2.Msg.MsgText.StartsWith("/rw ", StringComparison.OrdinalIgnoreCase))
                        {
                            string[] parts = arg2.Msg.MsgText.Split(' ', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

                            if (parts.Length > 1)
                            {
                                if (!string.IsNullOrEmpty(parts[1]))
                                {
                                    AddRaidWarning(parts[1]);
                                }
                            }
                        }
                    }
                }
            }
        }

        static void AddRaidWarning(string text)
        {
            ulong nextWarningId = LastWarningId++;
            RaidWarningMessages.TryAdd(LastWarningId, new RaidWarningMessage()
            {
                WarningId = nextWarningId,
                MessageText = text
            });
            if (Settings.Instance.WindowSettings.RaidManagerRaidWarning.PlayAlertSoundOnWarning)
            {
                Task.Run(() =>
                {
                    try
                    {
                        using (var output = new NAudio.Wave.WaveOutEvent())
                        {
                            string filepath = Settings.Instance.WindowSettings.RaidManagerRaidWarning.WarningNotificationSoundPath;
                            if (string.IsNullOrEmpty(filepath) || !File.Exists(filepath))
                            {
                                filepath = Path.Combine(Utils.DATA_DIR_NAME, "Audio", "RaidWarning_Woosh.wav");
                            }
                            using (var player = new NAudio.Wave.AudioFileReader(filepath))
                            {
                                output.Init(player);
                                var duration = player.TotalTime;
                                output.Play();
                                Thread.Sleep(duration);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Serilog.Log.Error(ex, "Error trying to play Raid Warning sound alert.");
                    }
                });
                
            }
        }

        public static void Draw(MainWindow mainWindow)
        {
            InitializeBindings();

            if (Settings.Instance.WindowSettings.RaidManagerRaidWarning.RaidWarningMessageSize == new Vector2())
            {
                DefaultSize();
            }

            if (Settings.Instance.WindowSettings.RaidManagerRaidWarning.RaidWarningMessagePosition == new Vector2())
            {
                CenterDisplay();
            }

            RenderClearTime++;
            if (RenderClearTime > 3)
            {
                RenderClearTime = 0;
            }
            if (RaidWarningMessages.Count > 0)
            {
                ImGuiP.PushOverrideID(ImGuiP.ImHashStr(LAYER));

                ImGui.SetNextWindowClass(NotifyMsgClass);

                if (Settings.Instance.WindowSettings.RaidManagerRaidWarning.RaidWarningMessagePosition != new Vector2())
                {
                    ImGui.SetNextWindowPos(Settings.Instance.WindowSettings.RaidManagerRaidWarning.RaidWarningMessagePosition, ImGuiCond.Always);
                }

                float maxWindowWidth = 0.0f;
                if (Settings.Instance.WindowSettings.RaidManagerRaidWarning.RaidWarningMessageSize != new Vector2())
                {
                    maxWindowWidth = Settings.Instance.WindowSettings.RaidManagerRaidWarning.RaidWarningMessageSize.X;

                    ImGui.SetNextWindowSize(Settings.Instance.WindowSettings.RaidManagerRaidWarning.RaidWarningMessageSize, ImGuiCond.Appearing);
                }
                
                ImGui.SetNextWindowSizeConstraints(new Vector2(0, 20), new Vector2(maxWindowWidth, ImGui.GETFLTMAX()));

                if (RenderClearTime % 2 == 0)
                {
                    ImGui.SetNextWindowSize(new Vector2(maxWindowWidth, RaidWarningMessages.Count * LineHeight));
                }
                else
                {
                    ImGui.SetNextWindowSize(new Vector2(maxWindowWidth, (RaidWarningMessages.Count * LineHeight) + 1));
                }

                if (ImGui.Begin($"RaidWarningMessagesWindow", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoBackground))
                {
                    ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0, 0, 0, Settings.Instance.WindowSettings.RaidManagerRaidWarning.MessageBackgroundOpacity));
                    if (ImGui.BeginChild("##WarningsListChild", ImGuiChildFlags.AutoResizeY, ImGuiWindowFlags.NoInputs))
                    {
                        ImGui.PushFont(null, 32.0f * Settings.Instance.WindowSettings.RaidManagerRaidWarning.MessageTextScale);
                        LineHeight = ImGui.CalcTextSize("0WM0").Y + ImGui.GetStyle().ItemSpacing.Y + ImGui.GetStyle().FramePadding.Y;
                        ImGui.PushStyleColor(ImGuiCol.Text, Colors.OrangeRed);

                        float width = ImGui.GetContentRegionAvail().X;
                        List<ulong> MessagesToRemove = new();
                        foreach (var warning in RaidWarningMessages)
                        {
                            var textSize = ImGui.CalcTextSize(warning.Value.MessageText);
                            var halfLength = textSize.X * 0.5f;
                            var windowHalf = maxWindowWidth * 0.5f;
                            var newOffset = (windowHalf - halfLength);
                            if (textSize.X > width)
                            {
                                // Text is too long so we'll start at 0 and let it do an ugly wrap or get cut off depending on how we decide to render
                                newOffset = 0.0f;
                            }

                            ImGui.SetCursorPosX(newOffset);
                            ImGui.TextUnformatted(warning.Value.MessageText);

                            bool expireMessages = true;

                            if (!expireMessages)
                            {
                                continue;
                            }
                            if (warning.Value.TimeToRemove.CompareTo(DateTime.Now) <= 0)
                            {
                                MessagesToRemove.Add(warning.Key);
                            }
                        }
                        foreach (var item in MessagesToRemove)
                        {
                            RaidWarningMessages.TryRemove(item, out _);
                        }

                        ImGui.PopStyleColor();
                        ImGui.PopFont();

                        ImGui.EndChild();
                    }

                    ImGui.PopStyleColor();
                    ImGui.End();
                }

                ImGui.PopID();
            }

            if (!IsOpened)
            {
                return;
            }

            ImGui.SetNextWindowSize(new Vector2(680, 580), ImGuiCond.Appearing);
            ImGui.SetNextWindowSizeConstraints(new Vector2(680, 400), new Vector2(ImGui.GETFLTMAX()));

            ImGuiP.PushOverrideID(ImGuiP.ImHashStr(LAYER));

            if (ImGui.Begin($"Raid Manager - Raid Warnings{TITLE_ID}", ref IsOpened, ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoDocking))
            {
                if (RunOnceDelayed == 0)
                {
                    RunOnceDelayed++;
                }
                else if (RunOnceDelayed == 1)
                {
                    RunOnceDelayed++;
                    Utils.SetCurrentWindowIcon();
                    Utils.BringWindowToFront();
                }

                ImGui.TextWrapped("Raid Warnings are messages sent in-game, prefixed with '/rw', that appear in a defined section of your screen as large bold orange text.");
                ImGui.BulletText("'/rw Group 1 go left.' will display the message 'Group 1 go left.' on the screen as a Raid Warning.");

                ImGui.AlignTextToFramePadding();
                ImGui.TextUnformatted("Allow Raid Warnings: ");
                ImGui.SameLine();
                ImGui.Checkbox("##RaidWarningsEnabled", ref Settings.Instance.WindowSettings.RaidManagerRaidWarning.AllowRaidWarnings);
                ImGui.Indent();
                ImGui.BeginDisabled(true);
                ImGui.TextWrapped("When enabled, allows Raid Warnings to be processed and displayed.");
                ImGui.EndDisabled();
                ImGui.Unindent();

                ImGui.AlignTextToFramePadding();
                ImGui.TextUnformatted("Play Alert Sound On New Raid Warning: ");
                ImGui.SameLine();
                ImGui.Checkbox("##PlaySoundAlertOnNewRaidWarning", ref Settings.Instance.WindowSettings.RaidManagerRaidWarning.PlayAlertSoundOnWarning);
                ImGui.Indent();
                ImGui.BeginDisabled(true);
                ImGui.TextWrapped("When enabled, a sound alert will be played each time a new Raid Warning appears.");
                ImGui.EndDisabled();
                ImGui.Unindent();

                ImGui.AlignTextToFramePadding();
                ImGui.Text("Raid Warning Notification Sound Path: ");
                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 140 - ImGui.GetStyle().ItemSpacing.X);
                ImGui.InputText("##WarningNotificationSoundPath", ref Settings.Instance.WindowSettings.RaidManagerRaidWarning.WarningNotificationSoundPath, 1024);
                ImGui.SameLine();
                if (ImGui.Button("Browse...##WarningSoundPathBrowseBtn", new Vector2(140, 0)))
                {
                    string defaultDir = File.Exists(Settings.Instance.WindowSettings.RaidManagerRaidWarning.WarningNotificationSoundPath) ? Path.GetDirectoryName(Settings.Instance.WindowSettings.RaidManagerRaidWarning.WarningNotificationSoundPath) : "";

                    ImFileBrowser.OpenFile((selectedFilePath) =>
                    {
                        System.Diagnostics.Debug.WriteLine($"WarningNotificationSoundPath = {selectedFilePath}");
                        Settings.Instance.WindowSettings.RaidManagerRaidWarning.WarningNotificationSoundPath = selectedFilePath;
                    },
                    "Select a sound file...", defaultDir, "MP3 (*.mp3)|*.mp3|WAV (*.wav)|*.wav|All Files (*.*)|*.*", 0);
                }
                ImGui.Indent();
                ImGui.BeginDisabled(true);
                ImGui.TextWrapped("File path to a custom sound file to play when the Raid Warning message occurs.\nA default sound will be used if none is set or the file is invalid.\nNote: Only MP3 and WAV are supported formats.");
                ImGui.EndDisabled();
                ImGui.Unindent();

                ImGui.AlignTextToFramePadding();
                ImGui.TextUnformatted("Select Chat Channels To Use: ");
                ChatChannelToggle("Local", Zproto.ChitChatChannelType.ChannelScene);
                ImGui.SameLine();
                ChatChannelToggle("Team", Zproto.ChitChatChannelType.ChannelTeam);
                ImGui.SameLine();
                ChatChannelToggle("Guild", Zproto.ChitChatChannelType.ChannelUnion);
                ImGui.SameLine();
                ChatChannelToggle("Group", Zproto.ChitChatChannelType.ChannelGroup);

                ImGui.AlignTextToFramePadding();
                ImGui.Text("Message Text Scale: ");
                ImGui.SetNextItemWidth(-1);
                ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, ImGui.GetColorU32(ImGuiCol.FrameBgHovered, 0.55f));
                ImGui.PushStyleColor(ImGuiCol.FrameBgActive, ImGui.GetColorU32(ImGuiCol.FrameBgActive, 0.55f));
                if (ImGui.SliderFloat("##MessageTextScale", ref Settings.Instance.WindowSettings.RaidManagerRaidWarning.MessageTextScale, 0.80f, 3.0f, $"{(int)(Settings.Instance.WindowSettings.RaidManagerRaidWarning.MessageTextScale * 100)}%%"))
                {
                    Settings.Instance.WindowSettings.RaidManagerRaidWarning.MessageTextScale = MathF.Round(Settings.Instance.WindowSettings.RaidManagerRaidWarning.MessageTextScale, 2);
                }
                ImGui.PopStyleColor(2);
                ImGui.Indent();
                ImGui.BeginDisabled(true);
                ImGui.TextWrapped("Scaling for how large the Raid Warning message text should be. 100%% is the default scale.");
                ImGui.EndDisabled();
                ImGui.Unindent();

                ImGui.AlignTextToFramePadding();
                ImGui.Text("Message Background Opacity: ");
                ImGui.SetNextItemWidth(-1);
                ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, ImGui.GetColorU32(ImGuiCol.FrameBgHovered, 0.55f));
                ImGui.PushStyleColor(ImGuiCol.FrameBgActive, ImGui.GetColorU32(ImGuiCol.FrameBgActive, 0.55f));
                if (ImGui.SliderFloat("##MessageBackgroundOpacity", ref Settings.Instance.WindowSettings.RaidManagerRaidWarning.MessageBackgroundOpacity, 0.0f, 1.0f, $"{(int)(Settings.Instance.WindowSettings.RaidManagerRaidWarning.MessageBackgroundOpacity * 100)}%%"))
                {
                    Settings.Instance.WindowSettings.RaidManagerRaidWarning.MessageBackgroundOpacity = MathF.Round(Settings.Instance.WindowSettings.RaidManagerRaidWarning.MessageBackgroundOpacity, 2);
                }
                ImGui.PopStyleColor(2);
                ImGui.Indent();
                ImGui.BeginDisabled(true);
                ImGui.TextWrapped("Opacity for the background of Raid Warning notification messages.");
                ImGui.EndDisabled();
                ImGui.Unindent();

                string toggleEditModeText = !IsEditMode ? "Edit Raid Warning Location" : "Stop Editing Location";
                if (ImGui.Button($"{toggleEditModeText}##ToggleEditModeBtn"))
                {
                    IsEditMode = !IsEditMode;
                }

                if (ImGui.Button("Set To Default Location"))
                {
                    CenterDisplay();
                }

                if (ImGui.Button("Set To Default Size"))
                {
                    DefaultSize();
                }

                if (ImGui.Button("Display Raid Warning Test Message"))
                {
                    AddRaidWarning($"This is a test Raid Warning message - {LastWarningId}.");
                }

                if (ImGui.CollapsingHeader("Player Blacklist##PlayerBlacklistSection", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    ImGui.TextUnformatted("Select Players from the list below to add them to a Blacklist that ignores Raid Warnings from them.");

                    ImGui.AlignTextToFramePadding();
                    ImGui.Text("Entity Filter: ");
                    ImGui.SameLine();
                    ImGui.PushItemWidth(ImGui.GetContentRegionAvail().X);
                    if (ImGui.InputText("##EntityFilterText", ref EntityNameFilter, 64))
                    {
                        if (EntityNameFilter.Length > 0)
                        {
                            bool isNum = Char.IsNumber(EntityNameFilter[0]);
                            EntityFilterMatches = EntityCache.Instance.Cache.Lines.AsValueEnumerable().Where(x => isNum ? x.Value.UID.ToString().Contains(EntityNameFilter) : x.Value.Name != null && x.Value.Name.Contains(EntityNameFilter, StringComparison.OrdinalIgnoreCase)).ToArray();
                        }
                        else
                        {
                            EntityFilterMatches = null;
                        }
                    }

                    if (ImGui.BeginListBox("##PlayerListBox", new Vector2(ImGui.GetContentRegionAvail().X, 120)))
                    {
                        if (EntityFilterMatches != null && (EntityFilterMatches.Length < 100 || EntityNameFilter.Length > 2))
                        {
                            if (EntityFilterMatches.Any())
                            {
                                long matchIdx = 0;
                                foreach (var match in EntityFilterMatches)
                                {
                                    bool isSelected = Settings.Instance.WindowSettings.RaidManagerRaidWarning.PlayerUIDBlacklist.Contains(match.Value.UID);

                                    if (isSelected)
                                    {
                                        ImGui.PushStyleColor(ImGuiCol.Text, Colors.Red_Transparent);
                                        ImGui.PushFont(HelperMethods.Fonts["FASIcons"], ImGui.GetFontSize());
                                        if (ImGui.Button($"{FASIcons.Minus}##RemoveBtn_{matchIdx}", new Vector2(30, 30)))
                                        {
                                            Settings.Instance.WindowSettings.RaidManagerRaidWarning.PlayerUIDBlacklist.Remove(match.Value.UID);
                                        }
                                        ImGui.PopFont();
                                        ImGui.PopStyleColor();
                                    }
                                    else
                                    {
                                        ImGui.PushStyleColor(ImGuiCol.Text, Colors.Green_Transparent);
                                        ImGui.PushFont(HelperMethods.Fonts["FASIcons"], ImGui.GetFontSize());
                                        if (ImGui.Button($"{FASIcons.Plus}##AddBtn_{matchIdx}", new Vector2(30, 30)))
                                        {
                                            Settings.Instance.WindowSettings.RaidManagerRaidWarning.PlayerUIDBlacklist.Add(match.Value.UID);
                                        }
                                        ImGui.PopFont();
                                        ImGui.PopStyleColor();
                                    }

                                    ImGui.SameLine();
                                    ImGui.Text($"{match.Value.Name} [U:{match.Value.UID}] {{UU:{match.Value.UUID}}}");

                                    matchIdx++;
                                }
                            }
                        }
                        ImGui.EndListBox();
                    }
                }

                ImGui.End();
            }

            if (IsEditMode)
            {
                ImGui.SetNextWindowClass(EditModeClass);

                ImGui.SetNextWindowSize(new Vector2(700, 100), ImGuiCond.Appearing);

                if (Settings.Instance.WindowSettings.RaidManagerRaidWarning.RaidWarningMessagePosition != new Vector2())
                {
                    ImGui.SetNextWindowPos(Settings.Instance.WindowSettings.RaidManagerRaidWarning.RaidWarningMessagePosition, ImGuiCond.Appearing);
                }

                if (Settings.Instance.WindowSettings.RaidManagerRaidWarning.RaidWarningMessageSize != new Vector2())
                {
                    ImGui.SetNextWindowSize(Settings.Instance.WindowSettings.RaidManagerRaidWarning.RaidWarningMessageSize, ImGuiCond.Appearing);
                }

                if (NewWarningWindowSize != null)
                {
                    ImGui.SetNextWindowSize(NewWarningWindowSize.Value, ImGuiCond.Always);
                    NewWarningWindowSize = null;
                }

                if (NewWarningWindowLocation != null)
                {
                    ImGui.SetNextWindowPos(NewWarningWindowLocation.Value, ImGuiCond.Always);
                    NewWarningWindowLocation = null;
                }

                ImGui.SetNextWindowSizeConstraints(new Vector2(500, -1), new Vector2(ImGui.GETFLTMAX(), -1));

                ImGui.GetStyle().WindowTitleAlign = new Vector2(0.5f, 0.5f);
                if (ImGui.Begin("Raid Warning - Default Message Position", ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoDocking))
                {
                    Settings.Instance.WindowSettings.RaidManagerRaidWarning.RaidWarningMessagePosition = ImGui.GetWindowPos();
                    Settings.Instance.WindowSettings.RaidManagerRaidWarning.RaidWarningMessageSize = ImGui.GetWindowSize();

                    ImGui.TextAligned(0.5f, -1, "Place this window where you want Raid Warnings to begin appearing.");
                    ImGui.TextAligned(0.5f, -1, "Messages will be as wide as this window so be sure to make it wide enough!");
                    ImGui.End();
                }
            }

            ImGui.PopID();
        }

        static void CenterDisplay()
        {
            var gameProc = BPSR_DeepsLib.Utils.GetCachedProcessEntry();
            if (gameProc != null && gameProc.ProcessId > 0 && !string.IsNullOrEmpty(gameProc.ProcessName))
            {
                try
                {
                    System.Diagnostics.Process process = System.Diagnostics.Process.GetProcessById(gameProc.ProcessId);
                    User32.RECT procRect = new();
                    User32.GetWindowRect(process.MainWindowHandle, ref procRect);

                    float centerX = (procRect.left + procRect.right) * 0.5f;
                    Vector2 centerPoint = new Vector2(MathF.Floor(centerX), MathF.Floor((procRect.bottom - procRect.top) * 0.15f));

                    var size = Settings.Instance.WindowSettings.RaidManagerRaidWarning.RaidWarningMessageSize;
                    Vector2 newPoint = centerPoint - new Vector2(size.X * 0.5f, 0);

                    Settings.Instance.WindowSettings.RaidManagerRaidWarning.RaidWarningMessagePosition = newPoint;
                    NewWarningWindowLocation = newPoint;
                }
                catch (Exception ex)
                {
                    Serilog.Log.Error(ex, "Error using game process for centering Countdown Timer.");
                }
            }
            else
            {
                // Game process was not found, use the primary monitor bounds instead
                var glfwMonitor = Hexa.NET.GLFW.GLFW.GetPrimaryMonitor();
                var glfwVidMode = Hexa.NET.GLFW.GLFW.GetVideoMode(glfwMonitor);

                Vector2 centerPoint = new Vector2(MathF.Floor(glfwVidMode.Width * 0.5f), MathF.Floor(glfwVidMode.Height * 0.15f));

                var size = Settings.Instance.WindowSettings.RaidManagerRaidWarning.RaidWarningMessageSize;
                Vector2 newPoint = centerPoint - new Vector2(MathF.Floor(size.X * 0.5f), 0);

                Settings.Instance.WindowSettings.RaidManagerRaidWarning.RaidWarningMessagePosition = newPoint;
                NewWarningWindowLocation = newPoint;
            }
        }

        static void DefaultSize()
        {
            var gameProc = BPSR_DeepsLib.Utils.GetCachedProcessEntry();
            if (gameProc != null && gameProc.ProcessId > 0 && !string.IsNullOrEmpty(gameProc.ProcessName))
            {
                try
                {
                    System.Diagnostics.Process process = System.Diagnostics.Process.GetProcessById(gameProc.ProcessId);
                    User32.RECT procRect = new();
                    User32.GetWindowRect(process.MainWindowHandle, ref procRect);

                    Vector2 newSize = new Vector2(MathF.Floor((procRect.right - procRect.left) * 0.90f), 100);

                    Settings.Instance.WindowSettings.RaidManagerRaidWarning.RaidWarningMessageSize = newSize;
                    NewWarningWindowSize = newSize;
                }
                catch (Exception ex)
                {
                    Serilog.Log.Error(ex, "Error using game process for default size of Countdown Timer.");
                }
            }
            else
            {
                // Game process was not found, use the primary monitor bounds instead
                var glfwMonitor = Hexa.NET.GLFW.GLFW.GetPrimaryMonitor();
                var glfwVidMode = Hexa.NET.GLFW.GLFW.GetVideoMode(glfwMonitor);

                Vector2 newSize = new Vector2(MathF.Floor(glfwVidMode.Width * 0.90f), 100);

                Settings.Instance.WindowSettings.RaidManagerRaidWarning.RaidWarningMessageSize = newSize;
                NewWarningWindowSize = newSize;
            }
        }

        static void ChatChannelToggle(string name, Zproto.ChitChatChannelType channel)
        {
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted($"{name}: ");
            ImGui.SameLine();
            var isEnabled = Settings.Instance.WindowSettings.RaidManagerRaidWarning.ChatChannels.Contains(channel);
            if (ImGui.Checkbox($"##Channel_{name}", ref isEnabled))
            {
                if (isEnabled)
                {
                    Settings.Instance.WindowSettings.RaidManagerRaidWarning.ChatChannels.Add(channel);
                }
                else
                {
                    Settings.Instance.WindowSettings.RaidManagerRaidWarning.ChatChannels.Remove(channel);
                }
            }
        }
    }

    public class RaidWarningMessage()
    {
        public string MessageText = "";
        public ulong WarningId = 0;
        public DateTime TimeAdded = DateTime.Now;
        public DateTime TimeToRemove = DateTime.Now.AddSeconds(10);
    }

    public class RaidManagerRaidWarningWindowSettings : WindowSettingsBase
    {
        public bool AllowRaidWarnings = false;
        public Vector2 RaidWarningMessagePosition = new();
        public Vector2 RaidWarningMessageSize = new();
        public float MessageTextScale = 1.0f;
        public float MessageBackgroundOpacity = 0.0f;
        public bool PlayAlertSoundOnWarning = true;
        public string WarningNotificationSoundPath = "";
        public List<Zproto.ChitChatChannelType> ChatChannels = new() { Zproto.ChitChatChannelType.ChannelTeam };
        public List<long> PlayerUIDBlacklist = new();
    }
}
