using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("HydroUI", "HydroRust", "2.7.0")]
    [Description("Complete UI system for HydroRust racing")]
    class HydroUI : RustPlugin
    {
        #region Fields

        [PluginReference] private Plugin HydroRust;
        [PluginReference] private Plugin ImageLibrary;

        private ConfigData configData;
        private Dictionary<ulong, PlayerUIState> playerStates = new Dictionary<ulong, PlayerUIState>();
        private Dictionary<ulong, PlayerPreferences> playerPrefs = new Dictionary<ulong, PlayerPreferences>();

        // Timers
        private Dictionary<ulong, Timer> hudTimers = new Dictionary<ulong, Timer>();
        private Dictionary<ulong, Timer> counterTimers = new Dictionary<ulong, Timer>();
        private Dictionary<ulong, Timer> editorTimers = new Dictionary<ulong, Timer>();
        private Timer globalCounterTimer;

        // UI Panel Names
        private const string UI_ROOT = "HydroUI_Root";
        private const string UI_START_MENU = "HydroUI_StartMenu";
        private const string UI_HUD = "HydroUI_HUD";
        private const string UI_PLAYER_COUNTER = "HydroUI_PlayerCounter";
        private const string UI_VOTING_MODAL = "HydroUI_VotingModal";
        private const string UI_COUNTDOWN = "HydroUI_Countdown";
        private const string UI_MENU_TOGGLE = "HydroUI_MenuToggle";
        private const string UI_TRACK_EDITOR = "HydroUI_TrackEditor";
        private const string UI_PLAYER_PANEL = "HydroUI_PlayerPanel";

        #endregion

        #region Configuration

        private class ConfigData
        {
            [JsonProperty("UI Settings")]
            public UISettings UI { get; set; } = new UISettings();

            [JsonProperty("Chat Templates")]
            public ChatTemplates Chat { get; set; } = new ChatTemplates();

            [JsonProperty("Colors")]
            public ColorScheme Colors { get; set; } = new ColorScheme();
        }

        private class UISettings
        {
            [JsonProperty("Default HUD Visible")]
            public bool DefaultHUDVisible { get; set; } = true;

            [JsonProperty("Default Scale")]
            public float DefaultScale { get; set; } = 1.0f;

            [JsonProperty("HUD Update Interval")]
            public float HUDUpdateInterval { get; set; } = 0.1f;

            [JsonProperty("Counter Update Interval")]
            public float CounterUpdateInterval { get; set; } = 1.0f;

            [JsonProperty("Editor Update Interval")]
            public float EditorUpdateInterval { get; set; } = 0.5f;

            [JsonProperty("Tracks Per Page")]
            public int TracksPerPage { get; set; } = 10;
        }

        private class ChatTemplates
        {
            [JsonProperty("Join Command")]
            public string JoinCommand { get; set; } = "/hydro join";

            [JsonProperty("Leave Command")]
            public string LeaveCommand { get; set; } = "/hydro leave";

            [JsonProperty("Stats Command")]
            public string StatsCommand { get; set; } = "/hydro stats";

            [JsonProperty("Vote Command")]
            public string VoteCommand { get; set; } = "/hydro vote";

            [JsonProperty("Vote Normal Command")]
            public string VoteNormalCommand { get; set; } = "/hydro vote normal";

            [JsonProperty("Vote Battle Command")]
            public string VoteBattleCommand { get; set; } = "/hydro vote battle";
        }

        private class ColorScheme
        {
            [JsonProperty("Primary")]
            public string Primary { get; set; } = "0.2 0.6 0.9 0.95";

            [JsonProperty("Secondary")]
            public string Secondary { get; set; } = "0.1 0.1 0.1 0.9";

            [JsonProperty("Text")]
            public string Text { get; set; } = "1 1 1 1";

            [JsonProperty("Success")]
            public string Success { get; set; } = "0.2 0.8 0.2 0.95";

            [JsonProperty("Warning")]
            public string Warning { get; set; } = "0.9 0.7 0.2 0.95";

            [JsonProperty("Danger")]
            public string Danger { get; set; } = "0.9 0.2 0.2 0.95";

            [JsonProperty("Background")]
            public string Background { get; set; } = "0.05 0.05 0.05 0.85";
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                configData = Config.ReadObject<ConfigData>();
                if (configData == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch (Exception ex)
            {
                PrintError($"Error loading config: {ex.Message}");
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            configData = new ConfigData();
            PrintWarning("Creating new config file");
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(configData);
        }

        #endregion

        #region Data Classes

        private class PlayerUIState
        {
            public bool HUDVisible { get; set; } = true;
            public bool MenuOpen { get; set; } = false;
            public bool EditorOpen { get; set; } = false;
            public bool WelcomeLocked { get; set; } = true;
            public int CurrentPage { get; set; } = 0;
            public string LastHUDData { get; set; } = "";
            public string LastCounterData { get; set; } = "";
        }

        private class PlayerPreferences
        {
            public float Scale { get; set; } = 1.0f;
            public bool CompactHUD { get; set; } = false;
            public string Theme { get; set; } = "default";
        }

        private class HUDData
        {
            public string TrackName { get; set; }
            public int Lap { get; set; }
            public int TotalLaps { get; set; }
            public int Checkpoint { get; set; }
            public int TotalCheckpoints { get; set; }
            public float Progress01 { get; set; }
            public float Speed { get; set; }
            public float Boost01 { get; set; }
            public int Position { get; set; }
            public int Racers { get; set; }
            public bool IsRace { get; set; }
            public bool Finished { get; set; }
            public float FinishTime { get; set; }
            public string RaceMode { get; set; }
            public bool Voting { get; set; }
            public int VoteSecondsRemaining { get; set; }
            public int CountdownSeconds { get; set; }
        }

        private class VotingData
        {
            public bool Voting { get; set; }
            public int SecondsRemaining { get; set; }
            public int NormalVotes { get; set; }
            public int BattleVotes { get; set; }
            public int TotalParticipants { get; set; }
            public string MyVote { get; set; }
        }

        private class CounterData
        {
            public int Online { get; set; }
            public int Lobby { get; set; }
            public int Queue { get; set; }
            public int InRace { get; set; }
        }

        #endregion

        #region Oxide Hooks

        private void Init()
        {
            LoadConfig();
            LoadPlayerPreferences();
        }

        private void OnServerInitialized()
        {
            // Start global counter timer
            globalCounterTimer = timer.Every(configData.UI.CounterUpdateInterval, () =>
            {
                foreach (var player in BasePlayer.activePlayerList)
                {
                    UpdatePlayerCounter(player);
                }
            });

            // Initialize UI for online players
            foreach (var player in BasePlayer.activePlayerList)
            {
                OnPlayerConnected(player);
            }
        }

        private void Unload()
        {
            // Clean up all timers
            foreach (var timer in hudTimers.Values)
            {
                timer?.Destroy();
            }
            foreach (var timer in counterTimers.Values)
            {
                timer?.Destroy();
            }
            foreach (var timer in editorTimers.Values)
            {
                timer?.Destroy();
            }
            globalCounterTimer?.Destroy();

            // Destroy all UI for online players
            foreach (var player in BasePlayer.activePlayerList)
            {
                DestroyAllUI(player);
            }

            SavePlayerPreferences();
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (player == null)
                return;

            // Initialize player state
            if (!playerStates.ContainsKey(player.userID))
            {
                playerStates[player.userID] = new PlayerUIState();
            }

            if (!playerPrefs.ContainsKey(player.userID))
            {
                playerPrefs[player.userID] = new PlayerPreferences
                {
                    Scale = configData.UI.DefaultScale
                };
            }

            // Show start menu
            timer.Once(1f, () =>
            {
                if (player != null && player.IsConnected)
                {
                    ShowStartMenu(player);
                    StartHUDTimer(player);
                }
            });
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            if (player == null)
                return;

            // Stop timers
            if (hudTimers.ContainsKey(player.userID))
            {
                hudTimers[player.userID]?.Destroy();
                hudTimers.Remove(player.userID);
            }
            if (counterTimers.ContainsKey(player.userID))
            {
                counterTimers[player.userID]?.Destroy();
                counterTimers.Remove(player.userID);
            }
            if (editorTimers.ContainsKey(player.userID))
            {
                editorTimers[player.userID]?.Destroy();
                editorTimers.Remove(player.userID);
            }

            DestroyAllUI(player);
        }

        #endregion

        #region Data Management

        private void LoadPlayerPreferences()
        {
            try
            {
                playerPrefs = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, PlayerPreferences>>("HydroUI_Prefs") 
                    ?? new Dictionary<ulong, PlayerPreferences>();
            }
            catch
            {
                playerPrefs = new Dictionary<ulong, PlayerPreferences>();
            }
        }

        private void SavePlayerPreferences()
        {
            Interface.Oxide.DataFileSystem.WriteObject("HydroUI_Prefs", playerPrefs);
        }

        #endregion

        #region UI Timers

        private void StartHUDTimer(BasePlayer player)
        {
            if (hudTimers.ContainsKey(player.userID))
            {
                hudTimers[player.userID]?.Destroy();
            }

            hudTimers[player.userID] = timer.Every(configData.UI.HUDUpdateInterval, () =>
            {
                if (player == null || !player.IsConnected)
                {
                    if (hudTimers.ContainsKey(player.userID))
                    {
                        hudTimers[player.userID]?.Destroy();
                        hudTimers.Remove(player.userID);
                    }
                    return;
                }

                UpdateHUD(player);
                UpdateVotingModal(player);
                UpdateCountdown(player);
            });
        }

        #endregion

        #region Start Menu

        private void ShowStartMenu(BasePlayer player)
        {
            var state = GetPlayerState(player);

            CuiHelper.DestroyUi(player, UI_START_MENU);

            var container = new CuiElementContainer();

            // Background
            container.Add(new CuiPanel
            {
                Image = { Color = configData.Colors.Background },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                CursorEnabled = true
            }, "Overlay", UI_START_MENU);

            if (state.WelcomeLocked)
            {
                // Welcome screen
                container.Add(new CuiLabel
                {
                    Text = { Text = "HYDRORUST", FontSize = 48, Align = TextAnchor.MiddleCenter, Color = configData.Colors.Text },
                    RectTransform = { AnchorMin = "0.3 0.6", AnchorMax = "0.7 0.7" }
                }, UI_START_MENU);

                container.Add(new CuiLabel
                {
                    Text = { Text = "Join the lobby to unlock racing", FontSize = 18, Align = TextAnchor.MiddleCenter, Color = configData.Colors.Text },
                    RectTransform = { AnchorMin = "0.3 0.45", AnchorMax = "0.7 0.5" }
                }, UI_START_MENU);

                // Join button
                container.Add(new CuiButton
                {
                    Button = { Command = "hydroui.join", Color = configData.Colors.Success },
                    RectTransform = { AnchorMin = "0.4 0.35", AnchorMax = "0.6 0.4" },
                    Text = { Text = "Join Lobby", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = configData.Colors.Text }
                }, UI_START_MENU);
            }
            else
            {
                // Play menu
                container.Add(new CuiLabel
                {
                    Text = { Text = "Ready to Race!", FontSize = 32, Align = TextAnchor.MiddleCenter, Color = configData.Colors.Text },
                    RectTransform = { AnchorMin = "0.3 0.6", AnchorMax = "0.7 0.65" }
                }, UI_START_MENU);

                container.Add(new CuiButton
                {
                    Button = { Command = "hydroui.play", Color = configData.Colors.Primary },
                    RectTransform = { AnchorMin = "0.4 0.4", AnchorMax = "0.6 0.45" },
                    Text = { Text = "Start Racing", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = configData.Colors.Text }
                }, UI_START_MENU);

                container.Add(new CuiButton
                {
                    Button = { Command = "hydroui.editor", Color = configData.Colors.Secondary },
                    RectTransform = { AnchorMin = "0.4 0.32", AnchorMax = "0.6 0.37" },
                    Text = { Text = "Track Editor", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = configData.Colors.Text }
                }, UI_START_MENU);
            }

            CuiHelper.AddUi(player, container);
        }

        private void HideStartMenu(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, UI_START_MENU);
        }

        #endregion

        #region HUD

        private void UpdateHUD(BasePlayer player)
        {
            var state = GetPlayerState(player);
            if (!state.HUDVisible)
                return;

            var hudData = GetHUDData(player);
            if (hudData == null)
                return;

            // Only rebuild if data changed
            string currentData = JsonConvert.SerializeObject(hudData);
            if (state.LastHUDData == currentData)
                return;

            state.LastHUDData = currentData;

            CuiHelper.DestroyUi(player, UI_HUD);

            var container = new CuiElementContainer();
            var prefs = GetPlayerPrefs(player);
            float scale = prefs.Scale;

            // HUD background panel
            container.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
            }, "Hud", UI_HUD);

            // Track name and mode indicator
            string modeIndicator = "";
            if (hudData.Voting)
            {
                modeIndicator = "[Voting]";
            }
            else if (hudData.IsRace)
            {
                // Display based on RaceMode string: "normal"→[Race], "battle"→[Battle]
                // Note: IsRace is a legacy boolean that should always be true during active races
                modeIndicator = hudData.RaceMode == "battle" ? "[Battle]" : "[Race]";
            }

            container.Add(new CuiLabel
            {
                Text = { Text = $"{hudData.TrackName} {modeIndicator}", FontSize = (int)(16 * scale), Align = TextAnchor.MiddleLeft, Color = configData.Colors.Text },
                RectTransform = { AnchorMin = "0.02 0.9", AnchorMax = "0.3 0.95" }
            }, UI_HUD);

            if (!prefs.CompactHUD)
            {
                // Progress bar
                float progressWidth = 0.2f * hudData.Progress01;
                container.Add(new CuiPanel
                {
                    Image = { Color = configData.Colors.Secondary },
                    RectTransform = { AnchorMin = "0.02 0.85", AnchorMax = "0.22 0.88" }
                }, UI_HUD, UI_HUD + "_ProgressBG");

                container.Add(new CuiPanel
                {
                    Image = { Color = configData.Colors.Primary },
                    RectTransform = { AnchorMin = "0 0", AnchorMax = $"{hudData.Progress01} 1" }
                }, UI_HUD + "_ProgressBG");

                // Boost bar
                container.Add(new CuiPanel
                {
                    Image = { Color = configData.Colors.Secondary },
                    RectTransform = { AnchorMin = "0.02 0.81", AnchorMax = "0.22 0.84" }
                }, UI_HUD, UI_HUD + "_BoostBG");

                container.Add(new CuiPanel
                {
                    Image = { Color = configData.Colors.Warning },
                    RectTransform = { AnchorMin = "0 0", AnchorMax = $"{hudData.Boost01} 1" }
                }, UI_HUD + "_BoostBG");

                // Speed
                container.Add(new CuiLabel
                {
                    Text = { Text = $"Speed: {hudData.Speed:F1}", FontSize = (int)(14 * scale), Align = TextAnchor.MiddleLeft, Color = configData.Colors.Text },
                    RectTransform = { AnchorMin = "0.02 0.77", AnchorMax = "0.15 0.80" }
                }, UI_HUD);

                // Lap info
                if (hudData.TotalLaps > 0)
                {
                    container.Add(new CuiLabel
                    {
                        Text = { Text = $"Lap: {hudData.Lap}/{hudData.TotalLaps}", FontSize = (int)(14 * scale), Align = TextAnchor.MiddleLeft, Color = configData.Colors.Text },
                        RectTransform = { AnchorMin = "0.02 0.73", AnchorMax = "0.15 0.76" }
                    }, UI_HUD);
                }

                // Checkpoint info
                if (hudData.TotalCheckpoints > 0)
                {
                    container.Add(new CuiLabel
                    {
                        Text = { Text = $"CP: {hudData.Checkpoint}/{hudData.TotalCheckpoints}", FontSize = (int)(14 * scale), Align = TextAnchor.MiddleLeft, Color = configData.Colors.Text },
                        RectTransform = { AnchorMin = "0.02 0.69", AnchorMax = "0.15 0.72" }
                    }, UI_HUD);
                }

                // Position
                if (hudData.Racers > 0)
                {
                    container.Add(new CuiLabel
                    {
                        Text = { Text = $"Position: {hudData.Position}/{hudData.Racers}", FontSize = (int)(14 * scale), Align = TextAnchor.MiddleLeft, Color = configData.Colors.Text },
                        RectTransform = { AnchorMin = "0.02 0.65", AnchorMax = "0.15 0.68" }
                    }, UI_HUD);
                }

                // Finish time
                if (hudData.Finished)
                {
                    container.Add(new CuiLabel
                    {
                        Text = { Text = $"Time: {hudData.FinishTime:F2}s", FontSize = (int)(16 * scale), Align = TextAnchor.MiddleLeft, Color = configData.Colors.Success },
                        RectTransform = { AnchorMin = "0.02 0.60", AnchorMax = "0.15 0.64" }
                    }, UI_HUD);
                }
            }

            CuiHelper.AddUi(player, container);
        }

        #endregion

        #region Player Counter

        private void UpdatePlayerCounter(BasePlayer player)
        {
            var counterData = GetCounterData();
            if (counterData == null)
                return;

            var state = GetPlayerState(player);
            string currentData = JsonConvert.SerializeObject(counterData);
            if (state.LastCounterData == currentData)
                return;

            state.LastCounterData = currentData;

            CuiHelper.DestroyUi(player, UI_PLAYER_COUNTER);

            var container = new CuiElementContainer();

            // Small panel in top-right
            container.Add(new CuiPanel
            {
                Image = { Color = configData.Colors.Background },
                RectTransform = { AnchorMin = "0.85 0.92", AnchorMax = "0.98 0.98" }
            }, "Hud", UI_PLAYER_COUNTER);

            string text = $"Online: {counterData.Online}\n" +
                         $"Lobby: {counterData.Lobby}\n" +
                         $"Queue: {counterData.Queue}\n" +
                         $"Race: {counterData.InRace}";

            container.Add(new CuiLabel
            {
                Text = { Text = text, FontSize = 10, Align = TextAnchor.MiddleCenter, Color = configData.Colors.Text },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
            }, UI_PLAYER_COUNTER);

            CuiHelper.AddUi(player, container);
        }

        #endregion

        #region Voting Modal

        private void UpdateVotingModal(BasePlayer player)
        {
            var votingData = GetVotingData(player);

            if (votingData == null || !votingData.Voting)
            {
                CuiHelper.DestroyUi(player, UI_VOTING_MODAL);
                return;
            }

            CuiHelper.DestroyUi(player, UI_VOTING_MODAL);

            var container = new CuiElementContainer();

            // Center modal
            container.Add(new CuiPanel
            {
                Image = { Color = configData.Colors.Background },
                RectTransform = { AnchorMin = "0.35 0.4", AnchorMax = "0.65 0.6" },
                CursorEnabled = true
            }, "Overlay", UI_VOTING_MODAL);

            // Title
            container.Add(new CuiLabel
            {
                Text = { Text = "Vote for Race Mode", FontSize = 20, Align = TextAnchor.MiddleCenter, Color = configData.Colors.Text },
                RectTransform = { AnchorMin = "0 0.75", AnchorMax = "1 0.95" }
            }, UI_VOTING_MODAL);

            // Time remaining
            container.Add(new CuiLabel
            {
                Text = { Text = $"Time: {votingData.SecondsRemaining}s", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = configData.Colors.Warning },
                RectTransform = { AnchorMin = "0 0.65", AnchorMax = "1 0.75" }
            }, UI_VOTING_MODAL);

            // Vote counts
            string voteText = $"Normal: {votingData.NormalVotes}  |  Battle: {votingData.BattleVotes}";
            container.Add(new CuiLabel
            {
                Text = { Text = voteText, FontSize = 14, Align = TextAnchor.MiddleCenter, Color = configData.Colors.Text },
                RectTransform = { AnchorMin = "0 0.55", AnchorMax = "1 0.65" }
            }, UI_VOTING_MODAL);

            // Your vote
            if (!string.IsNullOrEmpty(votingData.MyVote))
            {
                container.Add(new CuiLabel
                {
                    Text = { Text = $"Your vote: {votingData.MyVote}", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = configData.Colors.Success },
                    RectTransform = { AnchorMin = "0 0.45", AnchorMax = "1 0.55" }
                }, UI_VOTING_MODAL);
            }

            // Normal button
            container.Add(new CuiButton
            {
                Button = { Command = "hydroui.vote.normal", Color = configData.Colors.Primary },
                RectTransform = { AnchorMin = "0.1 0.25", AnchorMax = "0.45 0.40" },
                Text = { Text = "Normal Race", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = configData.Colors.Text }
            }, UI_VOTING_MODAL);

            // Battle button
            container.Add(new CuiButton
            {
                Button = { Command = "hydroui.vote.battle", Color = configData.Colors.Danger },
                RectTransform = { AnchorMin = "0.55 0.25", AnchorMax = "0.9 0.40" },
                Text = { Text = "Battle Mode", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = configData.Colors.Text }
            }, UI_VOTING_MODAL);

            CuiHelper.AddUi(player, container);
        }

        #endregion

        #region Countdown

        private void UpdateCountdown(BasePlayer player)
        {
            var hudData = GetHUDData(player);

            if (hudData == null || hudData.CountdownSeconds <= 0)
            {
                CuiHelper.DestroyUi(player, UI_COUNTDOWN);
                return;
            }

            CuiHelper.DestroyUi(player, UI_COUNTDOWN);

            var container = new CuiElementContainer();

            // Large center number
            string displayText = hudData.CountdownSeconds > 0 ? hudData.CountdownSeconds.ToString() : "GO!";
            string color = hudData.CountdownSeconds > 3 ? configData.Colors.Warning : configData.Colors.Success;

            container.Add(new CuiLabel
            {
                Text = { Text = displayText, FontSize = 80, Align = TextAnchor.MiddleCenter, Color = color },
                RectTransform = { AnchorMin = "0.4 0.45", AnchorMax = "0.6 0.55" }
            }, "Overlay", UI_COUNTDOWN);

            CuiHelper.AddUi(player, container);
        }

        #endregion

        #region Menu Toggle Button

        private void ShowMenuToggle(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, UI_MENU_TOGGLE);

            var container = new CuiElementContainer();

            container.Add(new CuiButton
            {
                Button = { Command = "hydroui.togglemenu", Color = configData.Colors.Primary },
                RectTransform = { AnchorMin = "0.01 0.5", AnchorMax = "0.04 0.55" },
                Text = { Text = "☰", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = configData.Colors.Text }
            }, "Hud", UI_MENU_TOGGLE);

            CuiHelper.AddUi(player, container);
        }

        #endregion

        #region Player Quick Panel

        private void ShowPlayerPanel(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, UI_PLAYER_PANEL);

            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                Image = { Color = configData.Colors.Background },
                RectTransform = { AnchorMin = "0.05 0.3", AnchorMax = "0.25 0.7" },
                CursorEnabled = true
            }, "Overlay", UI_PLAYER_PANEL);

            // Title
            container.Add(new CuiLabel
            {
                Text = { Text = "Quick Actions", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = configData.Colors.Text },
                RectTransform = { AnchorMin = "0 0.85", AnchorMax = "1 0.95" }
            }, UI_PLAYER_PANEL);

            // Join button
            container.Add(new CuiButton
            {
                Button = { Command = "hydroui.action.join", Color = configData.Colors.Success },
                RectTransform = { AnchorMin = "0.1 0.70", AnchorMax = "0.9 0.80" },
                Text = { Text = "Join Lobby", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = configData.Colors.Text }
            }, UI_PLAYER_PANEL);

            // Leave button
            container.Add(new CuiButton
            {
                Button = { Command = "hydroui.action.leave", Color = configData.Colors.Danger },
                RectTransform = { AnchorMin = "0.1 0.58", AnchorMax = "0.9 0.68" },
                Text = { Text = "Leave Lobby", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = configData.Colors.Text }
            }, UI_PLAYER_PANEL);

            // Stats button
            container.Add(new CuiButton
            {
                Button = { Command = "hydroui.action.stats", Color = configData.Colors.Secondary },
                RectTransform = { AnchorMin = "0.1 0.46", AnchorMax = "0.9 0.56" },
                Text = { Text = "View Stats", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = configData.Colors.Text }
            }, UI_PLAYER_PANEL);

            // Close button
            container.Add(new CuiButton
            {
                Button = { Command = "hydroui.closepanel", Color = configData.Colors.Secondary },
                RectTransform = { AnchorMin = "0.1 0.05", AnchorMax = "0.9 0.15" },
                Text = { Text = "Close", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = configData.Colors.Text }
            }, UI_PLAYER_PANEL);

            CuiHelper.AddUi(player, container);
        }

        #endregion

        #region Track Editor

        private void ShowTrackEditor(BasePlayer player)
        {
            var state = GetPlayerState(player);
            state.EditorOpen = true;

            CuiHelper.DestroyUi(player, UI_TRACK_EDITOR);

            var container = new CuiElementContainer();

            // Side panel
            container.Add(new CuiPanel
            {
                Image = { Color = configData.Colors.Background },
                RectTransform = { AnchorMin = "0.75 0.1", AnchorMax = "0.98 0.9" },
                CursorEnabled = true
            }, "Overlay", UI_TRACK_EDITOR);

            // Title
            container.Add(new CuiLabel
            {
                Text = { Text = "Track Editor", FontSize = 18, Align = TextAnchor.MiddleCenter, Color = configData.Colors.Text },
                RectTransform = { AnchorMin = "0 0.93", AnchorMax = "1 0.98" }
            }, UI_TRACK_EDITOR);

            // Summary
            var trackList = GetTrackList();
            container.Add(new CuiLabel
            {
                Text = { Text = $"Total Tracks: {trackList?.Count ?? 0}", FontSize = 12, Align = TextAnchor.MiddleLeft, Color = configData.Colors.Text },
                RectTransform = { AnchorMin = "0.05 0.87", AnchorMax = "0.95 0.92" }
            }, UI_TRACK_EDITOR);

            // Track list header
            container.Add(new CuiLabel
            {
                Text = { Text = "Tracks:", FontSize = 14, Align = TextAnchor.MiddleLeft, Color = configData.Colors.Text },
                RectTransform = { AnchorMin = "0.05 0.80", AnchorMax = "0.95 0.85" }
            }, UI_TRACK_EDITOR);

            // Track list with pagination
            if (trackList != null && trackList.Count > 0)
            {
                int startIdx = state.CurrentPage * configData.UI.TracksPerPage;
                int endIdx = Math.Min(startIdx + configData.UI.TracksPerPage, trackList.Count);

                float yStart = 0.75f;
                float yStep = 0.05f;

                for (int i = startIdx; i < endIdx; i++)
                {
                    float yMin = yStart - ((i - startIdx) * yStep);
                    float yMax = yMin + yStep - 0.005f;

                    container.Add(new CuiLabel
                    {
                        Text = { Text = trackList[i], FontSize = 10, Align = TextAnchor.MiddleLeft, Color = configData.Colors.Text },
                        RectTransform = { AnchorMin = $"0.05 {yMin}", AnchorMax = $"0.95 {yMax}" }
                    }, UI_TRACK_EDITOR);
                }

                // Pagination buttons
                if (state.CurrentPage > 0)
                {
                    container.Add(new CuiButton
                    {
                        Button = { Command = "hydroui.editor.prevpage", Color = configData.Colors.Secondary },
                        RectTransform = { AnchorMin = "0.05 0.15", AnchorMax = "0.25 0.20" },
                        Text = { Text = "< Prev", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = configData.Colors.Text }
                    }, UI_TRACK_EDITOR);
                }

                int totalPages = (trackList.Count + configData.UI.TracksPerPage - 1) / configData.UI.TracksPerPage;
                if (state.CurrentPage < totalPages - 1)
                {
                    container.Add(new CuiButton
                    {
                        Button = { Command = "hydroui.editor.nextpage", Color = configData.Colors.Secondary },
                        RectTransform = { AnchorMin = "0.75 0.15", AnchorMax = "0.95 0.20" },
                        Text = { Text = "Next >", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = configData.Colors.Text }
                    }, UI_TRACK_EDITOR);
                }
            }
            else
            {
                container.Add(new CuiLabel
                {
                    Text = { Text = "No tracks available", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = configData.Colors.Warning },
                    RectTransform = { AnchorMin = "0.05 0.50", AnchorMax = "0.95 0.55" }
                }, UI_TRACK_EDITOR);
            }

            // Close button
            container.Add(new CuiButton
            {
                Button = { Command = "hydroui.editor.close", Color = configData.Colors.Danger },
                RectTransform = { AnchorMin = "0.1 0.05", AnchorMax = "0.9 0.10" },
                Text = { Text = "Close Editor", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = configData.Colors.Text }
            }, UI_TRACK_EDITOR);

            CuiHelper.AddUi(player, container);
        }

        #endregion

        #region API Integration

        private HUDData GetHUDData(BasePlayer player)
        {
            if (HydroRust == null)
            {
                // Fallback data
                return new HUDData
                {
                    TrackName = "No Track",
                    Lap = 0,
                    TotalLaps = 0,
                    Checkpoint = 0,
                    TotalCheckpoints = 0,
                    Progress01 = 0f,
                    Speed = 0f,
                    Boost01 = 1f,
                    Position = 0,
                    Racers = 0,
                    IsRace = true,
                    Finished = false,
                    FinishTime = 0f,
                    RaceMode = "normal",
                    Voting = false,
                    VoteSecondsRemaining = 0,
                    CountdownSeconds = 0
                };
            }

            var result = HydroRust.Call("UI_GetHudData", player);
            if (result == null || !(result is Dictionary<string, object>))
                return null;

            var dict = result as Dictionary<string, object>;

            return new HUDData
            {
                TrackName = GetValue<string>(dict, "TrackName", "No Track"),
                Lap = GetValue<int>(dict, "Lap", 0),
                TotalLaps = GetValue<int>(dict, "TotalLaps", 0),
                Checkpoint = GetValue<int>(dict, "Checkpoint", 0),
                TotalCheckpoints = GetValue<int>(dict, "TotalCheckpoints", 0),
                Progress01 = GetValue<float>(dict, "Progress01", 0f),
                Speed = GetValue<float>(dict, "Speed", 0f),
                Boost01 = GetValue<float>(dict, "Boost01", 1f),
                Position = GetValue<int>(dict, "Position", 0),
                Racers = GetValue<int>(dict, "Racers", 0),
                IsRace = GetValue<bool>(dict, "IsRace", true),
                Finished = GetValue<bool>(dict, "Finished", false),
                FinishTime = GetValue<float>(dict, "FinishTime", 0f),
                RaceMode = GetValue<string>(dict, "RaceMode", "normal"),
                Voting = GetValue<bool>(dict, "Voting", false),
                VoteSecondsRemaining = GetValue<int>(dict, "VoteSecondsRemaining", 0),
                CountdownSeconds = GetValue<int>(dict, "CountdownSeconds", 0)
            };
        }

        private VotingData GetVotingData(BasePlayer player)
        {
            if (HydroRust == null)
                return null;

            var result = HydroRust.Call("UI_GetVotingState", player);
            if (result == null || !(result is Dictionary<string, object>))
                return null;

            var dict = result as Dictionary<string, object>;

            return new VotingData
            {
                Voting = GetValue<bool>(dict, "Voting", false),
                SecondsRemaining = GetValue<int>(dict, "SecondsRemaining", 0),
                NormalVotes = GetValue<int>(dict, "NormalVotes", 0),
                BattleVotes = GetValue<int>(dict, "BattleVotes", 0),
                TotalParticipants = GetValue<int>(dict, "TotalParticipants", 0),
                MyVote = GetValue<string>(dict, "MyVote", "")
            };
        }

        private CounterData GetCounterData()
        {
            if (HydroRust == null)
            {
                return new CounterData
                {
                    Online = BasePlayer.activePlayerList.Count,
                    Lobby = 0,
                    Queue = 0,
                    InRace = 0
                };
            }

            var result = HydroRust.Call("UI_GetCounts");
            if (result == null || !(result is Dictionary<string, object>))
                return null;

            var dict = result as Dictionary<string, object>;

            return new CounterData
            {
                Online = GetValue<int>(dict, "Online", 0),
                Lobby = GetValue<int>(dict, "Lobby", 0),
                Queue = GetValue<int>(dict, "Queue", 0),
                InRace = GetValue<int>(dict, "InRace", 0)
            };
        }

        private List<string> GetTrackList()
        {
            if (HydroRust == null)
                return new List<string>();

            var result = HydroRust.Call("UI_ListTrackNames");
            if (result == null || !(result is List<string>))
                return new List<string>();

            return result as List<string>;
        }

        private T GetValue<T>(Dictionary<string, object> dict, string key, T defaultValue)
        {
            if (dict.ContainsKey(key))
            {
                try
                {
                    return (T)Convert.ChangeType(dict[key], typeof(T));
                }
                catch
                {
                    return defaultValue;
                }
            }
            return defaultValue;
        }

        #endregion

        #region Console Commands

        [ConsoleCommand("hydroui.join")]
        private void ConsoleJoin(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null)
                return;

            var state = GetPlayerState(player);
            state.WelcomeLocked = false;

            HideStartMenu(player);
            ShowMenuToggle(player);

            RunChat(player, configData.Chat.JoinCommand);
        }

        [ConsoleCommand("hydroui.play")]
        private void ConsolePlay(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null)
                return;

            HideStartMenu(player);
            ShowMenuToggle(player);
        }

        [ConsoleCommand("hydroui.editor")]
        private void ConsoleEditor(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null)
                return;

            HideStartMenu(player);
            ShowTrackEditor(player);
        }

        [ConsoleCommand("hydroui.togglemenu")]
        private void ConsoleToggleMenu(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null)
                return;

            var state = GetPlayerState(player);
            state.MenuOpen = !state.MenuOpen;

            if (state.MenuOpen)
            {
                ShowPlayerPanel(player);
            }
            else
            {
                CuiHelper.DestroyUi(player, UI_PLAYER_PANEL);
            }
        }

        [ConsoleCommand("hydroui.closepanel")]
        private void ConsoleClosePanel(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null)
                return;

            CuiHelper.DestroyUi(player, UI_PLAYER_PANEL);
        }

        [ConsoleCommand("hydroui.vote.normal")]
        private void ConsoleVoteNormal(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null)
                return;

            RunChat(player, configData.Chat.VoteNormalCommand);
        }

        [ConsoleCommand("hydroui.vote.battle")]
        private void ConsoleVoteBattle(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null)
                return;

            RunChat(player, configData.Chat.VoteBattleCommand);
        }

        [ConsoleCommand("hydroui.action.join")]
        private void ConsoleActionJoin(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null)
                return;

            RunChat(player, configData.Chat.JoinCommand);
        }

        [ConsoleCommand("hydroui.action.leave")]
        private void ConsoleActionLeave(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null)
                return;

            RunChat(player, configData.Chat.LeaveCommand);
        }

        [ConsoleCommand("hydroui.action.stats")]
        private void ConsoleActionStats(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null)
                return;

            RunChat(player, configData.Chat.StatsCommand);
        }

        [ConsoleCommand("hydroui.editor.close")]
        private void ConsoleEditorClose(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null)
                return;

            var state = GetPlayerState(player);
            state.EditorOpen = false;

            CuiHelper.DestroyUi(player, UI_TRACK_EDITOR);
        }

        [ConsoleCommand("hydroui.editor.nextpage")]
        private void ConsoleEditorNextPage(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null)
                return;

            var state = GetPlayerState(player);
            state.CurrentPage++;

            ShowTrackEditor(player);
        }

        [ConsoleCommand("hydroui.editor.prevpage")]
        private void ConsoleEditorPrevPage(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null)
                return;

            var state = GetPlayerState(player);
            state.CurrentPage = Math.Max(0, state.CurrentPage - 1);

            ShowTrackEditor(player);
        }

        #endregion

        #region Helper Methods

        private PlayerUIState GetPlayerState(BasePlayer player)
        {
            if (!playerStates.ContainsKey(player.userID))
            {
                playerStates[player.userID] = new PlayerUIState
                {
                    HUDVisible = configData.UI.DefaultHUDVisible
                };
            }
            return playerStates[player.userID];
        }

        private PlayerPreferences GetPlayerPrefs(BasePlayer player)
        {
            if (!playerPrefs.ContainsKey(player.userID))
            {
                playerPrefs[player.userID] = new PlayerPreferences
                {
                    Scale = configData.UI.DefaultScale
                };
            }
            return playerPrefs[player.userID];
        }

        private void RunChat(BasePlayer player, string command)
        {
            if (player == null || string.IsNullOrEmpty(command))
                return;

            player.SendConsoleCommand("chat.say", command);
        }

        private void DestroyAllUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, UI_START_MENU);
            CuiHelper.DestroyUi(player, UI_HUD);
            CuiHelper.DestroyUi(player, UI_PLAYER_COUNTER);
            CuiHelper.DestroyUi(player, UI_VOTING_MODAL);
            CuiHelper.DestroyUi(player, UI_COUNTDOWN);
            CuiHelper.DestroyUi(player, UI_MENU_TOGGLE);
            CuiHelper.DestroyUi(player, UI_TRACK_EDITOR);
            CuiHelper.DestroyUi(player, UI_PLAYER_PANEL);
        }

        #endregion
    }
}
