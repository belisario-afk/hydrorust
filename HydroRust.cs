using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("HydroRust", "HydroRust", "5.2.0")]
    [Description("Hydrofoil racing plugin with physics stabilization and UI endpoints")]
    class HydroRust : RustPlugin
    {
        #region Fields

        [PluginReference] private Plugin HydroLobby;
        [PluginReference] private Plugin ImageLibrary;

        private ConfigData configData;
        private StoredData storedData;

        private Dictionary<ulong, PlayerData> playerData = new Dictionary<ulong, PlayerData>();
        private Dictionary<ulong, RaceState> raceStates = new Dictionary<ulong, RaceState>();
        private List<Track> tracks = new List<Track>();
        private VotingSession currentVoting = null;
        private CountdownState countdown = null;
        private List<BasePlayer> activePlayers = new List<BasePlayer>();

        private Timer physicsTimer;
        private Timer votingTimer;
        private Timer countdownTimer;

        private const string PermissionUse = "hydrorust.use";
        private const string PermissionAdmin = "hydrorust.admin";

        #endregion

        #region Configuration

        private class ConfigData
        {
            [JsonProperty("Controls")]
            public ControlsConfig Controls { get; set; } = new ControlsConfig();

            [JsonProperty("Lobby")]
            public LobbyConfig Lobby { get; set; } = new LobbyConfig();

            [JsonProperty("Race")]
            public RaceConfig Race { get; set; } = new RaceConfig();
        }

        private class ControlsConfig
        {
            [JsonProperty("GroundedStabilizeStrength")]
            public float GroundedStabilizeStrength { get; set; } = 50f;

            [JsonProperty("AirborneStabilizeStrength")]
            public float AirborneStabilizeStrength { get; set; } = 30f;

            [JsonProperty("RollDamping")]
            public float RollDamping { get; set; } = 0.8f;

            [JsonProperty("PitchDamping")]
            public float PitchDamping { get; set; } = 0.8f;

            [JsonProperty("StickToWaterForce")]
            public float StickToWaterForce { get; set; } = 10f;

            [JsonProperty("AirborneBoostScale")]
            public float AirborneBoostScale { get; set; } = 0.3f;

            [JsonProperty("AirborneExtraDrag")]
            public float AirborneExtraDrag { get; set; } = 2f;

            [JsonProperty("MaxPitchDegrees")]
            public float MaxPitchDegrees { get; set; } = 30f;

            [JsonProperty("MaxRollDegrees")]
            public float MaxRollDegrees { get; set; } = 35f;

            [JsonProperty("MaxVerticalVelocity")]
            public float MaxVerticalVelocity { get; set; } = 15f;

            [JsonProperty("ExtraGravityInAir")]
            public float ExtraGravityInAir { get; set; } = 5f;

            [JsonProperty("CenterOfMassOffset")]
            public float CenterOfMassOffset { get; set; } = -0.5f;
        }

        private class LobbyConfig
        {
            [JsonProperty("Position")]
            public Vector3 Position { get; set; } = new Vector3(0, 0, 0);

            [JsonProperty("Radius")]
            public float Radius { get; set; } = 50f;
        }

        private class RaceConfig
        {
            [JsonProperty("VotingDurationSeconds")]
            public int VotingDurationSeconds { get; set; } = 30;

            [JsonProperty("CountdownSeconds")]
            public int CountdownSeconds { get; set; } = 5;

            [JsonProperty("MinPlayersForVoting")]
            public int MinPlayersForVoting { get; set; } = 2;
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

        private class StoredData
        {
            public List<Track> Tracks { get; set; } = new List<Track>();
            public Dictionary<ulong, PlayerStats> PlayerStats { get; set; } = new Dictionary<ulong, PlayerStats>();
        }

        private class Track
        {
            public string Name { get; set; }
            public List<Vector3> Checkpoints { get; set; } = new List<Vector3>();
            public int TotalLaps { get; set; } = 3;
            public bool IsRace { get; set; } = true;
            public string Creator { get; set; }
        }

        private class PlayerData
        {
            public BasePlayer Player { get; set; }
            public bool InLobby { get; set; }
            public bool InQueue { get; set; }
            public bool InRace { get; set; }
            public string CurrentVote { get; set; }
        }

        private class PlayerStats
        {
            public int RacesCompleted { get; set; }
            public int Wins { get; set; }
            public float BestTime { get; set; }
        }

        private class RaceState
        {
            public Track CurrentTrack { get; set; }
            public int CurrentLap { get; set; } = 1;
            public int CurrentCheckpoint { get; set; } = 0;
            public float Progress { get; set; } = 0f;
            public float Speed { get; set; } = 0f;
            public float Boost { get; set; } = 1f;
            public int Position { get; set; } = 1;
            public int TotalRacers { get; set; } = 1;
            public bool Finished { get; set; } = false;
            public float FinishTime { get; set; } = 0f;
            public float StartTime { get; set; }
        }

        private class VotingSession
        {
            public DateTime StartTime { get; set; }
            public int DurationSeconds { get; set; }
            public Dictionary<string, int> Votes { get; set; } = new Dictionary<string, int>();
            public List<ulong> Participants { get; set; } = new List<ulong>();
        }

        private class CountdownState
        {
            public DateTime StartTime { get; set; }
            public int TotalSeconds { get; set; }
            public int RemainingSeconds { get; set; }
        }

        #endregion

        #region Oxide Hooks

        private void Init()
        {
            permission.RegisterPermission(PermissionUse, this);
            permission.RegisterPermission(PermissionAdmin, this);

            LoadConfig();
            LoadData();

            // Initialize default tracks if none exist
            if (tracks.Count == 0)
            {
                tracks.Add(new Track
                {
                    Name = "Default Track",
                    TotalLaps = 3,
                    IsRace = true,
                    Creator = "System"
                });
            }
        }

        private void OnServerInitialized()
        {
            // Start physics update timer (10Hz)
            physicsTimer = timer.Every(0.1f, UpdatePhysics);
        }

        private void Unload()
        {
            physicsTimer?.Destroy();
            votingTimer?.Destroy();
            countdownTimer?.Destroy();

            SaveData();
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (!playerData.ContainsKey(player.userID))
            {
                playerData[player.userID] = new PlayerData
                {
                    Player = player,
                    InLobby = false,
                    InQueue = false,
                    InRace = false
                };
            }
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            if (playerData.ContainsKey(player.userID))
            {
                var data = playerData[player.userID];
                data.InLobby = false;
                data.InQueue = false;
                data.InRace = false;
            }
        }

        private void OnEntitySpawned(BaseEntity entity)
        {
            if (entity is MotorRowboat boat)
            {
                // Lower center of mass for stability
                var rb = boat.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.centerOfMass = new Vector3(0, configData.Controls.CenterOfMassOffset, 0);
                }
            }
        }

        #endregion

        #region Data Management

        private void LoadData()
        {
            try
            {
                storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>("HydroRust");
                if (storedData != null && storedData.Tracks != null)
                {
                    tracks = storedData.Tracks;
                }
            }
            catch
            {
                storedData = new StoredData();
            }
        }

        private void SaveData()
        {
            if (storedData == null)
                storedData = new StoredData();

            storedData.Tracks = tracks;
            Interface.Oxide.DataFileSystem.WriteObject("HydroRust", storedData);
        }

        #endregion

        #region Physics Stabilization

        private void UpdatePhysics()
        {
            foreach (var boat in BaseNetworkable.serverEntities.OfType<MotorRowboat>())
            {
                if (boat == null || boat.IsDestroyed)
                    continue;

                var rb = boat.GetComponent<Rigidbody>();
                if (rb == null)
                    continue;

                var player = boat.GetDriver();
                if (player == null)
                    continue;

                // Check if airborne
                bool isGrounded = IsBoatGrounded(boat);

                // Get local rotation angles
                var localRotation = boat.transform.localRotation.eulerAngles;
                float pitch = NormalizeAngle(localRotation.x);
                float roll = NormalizeAngle(localRotation.z);

                // Apply stabilization
                if (isGrounded)
                {
                    ApplyGroundedStabilization(boat, rb, pitch, roll);
                }
                else
                {
                    ApplyAirborneStabilization(boat, rb, pitch, roll);
                }

                // Update race state
                UpdateRaceState(player, boat, rb);
            }
        }

        private bool IsBoatGrounded(MotorRowboat boat)
        {
            RaycastHit hit;
            Vector3 origin = boat.transform.position;
            Vector3 direction = Vector3.down;
            float maxDistance = 2f;

            return Physics.Raycast(origin, direction, out hit, maxDistance, LayerMask.GetMask("Water", "Terrain", "World"));
        }

        private void ApplyGroundedStabilization(MotorRowboat boat, Rigidbody rb, float pitch, float roll)
        {
            float stabStrength = configData.Controls.GroundedStabilizeStrength;

            // Stabilize pitch
            float pitchTorque = -pitch * stabStrength * configData.Controls.PitchDamping;
            rb.AddRelativeTorque(pitchTorque, 0, 0, ForceMode.Force);

            // Stabilize roll
            float rollTorque = -roll * stabStrength * configData.Controls.RollDamping;
            rb.AddRelativeTorque(0, 0, rollTorque, ForceMode.Force);

            // Stick to water force
            rb.AddForce(Vector3.down * configData.Controls.StickToWaterForce, ForceMode.Force);

            // Damp angular velocity
            rb.angularVelocity = new Vector3(
                rb.angularVelocity.x * 0.95f,
                rb.angularVelocity.y * 0.98f,
                rb.angularVelocity.z * 0.95f
            );
        }

        private void ApplyAirborneStabilization(MotorRowboat boat, Rigidbody rb, float pitch, float roll)
        {
            float stabStrength = configData.Controls.AirborneStabilizeStrength;

            // Stabilize pitch (less aggressive)
            float pitchTorque = -pitch * stabStrength * configData.Controls.PitchDamping;
            rb.AddRelativeTorque(pitchTorque, 0, 0, ForceMode.Force);

            // Stabilize roll (less aggressive)
            float rollTorque = -roll * stabStrength * configData.Controls.RollDamping;
            rb.AddRelativeTorque(0, 0, rollTorque, ForceMode.Force);

            // Extra gravity and drag in air
            rb.AddForce(Vector3.down * configData.Controls.ExtraGravityInAir, ForceMode.Force);
            rb.AddForce(-rb.velocity * configData.Controls.AirborneExtraDrag, ForceMode.Force);

            // Clamp pitch and roll
            ClampRotation(boat, configData.Controls.MaxPitchDegrees, configData.Controls.MaxRollDegrees);

            // Clamp vertical velocity
            if (Mathf.Abs(rb.velocity.y) > configData.Controls.MaxVerticalVelocity)
            {
                rb.velocity = new Vector3(
                    rb.velocity.x,
                    Mathf.Sign(rb.velocity.y) * configData.Controls.MaxVerticalVelocity,
                    rb.velocity.z
                );
            }

            // Damp angular velocity more aggressively
            rb.angularVelocity = new Vector3(
                rb.angularVelocity.x * 0.9f,
                rb.angularVelocity.y * 0.95f,
                rb.angularVelocity.z * 0.9f
            );
        }

        private void ClampRotation(MotorRowboat boat, float maxPitch, float maxRoll)
        {
            var euler = boat.transform.localRotation.eulerAngles;
            float pitch = NormalizeAngle(euler.x);
            float roll = NormalizeAngle(euler.z);

            pitch = Mathf.Clamp(pitch, -maxPitch, maxPitch);
            roll = Mathf.Clamp(roll, -maxRoll, maxRoll);

            boat.transform.localRotation = Quaternion.Euler(pitch, euler.y, roll);
        }

        private float NormalizeAngle(float angle)
        {
            if (angle > 180f)
                return angle - 360f;
            return angle;
        }

        private void UpdateRaceState(BasePlayer player, MotorRowboat boat, Rigidbody rb)
        {
            if (!raceStates.ContainsKey(player.userID))
                return;

            var state = raceStates[player.userID];
            state.Speed = rb.velocity.magnitude;

            // Update boost based on airborne state
            if (!IsBoatGrounded(boat))
            {
                state.Boost = Mathf.Max(0.1f, state.Boost * configData.Controls.AirborneBoostScale);
            }
            else
            {
                state.Boost = Mathf.Min(1f, state.Boost + 0.01f);
            }
        }

        #endregion

        #region Voting System

        private void StartVoting(List<ulong> participants)
        {
            if (participants.Count < configData.Race.MinPlayersForVoting)
            {
                PrintWarning("Not enough players for voting");
                return;
            }

            currentVoting = new VotingSession
            {
                StartTime = DateTime.UtcNow,
                DurationSeconds = configData.Race.VotingDurationSeconds,
                Participants = new List<ulong>(participants)
            };

            currentVoting.Votes["normal"] = 0;
            currentVoting.Votes["battle"] = 0;

            // Start voting countdown timer
            votingTimer = timer.Every(1f, () =>
            {
                var elapsed = (DateTime.UtcNow - currentVoting.StartTime).TotalSeconds;
                if (elapsed >= currentVoting.DurationSeconds)
                {
                    EndVoting();
                }
            });

            foreach (var uid in participants)
            {
                var player = BasePlayer.FindByID(uid);
                if (player != null)
                {
                    player.ChatMessage("Voting started! Use /hydro vote normal or /hydro vote battle");
                }
            }
        }

        private void EndVoting()
        {
            votingTimer?.Destroy();
            votingTimer = null;

            if (currentVoting == null)
                return;

            // Determine winner - on tie, default to normal mode
            int normalVotes = currentVoting.Votes["normal"];
            int battleVotes = currentVoting.Votes["battle"];
            string winner = normalVotes >= battleVotes ? "normal" : "battle";

            foreach (var uid in currentVoting.Participants)
            {
                var player = BasePlayer.FindByID(uid);
                if (player != null)
                {
                    player.ChatMessage($"Voting ended! Mode: {winner}");
                }
            }

            // Start countdown
            StartCountdown(currentVoting.Participants, winner);
            currentVoting = null;
        }

        private void RecordVote(BasePlayer player, string voteType)
        {
            if (currentVoting == null)
            {
                player.ChatMessage("No active voting session");
                return;
            }

            if (!currentVoting.Participants.Contains(player.userID))
            {
                player.ChatMessage("You are not in this voting session");
                return;
            }

            if (!playerData.ContainsKey(player.userID))
                return;

            var data = playerData[player.userID];

            // Remove old vote
            if (!string.IsNullOrEmpty(data.CurrentVote) && currentVoting.Votes.ContainsKey(data.CurrentVote))
            {
                currentVoting.Votes[data.CurrentVote]--;
            }

            // Add new vote
            if (currentVoting.Votes.ContainsKey(voteType))
            {
                currentVoting.Votes[voteType]++;
                data.CurrentVote = voteType;
                player.ChatMessage($"Voted for {voteType} mode");
            }
        }

        #endregion

        #region Countdown System

        private void StartCountdown(List<ulong> participants, string mode)
        {
            countdown = new CountdownState
            {
                StartTime = DateTime.UtcNow,
                TotalSeconds = configData.Race.CountdownSeconds,
                RemainingSeconds = configData.Race.CountdownSeconds
            };

            countdownTimer = timer.Every(1f, () =>
            {
                countdown.RemainingSeconds--;

                if (countdown.RemainingSeconds <= 0)
                {
                    StartRace(participants, mode);
                    countdown = null;
                    countdownTimer?.Destroy();
                    countdownTimer = null;
                }
                else
                {
                    foreach (var uid in participants)
                    {
                        var player = BasePlayer.FindByID(uid);
                        if (player != null)
                        {
                            player.ChatMessage($"Race starts in {countdown.RemainingSeconds}...");
                        }
                    }
                }
            });
        }

        private void StartRace(List<ulong> participants, string mode)
        {
            foreach (var uid in participants)
            {
                var player = BasePlayer.FindByID(uid);
                if (player != null)
                {
                    player.ChatMessage("GO!");

                    if (!raceStates.ContainsKey(uid))
                    {
                        raceStates[uid] = new RaceState
                        {
                            CurrentTrack = tracks.FirstOrDefault(),
                            StartTime = Time.realtimeSinceStartup
                        };
                    }

                    if (playerData.ContainsKey(uid))
                    {
                        playerData[uid].InRace = true;
                        playerData[uid].CurrentVote = null;
                    }
                }
            }
        }

        #endregion

        #region UI Endpoints

        public object UI_GetHudData(BasePlayer player)
        {
            if (player == null)
                return null;

            var result = new Dictionary<string, object>();

            // Default values
            result["TrackName"] = "No Track";
            result["Lap"] = 0;
            result["TotalLaps"] = 0;
            result["Checkpoint"] = 0;
            result["TotalCheckpoints"] = 0;
            result["Progress01"] = 0f;
            result["Speed"] = 0f;
            result["Boost01"] = 1f;
            result["Position"] = 0;
            result["Racers"] = 0;
            result["IsRace"] = true;
            result["Finished"] = false;
            result["FinishTime"] = 0f;
            result["RaceMode"] = "normal";
            result["Voting"] = false;
            result["VoteSecondsRemaining"] = 0;
            result["CountdownSeconds"] = 0;

            // Voting state
            if (currentVoting != null)
            {
                result["Voting"] = true;
                var elapsed = (DateTime.UtcNow - currentVoting.StartTime).TotalSeconds;
                result["VoteSecondsRemaining"] = (int)Math.Max(0, currentVoting.DurationSeconds - elapsed);
            }

            // Countdown state
            if (countdown != null)
            {
                result["CountdownSeconds"] = countdown.RemainingSeconds;
            }

            // Race state
            if (raceStates.ContainsKey(player.userID))
            {
                var state = raceStates[player.userID];
                if (state.CurrentTrack != null)
                {
                    result["TrackName"] = state.CurrentTrack.Name;
                    result["TotalLaps"] = state.CurrentTrack.TotalLaps;
                    result["TotalCheckpoints"] = state.CurrentTrack.Checkpoints.Count;
                    // IsRace: legacy boolean, true during active racing
                    result["IsRace"] = state.CurrentTrack.IsRace;
                    // RaceMode: string for UI display - "normal" for standard racing, "battle" for combat
                    // Track.IsRace=true maps to "normal", false maps to "battle"
                    result["RaceMode"] = state.CurrentTrack.IsRace ? "normal" : "battle";
                }

                result["Lap"] = state.CurrentLap;
                result["Checkpoint"] = state.CurrentCheckpoint;
                result["Progress01"] = state.Progress;
                result["Speed"] = state.Speed;
                result["Boost01"] = state.Boost;
                result["Position"] = state.Position;
                result["Racers"] = state.TotalRacers;
                result["Finished"] = state.Finished;
                result["FinishTime"] = state.FinishTime;
            }

            return result;
        }

        public object UI_GetVotingState(BasePlayer player)
        {
            var result = new Dictionary<string, object>();

            result["Voting"] = false;
            result["SecondsRemaining"] = 0;
            result["NormalVotes"] = 0;
            result["BattleVotes"] = 0;
            result["TotalParticipants"] = 0;
            result["MyVote"] = "";

            if (currentVoting != null)
            {
                result["Voting"] = true;
                var elapsed = (DateTime.UtcNow - currentVoting.StartTime).TotalSeconds;
                result["SecondsRemaining"] = (int)Math.Max(0, currentVoting.DurationSeconds - elapsed);
                result["NormalVotes"] = currentVoting.Votes.ContainsKey("normal") ? currentVoting.Votes["normal"] : 0;
                result["BattleVotes"] = currentVoting.Votes.ContainsKey("battle") ? currentVoting.Votes["battle"] : 0;
                result["TotalParticipants"] = currentVoting.Participants.Count;

                if (player != null && playerData.ContainsKey(player.userID))
                {
                    result["MyVote"] = playerData[player.userID].CurrentVote ?? "";
                }
            }

            return result;
        }

        public object UI_GetCounts()
        {
            var result = new Dictionary<string, object>();

            int online = BasePlayer.activePlayerList.Count;
            int lobby = 0;
            int queue = 0;
            int inRace = 0;

            // Try HydroLobby first
            if (HydroLobby != null)
            {
                var lobbyCount = HydroLobby.Call("GetLobbyCount");
                if (lobbyCount != null && lobbyCount is int)
                {
                    lobby = (int)lobbyCount;
                }
            }
            else
            {
                // Fallback: proximity to lobby position
                foreach (var player in BasePlayer.activePlayerList)
                {
                    if (Vector3.Distance(player.transform.position, configData.Lobby.Position) <= configData.Lobby.Radius)
                    {
                        lobby++;
                    }
                }
            }

            // Count queue and race players
            foreach (var kvp in playerData)
            {
                if (kvp.Value.InQueue)
                    queue++;
                if (kvp.Value.InRace)
                    inRace++;
            }

            result["Online"] = online;
            result["Lobby"] = lobby;
            result["Queue"] = queue;
            result["InRace"] = inRace;

            return result;
        }

        public object UI_ListTrackNames()
        {
            return tracks.Select(t => t.Name).ToList();
        }

        public object UI_GetEditorContext(BasePlayer player)
        {
            var result = new Dictionary<string, object>();

            result["IsAdmin"] = permission.UserHasPermission(player.UserIDString, PermissionAdmin);
            result["TrackCount"] = tracks.Count;
            result["CurrentTrack"] = tracks.FirstOrDefault()?.Name ?? "";

            return result;
        }

        #endregion

        #region Chat Commands

        [ChatCommand("hydro")]
        private void HydroCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PermissionUse))
            {
                player.ChatMessage("You don't have permission to use this command");
                return;
            }

            if (args.Length == 0)
            {
                player.ChatMessage($"{Title} v{Version} - Use /hydro help for commands");
                return;
            }

            switch (args[0].ToLower())
            {
                case "join":
                    JoinLobby(player);
                    break;

                case "leave":
                    LeaveLobby(player);
                    break;

                case "vote":
                    if (args.Length < 2)
                    {
                        player.ChatMessage("Usage: /hydro vote <normal|battle>");
                        return;
                    }
                    RecordVote(player, args[1].ToLower());
                    break;

                case "startvote":
                    if (!permission.UserHasPermission(player.UserIDString, PermissionAdmin))
                    {
                        player.ChatMessage("You don't have permission for this command");
                        return;
                    }
                    var lobbyPlayers = playerData.Where(p => p.Value.InLobby).Select(p => p.Key).ToList();
                    StartVoting(lobbyPlayers);
                    break;

                case "help":
                    player.ChatMessage("HydroRust Commands:\n" +
                        "/hydro join - Join the lobby\n" +
                        "/hydro leave - Leave the lobby\n" +
                        "/hydro vote <normal|battle> - Vote for race mode\n" +
                        "/hydro stats - View your statistics");
                    break;

                case "stats":
                    ShowStats(player);
                    break;

                default:
                    player.ChatMessage("Unknown command. Use /hydro help");
                    break;
            }
        }

        private void JoinLobby(BasePlayer player)
        {
            if (!playerData.ContainsKey(player.userID))
            {
                playerData[player.userID] = new PlayerData { Player = player };
            }

            playerData[player.userID].InLobby = true;
            player.ChatMessage("Joined the lobby");
        }

        private void LeaveLobby(BasePlayer player)
        {
            if (playerData.ContainsKey(player.userID))
            {
                playerData[player.userID].InLobby = false;
                playerData[player.userID].InQueue = false;
                playerData[player.userID].InRace = false;
            }
            player.ChatMessage("Left the lobby");
        }

        private void ShowStats(BasePlayer player)
        {
            if (storedData.PlayerStats.ContainsKey(player.userID))
            {
                var stats = storedData.PlayerStats[player.userID];
                player.ChatMessage($"Races: {stats.RacesCompleted} | Wins: {stats.Wins} | Best Time: {stats.BestTime:F2}s");
            }
            else
            {
                player.ChatMessage("No statistics available yet");
            }
        }

        #endregion
    }
}
