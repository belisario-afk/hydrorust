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
    [Info("HydroUI", "HydroTeam", "2.7.0")]
    [Description("Complete UI system for HydroRust boat racing")]
    class HydroUI : RustPlugin
    {
        #region Fields
        
        private Configuration config;
        private Dictionary<ulong, PlayerUIState> playerStates = new Dictionary<ulong, PlayerUIState>();
        private Dictionary<ulong, PlayerPrefs> playerPrefs = new Dictionary<ulong, PlayerPrefs>();
        
        [PluginReference]
        private Plugin HydroRust, ImageLibrary;
        
        // UI Panel Names
        private const string UI_MAIN = "HydroUI.Main";
        private const string UI_MENU = "HydroUI.Menu";
        private const string UI_WELCOME = "HydroUI.Welcome";
        private const string UI_HUD = "HydroUI.HUD";
        private const string UI_COUNTER = "HydroUI.Counter";
        private const string UI_VOTING = "HydroUI.Voting";
        private const string UI_COUNTDOWN = "HydroUI.Countdown";
        private const string UI_EDITOR = "HydroUI.Editor";
        private const string UI_QUICKPANEL = "HydroUI.QuickPanel";
        
        // Data file names
        private const string PREFS_FILE = "HydroUI_Prefs";
        
        // UI Constants
        private const string MENU_ICON = "☰";
        
        private Timer hudTick;
        private Timer counterTick;
        private Timer editorRefreshTick;
        
        #endregion
        
        #region Configuration
        
        private class Configuration
        {
            [JsonProperty("UI Settings")]
            public UISettings UI { get; set; } = new UISettings();
            
            [JsonProperty("Chat Commands")]
            public ChatCommandsConfig ChatCommands { get; set; } = new ChatCommandsConfig();
            
            [JsonProperty("Colors")]
            public ColorsConfig Colors { get; set; } = new ColorsConfig();
        }
        
        private class UISettings
        {
            [JsonProperty("DefaultScale")]
            public float DefaultScale { get; set; } = 1.0f;
            
            [JsonProperty("HudRefreshRate")]
            public float HudRefreshRate { get; set; } = 0.1f;
            
            [JsonProperty("CounterRefreshRate")]
            public float CounterRefreshRate { get; set; } = 1.0f;
            
            [JsonProperty("EditorRefreshRate")]
            public float EditorRefreshRate { get; set; } = 0.5f;
            
            [JsonProperty("TracksPerPage")]
            public int TracksPerPage { get; set; } = 5;
        }
        
        private class ChatCommandsConfig
        {
            [JsonProperty("JoinTemplate")]
            public string JoinTemplate { get; set; } = "/hydro join";
            
            [JsonProperty("LeaveTemplate")]
            public string LeaveTemplate { get; set; } = "/hydro leave";
            
            [JsonProperty("VoteNormalTemplate")]
            public string VoteNormalTemplate { get; set; } = "/hydro vote normal";
            
            [JsonProperty("VoteBattleTemplate")]
            public string VoteBattleTemplate { get; set; } = "/hydro vote battle";
        }
        
        private class ColorsConfig
        {
            [JsonProperty("Primary")]
            public string Primary { get; set; } = "0.2 0.4 0.6 0.95";
            
            [JsonProperty("Secondary")]
            public string Secondary { get; set; } = "0.15 0.15 0.15 0.9";
            
            [JsonProperty("Accent")]
            public string Accent { get; set; } = "0.3 0.6 0.9 1";
            
            [JsonProperty("Text")]
            public string Text { get; set; } = "1 1 1 1";
            
            [JsonProperty("Success")]
            public string Success { get; set; } = "0.2 0.8 0.2 1";
            
            [JsonProperty("Warning")]
            public string Warning { get; set; } = "0.9 0.6 0.1 1";
            
            [JsonProperty("Danger")]
            public string Danger { get; set; } = "0.8 0.2 0.2 1";
        }
        
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) throw new Exception();
            }
            catch
            {
                Config.WriteObject(config = new Configuration(), true);
            }
            SaveConfig();
        }
        
        protected override void SaveConfig() => Config.WriteObject(config, true);
        
        protected override void LoadDefaultConfig() => config = new Configuration();
        
        #endregion
        
        #region Data Classes
        
        private class PlayerUIState
        {
            public bool MenuOpen { get; set; }
            public bool WelcomeLocked { get; set; } = true;
            public bool EditorOpen { get; set; }
            public int EditorPage { get; set; }
            public string LastHudData { get; set; }
            public string LastCounterData { get; set; }
            public string LastVotingData { get; set; }
            public string LastCountdownData { get; set; }
        }
        
        private class PlayerPrefs
        {
            public float Scale { get; set; } = 1.0f;
            public bool CompactHUD { get; set; }
            public string Theme { get; set; } = "default";
        }
        
        #endregion
        
        #region Oxide Hooks
        
        private void Init()
        {
            LoadData();
        }
        
        private void OnServerInitialized()
        {
            // Start timers
            hudTick = timer.Every(config.UI.HudRefreshRate, UpdateAllHuds);
            counterTick = timer.Every(config.UI.CounterRefreshRate, UpdateAllCounters);
            editorRefreshTick = timer.Every(config.UI.EditorRefreshRate, UpdateAllEditors);
            
            // Initialize UI for all connected players
            foreach (var player in BasePlayer.activePlayerList)
            {
                OnPlayerConnected(player);
            }
        }
        
        private void Unload()
        {
            SaveData();
            
            // Destroy all UI
            foreach (var player in BasePlayer.activePlayerList)
            {
                DestroyUI(player);
            }
            
            hudTick?.Destroy();
            counterTick?.Destroy();
            editorRefreshTick?.Destroy();
        }
        
        private void OnPlayerConnected(BasePlayer player)
        {
            if (player == null) return;
            
            if (!playerStates.ContainsKey(player.userID))
            {
                playerStates[player.userID] = new PlayerUIState();
            }
            
            if (!playerPrefs.ContainsKey(player.userID))
            {
                playerPrefs[player.userID] = new PlayerPrefs();
            }
            
            // Show welcome screen
            timer.Once(1f, () =>
            {
                if (player != null && player.IsConnected)
                {
                    ShowWelcomeScreen(player);
                    ShowHUD(player);
                    ShowPlayerCounter(player);
                }
            });
        }
        
        private void OnPlayerDisconnected(BasePlayer player)
        {
            if (player == null) return;
            
            DestroyUI(player);
            SaveData();
        }
        
        #endregion
        
        #region Data Management
        
        private void LoadData()
        {
            try
            {
                playerPrefs = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, PlayerPrefs>>(PREFS_FILE) 
                    ?? new Dictionary<ulong, PlayerPrefs>();
            }
            catch
            {
                playerPrefs = new Dictionary<ulong, PlayerPrefs>();
            }
        }
        
        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject(PREFS_FILE, playerPrefs);
        }
        
        #endregion
        
        #region UI Creation - Welcome Screen
        
        private void ShowWelcomeScreen(BasePlayer player)
        {
            var state = GetPlayerState(player);
            
            var container = new CuiElementContainer();
            
            // Welcome panel
            container.Add(new CuiPanel
            {
                Image = { Color = config.Colors.Secondary },
                RectTransform = { AnchorMin = "0.3 0.3", AnchorMax = "0.7 0.7" },
                CursorEnabled = true
            }, "Overlay", UI_WELCOME);
            
            // Title
            container.Add(new CuiLabel
            {
                Text = { 
                    Text = "Welcome to HydroRust", 
                    FontSize = 24, 
                    Align = TextAnchor.MiddleCenter,
                    Color = config.Colors.Text
                },
                RectTransform = { AnchorMin = "0.1 0.7", AnchorMax = "0.9 0.9" }
            }, UI_WELCOME);
            
            // Description
            container.Add(new CuiLabel
            {
                Text = { 
                    Text = "Boat racing plugin with advanced physics\nJoin the lobby to start racing!", 
                    FontSize = 14, 
                    Align = TextAnchor.MiddleCenter,
                    Color = config.Colors.Text
                },
                RectTransform = { AnchorMin = "0.1 0.4", AnchorMax = "0.9 0.7" }
            }, UI_WELCOME);
            
            // Play button (locked until lobby join)
            string buttonColor = state.WelcomeLocked ? config.Colors.Secondary : config.Colors.Success;
            string buttonText = state.WelcomeLocked ? "Join Lobby First" : "Start Racing!";
            string command = state.WelcomeLocked ? "" : "hydroui.closewelcome";
            
            container.Add(new CuiButton
            {
                Button = { Color = buttonColor, Command = command },
                RectTransform = { AnchorMin = "0.3 0.15", AnchorMax = "0.7 0.3" },
                Text = { 
                    Text = buttonText, 
                    FontSize = 16, 
                    Align = TextAnchor.MiddleCenter,
                    Color = config.Colors.Text
                }
            }, UI_WELCOME);
            
            CuiHelper.DestroyUi(player, UI_WELCOME);
            CuiHelper.AddUi(player, container);
        }
        
        #endregion
        
        #region UI Creation - HUD
        
        private void ShowHUD(BasePlayer player)
        {
            var hudData = GetHudData(player);
            var prefs = GetPlayerPrefs(player);
            
            var container = new CuiElementContainer();
            
            // Main HUD container
            container.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
            }, "Overlay", UI_HUD);
            
            // Track name and mode indicator
            string modeTag = "";
            if (hudData.Voting)
                modeTag = "[Voting]";
            else if (hudData.IsRace)
                modeTag = hudData.RaceMode == "battle" ? "[Battle]" : "[Race]";
            
            string trackDisplay = string.IsNullOrEmpty(hudData.TrackName) ? "Free Roam" : $"{hudData.TrackName} {modeTag}";
            
            container.Add(new CuiLabel
            {
                Text = { 
                    Text = trackDisplay, 
                    FontSize = 16, 
                    Align = TextAnchor.MiddleLeft,
                    Color = config.Colors.Text
                },
                RectTransform = { AnchorMin = "0.02 0.92", AnchorMax = "0.3 0.98" }
            }, UI_HUD);
            
            // Speed indicator
            container.Add(new CuiLabel
            {
                Text = { 
                    Text = $"Speed: {hudData.Speed:F1}", 
                    FontSize = 14, 
                    Align = TextAnchor.MiddleLeft,
                    Color = config.Colors.Text
                },
                RectTransform = { AnchorMin = "0.02 0.12", AnchorMax = "0.15 0.17" }
            }, UI_HUD);
            
            // Lap/Checkpoint info
            if (hudData.IsRace)
            {
                string lapInfo = $"Lap {hudData.Lap}/{hudData.TotalLaps}  CP {hudData.Checkpoint}/{hudData.TotalCheckpoints}";
                container.Add(new CuiLabel
                {
                    Text = { 
                        Text = lapInfo, 
                        FontSize = 14, 
                        Align = TextAnchor.MiddleLeft,
                        Color = config.Colors.Text
                    },
                    RectTransform = { AnchorMin = "0.02 0.17", AnchorMax = "0.25 0.22" }
                }, UI_HUD);
                
                // Position
                string posInfo = $"Position: {hudData.Position}/{hudData.Racers}";
                container.Add(new CuiLabel
                {
                    Text = { 
                        Text = posInfo, 
                        FontSize = 14, 
                        Align = TextAnchor.MiddleLeft,
                        Color = config.Colors.Text
                    },
                    RectTransform = { AnchorMin = "0.02 0.22", AnchorMax = "0.2 0.27" }
                }, UI_HUD);
            }
            
            // Progress bar
            if (hudData.IsRace && hudData.Progress01 > 0)
            {
                // Background
                container.Add(new CuiPanel
                {
                    Image = { Color = config.Colors.Secondary },
                    RectTransform = { AnchorMin = "0.02 0.07", AnchorMax = "0.3 0.1" }
                }, UI_HUD, UI_HUD + ".ProgressBG");
                
                // Fill
                float progressWidth = 0.02f + (0.28f * hudData.Progress01);
                container.Add(new CuiPanel
                {
                    Image = { Color = config.Colors.Accent },
                    RectTransform = { AnchorMin = "0.02 0.07", AnchorMax = $"{progressWidth} 0.1" }
                }, UI_HUD, UI_HUD + ".ProgressFill");
                
                // Label
                container.Add(new CuiLabel
                {
                    Text = { 
                        Text = "Progress", 
                        FontSize = 10, 
                        Align = TextAnchor.MiddleCenter,
                        Color = config.Colors.Text
                    },
                    RectTransform = { AnchorMin = "0.02 0.07", AnchorMax = "0.3 0.1" }
                }, UI_HUD);
            }
            
            // Boost bar
            if (hudData.Boost01 > 0)
            {
                // Background
                container.Add(new CuiPanel
                {
                    Image = { Color = config.Colors.Secondary },
                    RectTransform = { AnchorMin = "0.02 0.03", AnchorMax = "0.3 0.06" }
                }, UI_HUD, UI_HUD + ".BoostBG");
                
                // Fill
                float boostWidth = 0.02f + (0.28f * hudData.Boost01);
                container.Add(new CuiPanel
                {
                    Image = { Color = config.Colors.Warning },
                    RectTransform = { AnchorMin = "0.02 0.03", AnchorMax = $"{boostWidth} 0.06" }
                }, UI_HUD, UI_HUD + ".BoostFill");
                
                // Label
                container.Add(new CuiLabel
                {
                    Text = { 
                        Text = "Boost", 
                        FontSize = 10, 
                        Align = TextAnchor.MiddleCenter,
                        Color = config.Colors.Text
                    },
                    RectTransform = { AnchorMin = "0.02 0.03", AnchorMax = "0.3 0.06" }
                }, UI_HUD);
            }
            
            // Compass (simple N/S/E/W indicator)
            Vector3 playerDir = player.eyes.HeadForward();
            float angle = Mathf.Atan2(playerDir.x, playerDir.z) * Mathf.Rad2Deg;
            string compass = GetCompassDirection(angle);
            
            container.Add(new CuiLabel
            {
                Text = { 
                    Text = compass, 
                    FontSize = 18, 
                    Align = TextAnchor.MiddleCenter,
                    Color = config.Colors.Text
                },
                RectTransform = { AnchorMin = "0.47 0.92", AnchorMax = "0.53 0.98" }
            }, UI_HUD);
            
            CuiHelper.DestroyUi(player, UI_HUD);
            CuiHelper.AddUi(player, container);
        }
        
        private string GetCompassDirection(float angle)
        {
            if (angle < 0) angle += 360;
            
            if (angle >= 337.5 || angle < 22.5) return "N";
            if (angle >= 22.5 && angle < 67.5) return "NE";
            if (angle >= 67.5 && angle < 112.5) return "E";
            if (angle >= 112.5 && angle < 157.5) return "SE";
            if (angle >= 157.5 && angle < 202.5) return "S";
            if (angle >= 202.5 && angle < 247.5) return "SW";
            if (angle >= 247.5 && angle < 292.5) return "W";
            return "NW";
        }
        
        #endregion
        
        #region UI Creation - Player Counter
        
        private void ShowPlayerCounter(BasePlayer player)
        {
            var counts = GetPlayerCounts();
            
            var container = new CuiElementContainer();
            
            // Counter panel (top right)
            container.Add(new CuiPanel
            {
                Image = { Color = config.Colors.Secondary },
                RectTransform = { AnchorMin = "0.85 0.92", AnchorMax = "0.98 0.98" }
            }, "Overlay", UI_COUNTER);
            
            // Counter text
            string counterText = $"Online: {counts.Online} | Lobby: {counts.Lobby} | Queue: {counts.Queue} | Race: {counts.InRace}";
            
            container.Add(new CuiLabel
            {
                Text = { 
                    Text = counterText, 
                    FontSize = 10, 
                    Align = TextAnchor.MiddleCenter,
                    Color = config.Colors.Text
                },
                RectTransform = { AnchorMin = "0.02 0", AnchorMax = "0.98 1" }
            }, UI_COUNTER);
            
            CuiHelper.DestroyUi(player, UI_COUNTER);
            CuiHelper.AddUi(player, container);
        }
        
        #endregion
        
        #region UI Creation - Voting Modal
        
        private void ShowVotingModal(BasePlayer player)
        {
            var votingData = GetVotingState(player);
            
            if (!votingData.Voting)
            {
                CuiHelper.DestroyUi(player, UI_VOTING);
                return;
            }
            
            var container = new CuiElementContainer();
            
            // Modal background
            container.Add(new CuiPanel
            {
                Image = { Color = config.Colors.Primary },
                RectTransform = { AnchorMin = "0.35 0.35", AnchorMax = "0.65 0.65" },
                CursorEnabled = true
            }, "Overlay", UI_VOTING);
            
            // Title
            container.Add(new CuiLabel
            {
                Text = { 
                    Text = "Vote for Race Mode", 
                    FontSize = 20, 
                    Align = TextAnchor.MiddleCenter,
                    Color = config.Colors.Text
                },
                RectTransform = { AnchorMin = "0.1 0.75", AnchorMax = "0.9 0.9" }
            }, UI_VOTING);
            
            // Time remaining
            container.Add(new CuiLabel
            {
                Text = { 
                    Text = $"Time remaining: {votingData.SecondsRemaining}s", 
                    FontSize = 14, 
                    Align = TextAnchor.MiddleCenter,
                    Color = config.Colors.Text
                },
                RectTransform = { AnchorMin = "0.1 0.65", AnchorMax = "0.9 0.75" }
            }, UI_VOTING);
            
            // Vote counts
            string voteCountText = $"Normal: {votingData.NormalVotes}  |  Battle: {votingData.BattleVotes}";
            container.Add(new CuiLabel
            {
                Text = { 
                    Text = voteCountText, 
                    FontSize = 16, 
                    Align = TextAnchor.MiddleCenter,
                    Color = config.Colors.Text
                },
                RectTransform = { AnchorMin = "0.1 0.5", AnchorMax = "0.9 0.65" }
            }, UI_VOTING);
            
            // Normal button
            string normalColor = votingData.MyVote == "normal" ? config.Colors.Success : config.Colors.Accent;
            container.Add(new CuiButton
            {
                Button = { Color = normalColor, Command = "hydroui.vote.normal" },
                RectTransform = { AnchorMin = "0.1 0.3", AnchorMax = "0.45 0.45" },
                Text = { 
                    Text = "Normal Race", 
                    FontSize = 14, 
                    Align = TextAnchor.MiddleCenter,
                    Color = config.Colors.Text
                }
            }, UI_VOTING);
            
            // Battle button
            string battleColor = votingData.MyVote == "battle" ? config.Colors.Success : config.Colors.Accent;
            container.Add(new CuiButton
            {
                Button = { Color = battleColor, Command = "hydroui.vote.battle" },
                RectTransform = { AnchorMin = "0.55 0.3", AnchorMax = "0.9 0.45" },
                Text = { 
                    Text = "Battle Race", 
                    FontSize = 14, 
                    Align = TextAnchor.MiddleCenter,
                    Color = config.Colors.Text
                }
            }, UI_VOTING);
            
            // Your vote indicator
            if (!string.IsNullOrEmpty(votingData.MyVote))
            {
                container.Add(new CuiLabel
                {
                    Text = { 
                        Text = $"Your vote: {votingData.MyVote.ToUpper()}", 
                        FontSize = 12, 
                        Align = TextAnchor.MiddleCenter,
                        Color = config.Colors.Success
                    },
                    RectTransform = { AnchorMin = "0.1 0.15", AnchorMax = "0.9 0.25" }
                }, UI_VOTING);
            }
            
            CuiHelper.DestroyUi(player, UI_VOTING);
            CuiHelper.AddUi(player, container);
        }
        
        #endregion
        
        #region UI Creation - Countdown
        
        private void ShowCountdown(BasePlayer player)
        {
            var hudData = GetHudData(player);
            
            if (hudData.CountdownSeconds <= 0)
            {
                CuiHelper.DestroyUi(player, UI_COUNTDOWN);
                return;
            }
            
            var container = new CuiElementContainer();
            
            // Countdown number (large center display)
            string countText = hudData.CountdownSeconds > 0 ? hudData.CountdownSeconds.ToString() : "GO!";
            string countColor = hudData.CountdownSeconds <= 3 ? config.Colors.Warning : config.Colors.Text;
            
            container.Add(new CuiLabel
            {
                Text = { 
                    Text = countText, 
                    FontSize = 72, 
                    Align = TextAnchor.MiddleCenter,
                    Color = countColor
                },
                RectTransform = { AnchorMin = "0.4 0.4", AnchorMax = "0.6 0.6" }
            }, "Overlay", UI_COUNTDOWN);
            
            CuiHelper.DestroyUi(player, UI_COUNTDOWN);
            CuiHelper.AddUi(player, container);
        }
        
        #endregion
        
        #region UI Creation - Menu Toggle & Quick Panel
        
        private void ShowMenuToggle(BasePlayer player)
        {
            var container = new CuiElementContainer();
            
            // Menu toggle button (top left)
            container.Add(new CuiButton
            {
                Button = { Color = config.Colors.Primary, Command = "hydroui.togglemenu" },
                RectTransform = { AnchorMin = "0.85 0.85", AnchorMax = "0.9 0.9" },
                Text = { 
                    Text = MENU_ICON, 
                    FontSize = 20, 
                    Align = TextAnchor.MiddleCenter,
                    Color = config.Colors.Text
                }
            }, "Overlay", UI_MAIN + ".MenuToggle");
            
            CuiHelper.DestroyUi(player, UI_MAIN + ".MenuToggle");
            CuiHelper.AddUi(player, container);
        }
        
        private void ShowQuickPanel(BasePlayer player)
        {
            var container = new CuiElementContainer();
            
            // Quick panel (right side)
            container.Add(new CuiPanel
            {
                Image = { Color = config.Colors.Secondary },
                RectTransform = { AnchorMin = "0.92 0.4", AnchorMax = "0.98 0.7" },
                CursorEnabled = true
            }, "Overlay", UI_QUICKPANEL);
            
            // Join button
            container.Add(new CuiButton
            {
                Button = { Color = config.Colors.Success, Command = "hydroui.join" },
                RectTransform = { AnchorMin = "0.1 0.75", AnchorMax = "0.9 0.9" },
                Text = { 
                    Text = "Join", 
                    FontSize = 12, 
                    Align = TextAnchor.MiddleCenter,
                    Color = config.Colors.Text
                }
            }, UI_QUICKPANEL);
            
            // Leave button
            container.Add(new CuiButton
            {
                Button = { Color = config.Colors.Danger, Command = "hydroui.leave" },
                RectTransform = { AnchorMin = "0.1 0.6", AnchorMax = "0.9 0.7" },
                Text = { 
                    Text = "Leave", 
                    FontSize = 12, 
                    Align = TextAnchor.MiddleCenter,
                    Color = config.Colors.Text
                }
            }, UI_QUICKPANEL);
            
            // Stats button
            container.Add(new CuiButton
            {
                Button = { Color = config.Colors.Accent, Command = "hydroui.stats" },
                RectTransform = { AnchorMin = "0.1 0.45", AnchorMax = "0.9 0.55" },
                Text = { 
                    Text = "Stats", 
                    FontSize = 12, 
                    Align = TextAnchor.MiddleCenter,
                    Color = config.Colors.Text
                }
            }, UI_QUICKPANEL);
            
            // Vote button
            container.Add(new CuiButton
            {
                Button = { Color = config.Colors.Warning, Command = "hydroui.openvote" },
                RectTransform = { AnchorMin = "0.1 0.3", AnchorMax = "0.9 0.4" },
                Text = { 
                    Text = "Vote", 
                    FontSize = 12, 
                    Align = TextAnchor.MiddleCenter,
                    Color = config.Colors.Text
                }
            }, UI_QUICKPANEL);
            
            CuiHelper.DestroyUi(player, UI_QUICKPANEL);
            CuiHelper.AddUi(player, container);
        }
        
        #endregion
        
        #region UI Creation - Track Editor
        
        private void ShowTrackEditor(BasePlayer player)
        {
            var state = GetPlayerState(player);
            var editorContext = GetEditorContext(player);
            
            if (editorContext == null) return;
            
            var container = new CuiElementContainer();
            
            // Editor panel (left side)
            container.Add(new CuiPanel
            {
                Image = { Color = config.Colors.Secondary },
                RectTransform = { AnchorMin = "0.01 0.2", AnchorMax = "0.25 0.8" },
                CursorEnabled = true
            }, "Overlay", UI_EDITOR);
            
            // Title
            container.Add(new CuiLabel
            {
                Text = { 
                    Text = "Track Editor", 
                    FontSize = 18, 
                    Align = TextAnchor.MiddleCenter,
                    Color = config.Colors.Text
                },
                RectTransform = { AnchorMin = "0.05 0.92", AnchorMax = "0.95 0.98" }
            }, UI_EDITOR);
            
            // Close button
            container.Add(new CuiButton
            {
                Button = { Color = config.Colors.Danger, Command = "hydroui.closeeditor" },
                RectTransform = { AnchorMin = "0.85 0.93", AnchorMax = "0.95 0.97" },
                Text = { 
                    Text = "X", 
                    FontSize = 12, 
                    Align = TextAnchor.MiddleCenter,
                    Color = config.Colors.Text
                }
            }, UI_EDITOR);
            
            // Context tabs
            float tabWidth = 0.3f;
            float tabStart = 0.05f;
            
            string[] contexts = { "Summary", "Tracks", "Inspector" };
            for (int i = 0; i < contexts.Length; i++)
            {
                string context = contexts[i].ToLower();
                string tabColor = editorContext.Context == context ? config.Colors.Accent : config.Colors.Primary;
                float xMin = tabStart + (i * tabWidth);
                float xMax = xMin + tabWidth - 0.02f;
                
                container.Add(new CuiButton
                {
                    Button = { Color = tabColor, Command = $"hydroui.editorcontext {context}" },
                    RectTransform = { AnchorMin = $"{xMin} 0.85", AnchorMax = $"{xMax} 0.91" },
                    Text = { 
                        Text = contexts[i], 
                        FontSize = 10, 
                        Align = TextAnchor.MiddleCenter,
                        Color = config.Colors.Text
                    }
                }, UI_EDITOR);
            }
            
            // Content area based on context
            if (editorContext.Context == "tracks")
            {
                ShowTrackList(container, player, editorContext);
            }
            else if (editorContext.Context == "summary")
            {
                ShowEditorSummary(container, editorContext);
            }
            else if (editorContext.Context == "inspector")
            {
                ShowEditorInspector(container, player);
            }
            
            CuiHelper.DestroyUi(player, UI_EDITOR);
            CuiHelper.AddUi(player, container);
        }
        
        private void ShowTrackList(CuiElementContainer container, BasePlayer player, dynamic editorContext)
        {
            var state = GetPlayerState(player);
            var tracks = editorContext.Tracks as List<object>;
            if (tracks == null) tracks = new List<object>();
            
            int tracksPerPage = config.UI.TracksPerPage;
            int totalPages = (int)Math.Ceiling(tracks.Count / (float)tracksPerPage);
            int currentPage = state.EditorPage;
            
            // Pagination info
            container.Add(new CuiLabel
            {
                Text = { 
                    Text = $"Page {currentPage + 1}/{Math.Max(1, totalPages)}", 
                    FontSize = 12, 
                    Align = TextAnchor.MiddleCenter,
                    Color = config.Colors.Text
                },
                RectTransform = { AnchorMin = "0.3 0.78", AnchorMax = "0.7 0.83" }
            }, UI_EDITOR);
            
            // Previous page button
            if (currentPage > 0)
            {
                container.Add(new CuiButton
                {
                    Button = { Color = config.Colors.Accent, Command = $"hydroui.editorpage {currentPage - 1}" },
                    RectTransform = { AnchorMin = "0.05 0.78", AnchorMax = "0.25 0.83" },
                    Text = { 
                        Text = "< Prev", 
                        FontSize = 10, 
                        Align = TextAnchor.MiddleCenter,
                        Color = config.Colors.Text
                    }
                }, UI_EDITOR);
            }
            
            // Next page button
            if (currentPage < totalPages - 1)
            {
                container.Add(new CuiButton
                {
                    Button = { Color = config.Colors.Accent, Command = $"hydroui.editorpage {currentPage + 1}" },
                    RectTransform = { AnchorMin = "0.75 0.78", AnchorMax = "0.95 0.83" },
                    Text = { 
                        Text = "Next >", 
                        FontSize = 10, 
                        Align = TextAnchor.MiddleCenter,
                        Color = config.Colors.Text
                    }
                }, UI_EDITOR);
            }
            
            // Track list
            int startIdx = currentPage * tracksPerPage;
            int endIdx = Math.Min(startIdx + tracksPerPage, tracks.Count);
            float itemHeight = 0.12f;
            float yStart = 0.65f;
            
            for (int i = startIdx; i < endIdx; i++)
            {
                var track = tracks[i] as Dictionary<string, object>;
                if (track == null) continue;
                
                string trackName = track.ContainsKey("Name") ? track["Name"].ToString() : "Unknown";
                string author = track.ContainsKey("Author") ? track["Author"].ToString() : "Unknown";
                int checkpoints = track.ContainsKey("Checkpoints") ? Convert.ToInt32(track["Checkpoints"]) : 0;
                int laps = track.ContainsKey("Laps") ? Convert.ToInt32(track["Laps"]) : 0;
                
                int listIdx = i - startIdx;
                float yMin = yStart - (listIdx * itemHeight) - itemHeight;
                float yMax = yStart - (listIdx * itemHeight);
                
                // Track item background
                container.Add(new CuiPanel
                {
                    Image = { Color = config.Colors.Primary },
                    RectTransform = { AnchorMin = $"0.05 {yMin}", AnchorMax = $"0.95 {yMax}" }
                }, UI_EDITOR, UI_EDITOR + $".Track{i}");
                
                // Track name
                container.Add(new CuiLabel
                {
                    Text = { 
                        Text = trackName, 
                        FontSize = 11, 
                        Align = TextAnchor.MiddleLeft,
                        Color = config.Colors.Text
                    },
                    RectTransform = { AnchorMin = "0.05 0.6", AnchorMax = "0.95 0.95" }
                }, UI_EDITOR + $".Track{i}");
                
                // Track info
                string info = $"Author: {author} | CP: {checkpoints} | Laps: {laps}";
                container.Add(new CuiLabel
                {
                    Text = { 
                        Text = info, 
                        FontSize = 9, 
                        Align = TextAnchor.MiddleLeft,
                        Color = config.Colors.Text
                    },
                    RectTransform = { AnchorMin = "0.05 0.05", AnchorMax = "0.95 0.5" }
                }, UI_EDITOR + $".Track{i}");
            }
        }
        
        private void ShowEditorSummary(CuiElementContainer container, dynamic editorContext)
        {
            var tracks = editorContext.Tracks as List<object>;
            int trackCount = tracks != null ? tracks.Count : 0;
            
            // Summary text
            string summary = $"Total Tracks: {trackCount}\n\n" +
                           "Use the Tracks tab to view and manage tracks.\n" +
                           "Use the Inspector tab for track details.";
            
            container.Add(new CuiLabel
            {
                Text = { 
                    Text = summary, 
                    FontSize = 12, 
                    Align = TextAnchor.UpperLeft,
                    Color = config.Colors.Text
                },
                RectTransform = { AnchorMin = "0.05 0.3", AnchorMax = "0.95 0.78" }
            }, UI_EDITOR);
        }
        
        private void ShowEditorInspector(CuiElementContainer container, BasePlayer player)
        {
            // Inspector actions and inputs
            container.Add(new CuiLabel
            {
                Text = { 
                    Text = "Track Inspector", 
                    FontSize = 14, 
                    Align = TextAnchor.MiddleCenter,
                    Color = config.Colors.Text
                },
                RectTransform = { AnchorMin = "0.05 0.7", AnchorMax = "0.95 0.78" }
            }, UI_EDITOR);
            
            // Example input field
            container.Add(new CuiLabel
            {
                Text = { 
                    Text = "Track Name:", 
                    FontSize = 11, 
                    Align = TextAnchor.MiddleLeft,
                    Color = config.Colors.Text
                },
                RectTransform = { AnchorMin = "0.05 0.6", AnchorMax = "0.95 0.68" }
            }, UI_EDITOR);
            
            // Actions
            container.Add(new CuiButton
            {
                Button = { Color = config.Colors.Success, Command = "hydroui.track.save" },
                RectTransform = { AnchorMin = "0.05 0.4", AnchorMax = "0.45 0.5" },
                Text = { 
                    Text = "Save", 
                    FontSize = 11, 
                    Align = TextAnchor.MiddleCenter,
                    Color = config.Colors.Text
                }
            }, UI_EDITOR);
            
            container.Add(new CuiButton
            {
                Button = { Color = config.Colors.Danger, Command = "hydroui.track.delete" },
                RectTransform = { AnchorMin = "0.55 0.4", AnchorMax = "0.95 0.5" },
                Text = { 
                    Text = "Delete", 
                    FontSize = 11, 
                    Align = TextAnchor.MiddleCenter,
                    Color = config.Colors.Text
                }
            }, UI_EDITOR);
        }
        
        #endregion
        
        #region UI Update Methods
        
        private void UpdateAllHuds()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player == null || !player.IsConnected) continue;
                
                var hudData = GetHudData(player);
                var state = GetPlayerState(player);
                
                string currentData = JsonConvert.SerializeObject(hudData);
                if (currentData != state.LastHudData)
                {
                    state.LastHudData = currentData;
                    ShowHUD(player);
                }
                
                // Update voting modal
                var votingData = GetVotingState(player);
                string votingJson = JsonConvert.SerializeObject(votingData);
                if (votingJson != state.LastVotingData)
                {
                    state.LastVotingData = votingJson;
                    ShowVotingModal(player);
                }
                
                // Update countdown
                string countdownData = hudData.CountdownSeconds.ToString();
                if (countdownData != state.LastCountdownData)
                {
                    state.LastCountdownData = countdownData;
                    ShowCountdown(player);
                }
            }
        }
        
        private void UpdateAllCounters()
        {
            var counts = GetPlayerCounts();
            string countsJson = JsonConvert.SerializeObject(counts);
            
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player == null || !player.IsConnected) continue;
                
                var state = GetPlayerState(player);
                if (countsJson != state.LastCounterData)
                {
                    state.LastCounterData = countsJson;
                    ShowPlayerCounter(player);
                }
            }
        }
        
        private void UpdateAllEditors()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player == null || !player.IsConnected) continue;
                
                var state = GetPlayerState(player);
                if (state.EditorOpen)
                {
                    ShowTrackEditor(player);
                }
            }
        }
        
        #endregion
        
        #region Helper Methods
        
        private PlayerUIState GetPlayerState(BasePlayer player)
        {
            if (!playerStates.ContainsKey(player.userID))
            {
                playerStates[player.userID] = new PlayerUIState();
            }
            return playerStates[player.userID];
        }
        
        private PlayerPrefs GetPlayerPrefs(BasePlayer player)
        {
            if (!playerPrefs.ContainsKey(player.userID))
            {
                playerPrefs[player.userID] = new PlayerPrefs();
            }
            return playerPrefs[player.userID];
        }
        
        private dynamic GetHudData(BasePlayer player)
        {
            if (HydroRust != null)
            {
                var data = HydroRust.Call("UI_GetHudData", player);
                if (data != null) return data;
            }
            
            // Fallback
            return new
            {
                TrackName = "",
                Lap = 0,
                TotalLaps = 0,
                Checkpoint = 0,
                TotalCheckpoints = 0,
                Progress01 = 0f,
                Speed = 0f,
                Boost01 = 0f,
                Position = 0,
                Racers = 0,
                IsRace = false,
                Finished = false,
                FinishTime = 0f,
                RaceMode = "normal",
                Voting = false,
                VoteSecondsRemaining = 0,
                CountdownSeconds = 0
            };
        }
        
        private dynamic GetVotingState(BasePlayer player)
        {
            if (HydroRust != null)
            {
                var data = HydroRust.Call("UI_GetVotingState", player);
                if (data != null) return data;
            }
            
            // Fallback
            return new
            {
                Voting = false,
                SecondsRemaining = 0,
                NormalVotes = 0,
                BattleVotes = 0,
                TotalParticipants = 0,
                MyVote = ""
            };
        }
        
        private dynamic GetPlayerCounts()
        {
            if (HydroRust != null)
            {
                var data = HydroRust.Call("UI_GetCounts");
                if (data != null) return data;
            }
            
            // Fallback
            return new
            {
                Online = BasePlayer.activePlayerList.Count,
                Lobby = 0,
                Queue = 0,
                InRace = 0
            };
        }
        
        private dynamic GetEditorContext(BasePlayer player)
        {
            if (HydroRust != null)
            {
                var data = HydroRust.Call("UI_GetEditorContext", player);
                if (data != null) return data;
            }
            
            // Fallback
            return new
            {
                Context = "summary",
                Tracks = new List<object>()
            };
        }
        
        private void DestroyUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, UI_MAIN);
            CuiHelper.DestroyUi(player, UI_MENU);
            CuiHelper.DestroyUi(player, UI_WELCOME);
            CuiHelper.DestroyUi(player, UI_HUD);
            CuiHelper.DestroyUi(player, UI_COUNTER);
            CuiHelper.DestroyUi(player, UI_VOTING);
            CuiHelper.DestroyUi(player, UI_COUNTDOWN);
            CuiHelper.DestroyUi(player, UI_EDITOR);
            CuiHelper.DestroyUi(player, UI_QUICKPANEL);
        }
        
        private void RunChat(BasePlayer player, string command)
        {
            if (player == null || string.IsNullOrEmpty(command)) return;
            player.SendConsoleCommand("chat.say", command);
        }
        
        #endregion
        
        #region Console Commands
        
        [ConsoleCommand("hydroui.closewelcome")]
        private void CmdCloseWelcome(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            
            CuiHelper.DestroyUi(player, UI_WELCOME);
        }
        
        [ConsoleCommand("hydroui.togglemenu")]
        private void CmdToggleMenu(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            
            var state = GetPlayerState(player);
            state.MenuOpen = !state.MenuOpen;
            
            if (state.MenuOpen)
            {
                ShowQuickPanel(player);
            }
            else
            {
                CuiHelper.DestroyUi(player, UI_QUICKPANEL);
            }
        }
        
        [ConsoleCommand("hydroui.join")]
        private void CmdJoin(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            
            RunChat(player, config.ChatCommands.JoinTemplate);
            
            var state = GetPlayerState(player);
            state.WelcomeLocked = false;
        }
        
        [ConsoleCommand("hydroui.leave")]
        private void CmdLeave(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            
            RunChat(player, config.ChatCommands.LeaveTemplate);
        }
        
        [ConsoleCommand("hydroui.stats")]
        private void CmdStats(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            
            SendReply(player, "Stats feature coming soon!");
        }
        
        [ConsoleCommand("hydroui.openvote")]
        private void CmdOpenVote(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            
            ShowVotingModal(player);
        }
        
        [ConsoleCommand("hydroui.vote.normal")]
        private void CmdVoteNormal(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            
            RunChat(player, config.ChatCommands.VoteNormalTemplate);
        }
        
        [ConsoleCommand("hydroui.vote.battle")]
        private void CmdVoteBattle(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            
            RunChat(player, config.ChatCommands.VoteBattleTemplate);
        }
        
        [ConsoleCommand("hydroui.openeditor")]
        private void CmdOpenEditor(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            
            var state = GetPlayerState(player);
            state.EditorOpen = true;
            ShowTrackEditor(player);
        }
        
        [ConsoleCommand("hydroui.closeeditor")]
        private void CmdCloseEditor(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            
            var state = GetPlayerState(player);
            state.EditorOpen = false;
            CuiHelper.DestroyUi(player, UI_EDITOR);
        }
        
        [ConsoleCommand("hydroui.editorcontext")]
        private void CmdEditorContext(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            
            string context = arg.GetString(0, "summary");
            
            // Store context via HydroRust or locally
            ShowTrackEditor(player);
        }
        
        [ConsoleCommand("hydroui.editorpage")]
        private void CmdEditorPage(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            
            int page = arg.GetInt(0, 0);
            var state = GetPlayerState(player);
            state.EditorPage = page;
            
            ShowTrackEditor(player);
        }
        
        [ConsoleCommand("hydroui.track.save")]
        private void CmdTrackSave(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            
            SendReply(player, "Track save feature coming soon!");
        }
        
        [ConsoleCommand("hydroui.track.delete")]
        private void CmdTrackDelete(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            
            SendReply(player, "Track delete feature coming soon!");
        }
        
        #endregion
        
        #region Chat Commands
        
        [ChatCommand("hydroui")]
        private void HydroUICommand(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;
            
            if (args.Length == 0)
            {
                SendReply(player, "HydroUI v2.7.0 - Use /hydroui help for commands");
                return;
            }
            
            switch (args[0].ToLower())
            {
                case "show":
                    ShowHUD(player);
                    ShowPlayerCounter(player);
                    ShowMenuToggle(player);
                    SendReply(player, "UI shown");
                    break;
                    
                case "hide":
                    DestroyUI(player);
                    SendReply(player, "UI hidden");
                    break;
                    
                case "editor":
                    var state = GetPlayerState(player);
                    state.EditorOpen = true;
                    ShowTrackEditor(player);
                    SendReply(player, "Editor opened");
                    break;
                    
                case "scale":
                    if (args.Length < 2)
                    {
                        SendReply(player, "Usage: /hydroui scale <0.5-2.0>");
                        return;
                    }
                    float scale;
                    if (float.TryParse(args[1], out scale))
                    {
                        scale = Mathf.Clamp(scale, 0.5f, 2.0f);
                        var prefs = GetPlayerPrefs(player);
                        prefs.Scale = scale;
                        SaveData();
                        SendReply(player, $"UI scale set to {scale}");
                    }
                    break;
                    
                case "help":
                    SendReply(player, "Commands: show, hide, editor, scale");
                    break;
                    
                default:
                    SendReply(player, "Unknown command. Use /hydroui help");
                    break;
            }
        }
        
        #endregion
    }
}
