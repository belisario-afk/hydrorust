using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("HydroRust", "HydroTeam", "5.2.0")]
    [Description("Boat racing plugin with physics stabilization and UI integration")]
    class HydroRust : RustPlugin
    {
        #region Fields
        
        private Configuration config;
        private Dictionary<ulong, PlayerData> playerData = new Dictionary<ulong, PlayerData>();
        private Dictionary<ulong, BoatPhysicsData> boatPhysics = new Dictionary<ulong, BoatPhysicsData>();
        private List<TrackData> tracks = new List<TrackData>();
        private RaceState currentRace = new RaceState();
        private VotingState votingState = new VotingState();
        
        [PluginReference]
        private Plugin HydroLobby;
        
        #endregion
        
        #region Configuration
        
        private class Configuration
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
            public float AirborneStabilizeStrength { get; set; } = 20f;
            
            [JsonProperty("RollDamping")]
            public float RollDamping { get; set; } = 0.8f;
            
            [JsonProperty("PitchDamping")]
            public float PitchDamping { get; set; } = 0.8f;
            
            [JsonProperty("StickToWaterForce")]
            public float StickToWaterForce { get; set; } = 15f;
            
            [JsonProperty("AirborneBoostScale")]
            public float AirborneBoostScale { get; set; } = 0.3f;
            
            [JsonProperty("AirborneExtraDrag")]
            public float AirborneExtraDrag { get; set; } = 0.5f;
            
            [JsonProperty("MaxPitchDegrees")]
            public float MaxPitchDegrees { get; set; } = 25f;
            
            [JsonProperty("MaxRollDegrees")]
            public float MaxRollDegrees { get; set; } = 30f;
            
            [JsonProperty("MaxVerticalVelocity")]
            public float MaxVerticalVelocity { get; set; } = 10f;
            
            [JsonProperty("CenterOfMassOffset")]
            public float CenterOfMassOffset { get; set; } = -0.5f;
        }
        
        private class LobbyConfig
        {
            [JsonProperty("Position")]
            public Vector3Data Position { get; set; } = new Vector3Data();
            
            [JsonProperty("Radius")]
            public float Radius { get; set; } = 50f;
        }
        
        private class RaceConfig
        {
            [JsonProperty("VotingDuration")]
            public int VotingDuration { get; set; } = 30;
            
            [JsonProperty("CountdownDuration")]
            public int CountdownDuration { get; set; } = 5;
        }
        
        private class Vector3Data
        {
            [JsonProperty("x")]
            public float X { get; set; } = 0f;
            
            [JsonProperty("y")]
            public float Y { get; set; } = 0f;
            
            [JsonProperty("z")]
            public float Z { get; set; } = 0f;
            
            public Vector3 ToVector3() => new Vector3(X, Y, Z);
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
        
        private class PlayerData
        {
            public ulong UserId { get; set; }
            public string TrackName { get; set; }
            public int Lap { get; set; }
            public int TotalLaps { get; set; }
            public int Checkpoint { get; set; }
            public int TotalCheckpoints { get; set; }
            public float Progress { get; set; }
            public float Speed { get; set; }
            public float Boost { get; set; }
            public int Position { get; set; }
            public bool IsInRace { get; set; }
            public bool Finished { get; set; }
            public float FinishTime { get; set; }
            public bool IsInLobby { get; set; }
            public bool IsInQueue { get; set; }
            public string EditorContext { get; set; }
        }
        
        private class BoatPhysicsData
        {
            public Rigidbody Rigidbody { get; set; }
            public bool IsGrounded { get; set; }
            public float LastGroundCheck { get; set; }
        }
        
        private class TrackData
        {
            public string Name { get; set; }
            public string Author { get; set; }
            public int Checkpoints { get; set; }
            public int Laps { get; set; }
        }
        
        private class RaceState
        {
            public bool IsActive { get; set; }
            public bool IsVoting { get; set; }
            public bool IsCountdown { get; set; }
            public bool IsRunning { get; set; }
            public string RaceMode { get; set; } = "normal";
            public int CountdownRemaining { get; set; }
            public string TrackName { get; set; }
            public int TotalRacers { get; set; }
            public float StartTime { get; set; }
        }
        
        private class VotingState
        {
            public bool IsVoting { get; set; }
            public float VoteEndTime { get; set; }
            public int NormalVotes { get; set; }
            public int BattleVotes { get; set; }
            public Dictionary<ulong, string> PlayerVotes { get; set; } = new Dictionary<ulong, string>();
            public int TotalParticipants { get; set; }
        }
        
        #endregion
        
        #region Oxide Hooks
        
        private void Init()
        {
            // Initialize sample tracks
            tracks.Add(new TrackData { Name = "Harbor Circuit", Author = "Admin", Checkpoints = 8, Laps = 3 });
            tracks.Add(new TrackData { Name = "Island Loop", Author = "Admin", Checkpoints = 10, Laps = 2 });
            tracks.Add(new TrackData { Name = "Coastal Sprint", Author = "Admin", Checkpoints = 6, Laps = 5 });
        }
        
        private void OnServerInitialized()
        {
            timer.Every(1f, () => UpdateRaceTimers());
            timer.Every(0.1f, () => UpdatePhysics());
        }
        
        private void Unload()
        {
            // Clean up
            playerData.Clear();
            boatPhysics.Clear();
        }
        
        private void OnEntitySpawned(BaseNetworkable entity)
        {
            if (entity == null) return;
            
            var boat = entity as MotorRowboat;
            if (boat == null) return;
            
            NextTick(() =>
            {
                if (boat == null || boat.IsDestroyed) return;
                InitializeBoatPhysics(boat);
            });
        }
        
        private void OnEntityKill(BaseNetworkable entity)
        {
            if (entity == null) return;
            
            var boat = entity as MotorRowboat;
            if (boat == null) return;
            
            var player = boat.GetDriver();
            if (player != null)
            {
                boatPhysics.Remove(player.userID);
            }
        }
        
        #endregion
        
        #region Physics System
        
        private void InitializeBoatPhysics(MotorRowboat boat)
        {
            if (boat == null) return;
            
            var rb = boat.GetComponent<Rigidbody>();
            if (rb == null) return;
            
            // Lower center of mass for stability
            rb.centerOfMass += new Vector3(0, config.Controls.CenterOfMassOffset, 0);
            
            var player = boat.GetDriver();
            if (player != null)
            {
                boatPhysics[player.userID] = new BoatPhysicsData
                {
                    Rigidbody = rb,
                    IsGrounded = true,
                    LastGroundCheck = Time.time
                };
            }
        }
        
        private void UpdatePhysics()
        {
            foreach (var kvp in boatPhysics.ToList())
            {
                var player = BasePlayer.FindByID(kvp.Key);
                if (player == null || !player.IsConnected)
                {
                    boatPhysics.Remove(kvp.Key);
                    continue;
                }
                
                var boat = player.GetMountedVehicle() as MotorRowboat;
                if (boat == null || boat.IsDestroyed)
                {
                    boatPhysics.Remove(kvp.Key);
                    continue;
                }
                
                var physicsData = kvp.Value;
                var rb = physicsData.Rigidbody;
                if (rb == null) continue;
                
                // Check if grounded (on water)
                bool isGrounded = CheckIfGrounded(boat, rb);
                physicsData.IsGrounded = isGrounded;
                physicsData.LastGroundCheck = Time.time;
                
                // Apply stabilization
                ApplyStabilization(rb, isGrounded);
                
                // Apply additional forces
                if (isGrounded)
                {
                    ApplyStickToWaterForce(rb);
                }
                else
                {
                    ApplyAirborneForces(rb);
                }
                
                // Update player speed data
                if (playerData.ContainsKey(kvp.Key))
                {
                    playerData[kvp.Key].Speed = rb.velocity.magnitude;
                }
            }
        }
        
        private bool CheckIfGrounded(MotorRowboat boat, Rigidbody rb)
        {
            // Raycast downward to detect water
            RaycastHit hit;
            Vector3 origin = boat.transform.position;
            
            if (Physics.Raycast(origin, Vector3.down, out hit, 2f, LayerMask.GetMask("Water")))
            {
                return true;
            }
            
            // Also check if close to water level
            var waterLevel = WaterSystem.GetHeight(boat.transform.position);
            return boat.transform.position.y <= waterLevel + 0.5f;
        }
        
        private void ApplyStabilization(Rigidbody rb, bool isGrounded)
        {
            float stabilizeStrength = isGrounded ? 
                config.Controls.GroundedStabilizeStrength : 
                config.Controls.AirborneStabilizeStrength;
            
            // Get local rotation
            Transform transform = rb.transform;
            Vector3 localEuler = transform.localEulerAngles;
            
            // Normalize angles to -180 to 180
            float pitch = NormalizeAngle(localEuler.x);
            float roll = NormalizeAngle(localEuler.z);
            
            // Clamp pitch and roll
            pitch = Mathf.Clamp(pitch, -config.Controls.MaxPitchDegrees, config.Controls.MaxPitchDegrees);
            roll = Mathf.Clamp(roll, -config.Controls.MaxRollDegrees, config.Controls.MaxRollDegrees);
            
            // Calculate corrective torques in local space
            Vector3 targetAngularVelocity = Vector3.zero;
            targetAngularVelocity.x = -pitch * stabilizeStrength * Time.fixedDeltaTime;
            targetAngularVelocity.z = -roll * stabilizeStrength * Time.fixedDeltaTime;
            
            // Apply damping to angular velocity
            Vector3 localAngularVel = transform.InverseTransformDirection(rb.angularVelocity);
            localAngularVel.x *= config.Controls.PitchDamping;
            localAngularVel.z *= config.Controls.RollDamping;
            rb.angularVelocity = transform.TransformDirection(localAngularVel);
            
            // Apply corrective torque
            Vector3 torque = transform.TransformDirection(targetAngularVelocity);
            rb.AddTorque(torque, ForceMode.VelocityChange);
        }
        
        private void ApplyStickToWaterForce(Rigidbody rb)
        {
            // Apply downward force to keep boat on water
            if (rb.velocity.y > 0)
            {
                rb.AddForce(Vector3.down * config.Controls.StickToWaterForce, ForceMode.Acceleration);
            }
        }
        
        private void ApplyAirborneForces(Rigidbody rb)
        {
            // Extra drag while airborne
            rb.drag = 0.5f + config.Controls.AirborneExtraDrag;
            
            // Extra gravity
            rb.AddForce(Vector3.down * 9.81f * 0.5f, ForceMode.Acceleration);
            
            // Clamp vertical velocity
            Vector3 vel = rb.velocity;
            vel.y = Mathf.Clamp(vel.y, -config.Controls.MaxVerticalVelocity, config.Controls.MaxVerticalVelocity);
            rb.velocity = vel;
            
            // Reduce boost effectiveness (would need to hook into boost system)
        }
        
        private float NormalizeAngle(float angle)
        {
            while (angle > 180f) angle -= 360f;
            while (angle < -180f) angle += 360f;
            return angle;
        }
        
        #endregion
        
        #region Race System
        
        private void UpdateRaceTimers()
        {
            // Update voting countdown
            if (votingState.IsVoting)
            {
                float timeRemaining = votingState.VoteEndTime - Time.time;
                if (timeRemaining <= 0)
                {
                    EndVoting();
                }
            }
            
            // Update race countdown
            if (currentRace.IsCountdown)
            {
                int previousCount = currentRace.CountdownRemaining;
                float elapsed = Time.time - currentRace.StartTime;
                currentRace.CountdownRemaining = config.Race.CountdownDuration - (int)elapsed;
                
                if (currentRace.CountdownRemaining <= 0)
                {
                    StartRace();
                }
            }
        }
        
        private void StartVoting()
        {
            votingState.IsVoting = true;
            votingState.VoteEndTime = Time.time + config.Race.VotingDuration;
            votingState.NormalVotes = 0;
            votingState.BattleVotes = 0;
            votingState.PlayerVotes.Clear();
            votingState.TotalParticipants = GetRacingPlayers().Count();
            
            currentRace.IsVoting = true;
            currentRace.IsCountdown = false;
            currentRace.IsRunning = false;
        }
        
        private void EndVoting()
        {
            votingState.IsVoting = false;
            currentRace.IsVoting = false;
            
            // Determine winner
            currentRace.RaceMode = votingState.BattleVotes > votingState.NormalVotes ? "battle" : "normal";
            
            // Start countdown
            StartCountdown();
        }
        
        private void StartCountdown()
        {
            currentRace.IsCountdown = true;
            currentRace.CountdownRemaining = config.Race.CountdownDuration;
            currentRace.StartTime = Time.time;
        }
        
        private void StartRace()
        {
            currentRace.IsCountdown = false;
            currentRace.IsRunning = true;
            currentRace.IsActive = true;
            currentRace.StartTime = Time.time;
        }
        
        private IEnumerable<BasePlayer> GetRacingPlayers()
        {
            return playerData.Where(kvp => kvp.Value.IsInRace || kvp.Value.IsInQueue)
                .Select(kvp => BasePlayer.FindByID(kvp.Key))
                .Where(p => p != null && p.IsConnected);
        }
        
        #endregion
        
        #region UI Endpoints
        
        private object UI_GetHudData(BasePlayer player)
        {
            if (player == null) return null;
            
            PlayerData data;
            if (!playerData.TryGetValue(player.userID, out data))
            {
                data = new PlayerData
                {
                    UserId = player.userID,
                    TrackName = "",
                    Lap = 0,
                    TotalLaps = 0,
                    Checkpoint = 0,
                    TotalCheckpoints = 0,
                    Progress = 0f,
                    Speed = 0f,
                    Boost = 0f,
                    Position = 0,
                    IsInRace = false,
                    Finished = false,
                    FinishTime = 0f
                };
                playerData[player.userID] = data;
            }
            
            return new Dictionary<string, object>
            {
                ["TrackName"] = currentRace.TrackName ?? data.TrackName,
                ["Lap"] = data.Lap,
                ["TotalLaps"] = data.TotalLaps,
                ["Checkpoint"] = data.Checkpoint,
                ["TotalCheckpoints"] = data.TotalCheckpoints,
                ["Progress01"] = data.Progress,
                ["Speed"] = data.Speed,
                ["Boost01"] = data.Boost,
                ["Position"] = data.Position,
                ["Racers"] = currentRace.TotalRacers,
                ["IsRace"] = data.IsInRace,
                ["Finished"] = data.Finished,
                ["FinishTime"] = data.FinishTime,
                ["RaceMode"] = currentRace.RaceMode,
                ["Voting"] = currentRace.IsVoting,
                ["VoteSecondsRemaining"] = votingState.IsVoting ? (int)(votingState.VoteEndTime - Time.time) : 0,
                ["CountdownSeconds"] = currentRace.IsCountdown ? currentRace.CountdownRemaining : 0
            };
        }
        
        private object UI_GetVotingState(BasePlayer player)
        {
            if (player == null) return null;
            
            string myVote = "";
            if (votingState.PlayerVotes.ContainsKey(player.userID))
            {
                myVote = votingState.PlayerVotes[player.userID];
            }
            
            return new Dictionary<string, object>
            {
                ["Voting"] = votingState.IsVoting,
                ["SecondsRemaining"] = votingState.IsVoting ? (int)(votingState.VoteEndTime - Time.time) : 0,
                ["NormalVotes"] = votingState.NormalVotes,
                ["BattleVotes"] = votingState.BattleVotes,
                ["TotalParticipants"] = votingState.TotalParticipants,
                ["MyVote"] = myVote
            };
        }
        
        private object UI_GetCounts()
        {
            int online = BasePlayer.activePlayerList.Count;
            int lobby = GetLobbyCount();
            int queue = playerData.Count(kvp => kvp.Value.IsInQueue);
            int inRace = playerData.Count(kvp => kvp.Value.IsInRace);
            
            return new Dictionary<string, object>
            {
                ["Online"] = online,
                ["Lobby"] = lobby,
                ["Queue"] = queue,
                ["InRace"] = inRace
            };
        }
        
        private int GetLobbyCount()
        {
            // Try HydroLobby plugin first
            if (HydroLobby != null)
            {
                var count = HydroLobby.Call("GetLobbyPlayerCount");
                if (count is int) return (int)count;
            }
            
            // Fallback: count players within lobby radius
            Vector3 lobbyPos = config.Lobby.Position.ToVector3();
            float radius = config.Lobby.Radius;
            
            return BasePlayer.activePlayerList.Count(p => 
                Vector3.Distance(p.transform.position, lobbyPos) <= radius);
        }
        
        private object UI_ListTrackNames()
        {
            return tracks.Select(t => t.Name).ToList();
        }
        
        private object UI_GetEditorContext(BasePlayer player)
        {
            if (player == null) return null;
            
            PlayerData data;
            if (!playerData.TryGetValue(player.userID, out data))
            {
                return null;
            }
            
            return new Dictionary<string, object>
            {
                ["Context"] = data.EditorContext ?? "summary",
                ["Tracks"] = tracks.Select(t => new Dictionary<string, object>
                {
                    ["Name"] = t.Name,
                    ["Author"] = t.Author,
                    ["Checkpoints"] = t.Checkpoints,
                    ["Laps"] = t.Laps
                }).ToList()
            };
        }
        
        #endregion
        
        #region Chat Commands
        
        [ChatCommand("hydro")]
        private void HydroCommand(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;
            
            if (args.Length == 0)
            {
                SendReply(player, "HydroRust v5.2.0 - Use /hydro help for commands");
                return;
            }
            
            switch (args[0].ToLower())
            {
                case "vote":
                    if (args.Length < 2)
                    {
                        SendReply(player, "Usage: /hydro vote <normal|battle>");
                        return;
                    }
                    HandleVote(player, args[1].ToLower());
                    break;
                    
                case "join":
                    HandleJoin(player);
                    break;
                    
                case "leave":
                    HandleLeave(player);
                    break;
                    
                case "startvote":
                    if (!player.IsAdmin)
                    {
                        SendReply(player, "Admin only command");
                        return;
                    }
                    StartVoting();
                    SendReply(player, "Voting started!");
                    break;
                    
                case "help":
                    SendReply(player, "Commands: vote, join, leave, startvote (admin)");
                    break;
                    
                default:
                    SendReply(player, "Unknown command. Use /hydro help");
                    break;
            }
        }
        
        private void HandleVote(BasePlayer player, string voteType)
        {
            if (!votingState.IsVoting)
            {
                SendReply(player, "No active vote");
                return;
            }
            
            // Remove previous vote if exists
            if (votingState.PlayerVotes.ContainsKey(player.userID))
            {
                string prevVote = votingState.PlayerVotes[player.userID];
                if (prevVote == "normal") votingState.NormalVotes--;
                else if (prevVote == "battle") votingState.BattleVotes--;
            }
            
            // Add new vote
            if (voteType == "normal")
            {
                votingState.NormalVotes++;
                votingState.PlayerVotes[player.userID] = "normal";
                SendReply(player, "Voted for Normal race");
            }
            else if (voteType == "battle")
            {
                votingState.BattleVotes++;
                votingState.PlayerVotes[player.userID] = "battle";
                SendReply(player, "Voted for Battle race");
            }
            else
            {
                SendReply(player, "Invalid vote type. Use: normal or battle");
            }
        }
        
        private void HandleJoin(BasePlayer player)
        {
            if (!playerData.ContainsKey(player.userID))
            {
                playerData[player.userID] = new PlayerData { UserId = player.userID };
            }
            
            playerData[player.userID].IsInQueue = true;
            SendReply(player, "Joined queue");
        }
        
        private void HandleLeave(BasePlayer player)
        {
            if (playerData.ContainsKey(player.userID))
            {
                playerData[player.userID].IsInQueue = false;
                playerData[player.userID].IsInRace = false;
            }
            SendReply(player, "Left queue/race");
        }
        
        #endregion
    }
}
