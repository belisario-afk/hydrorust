# System Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                         RUST GAME CLIENT                         │
│                                                                   │
│  ┌────────────────────────────────────────────────────────────┐ │
│  │                        UI OVERLAY                           │ │
│  │                                                              │ │
│  │  ┌──────────┐    ┌─────────────┐    ┌──────────────────┐  │ │
│  │  │ Welcome  │    │  Menu       │    │ Player Counter   │  │ │
│  │  │ Screen   │    │  Toggle (☰) │    │ O|L|Q|R (1s)     │  │ │
│  │  └──────────┘    └─────────────┘    └──────────────────┘  │ │
│  │                                                              │ │
│  │  ┌──────────────────────────────────────────────────────┐  │ │
│  │  │              HUD (0.1s refresh)                       │  │ │
│  │  │  • Track: Harbor Circuit [Race]                      │  │ │
│  │  │  • Speed: 42.3  Lap: 2/3  CP: 5/8  Pos: 3/8         │  │ │
│  │  │  • Progress: [████████░░░░] 65%                      │  │ │
│  │  │  • Boost:    [███░░░░░░░░░] 25%                      │  │ │
│  │  │  • Compass: NE                                        │  │ │
│  │  └──────────────────────────────────────────────────────┘  │ │
│  │                                                              │ │
│  │  ┌──────────────────────────────────────────────────────┐  │ │
│  │  │     CENTER VOTING MODAL (during voting)              │  │ │
│  │  │     Vote for Race Mode                                │  │ │
│  │  │     Time remaining: 23s                               │  │ │
│  │  │     Normal: 5  |  Battle: 3                          │  │ │
│  │  │     [Normal Race]  [Battle Race]                      │  │ │
│  │  └──────────────────────────────────────────────────────┘  │ │
│  │                                                              │ │
│  │  ┌──────────────────────────────────────────────────────┐  │ │
│  │  │        CENTER COUNTDOWN (during countdown)            │  │ │
│  │  │                     3                                 │  │ │
│  │  └──────────────────────────────────────────────────────┘  │ │
│  │                                                              │ │
│  │  ┌────────────┐                        ┌──────────────┐    │ │
│  │  │ Track      │                        │ Quick Panel  │    │ │
│  │  │ Editor     │                        │  • Join      │    │ │
│  │  │  • Summary │                        │  • Leave     │    │ │
│  │  │  • Tracks  │                        │  • Stats     │    │ │
│  │  │  • Inspector│                       │  • Vote      │    │ │
│  │  └────────────┘                        └──────────────┘    │ │
│  └──────────────────────────────────────────────────────────┘ │
└───────────────────────────────────────────────────────────────┘
                              ▲
                              │ CUI (Oxide UI System)
                              │
┌─────────────────────────────┴─────────────────────────────────┐
│                      HydroUI Plugin v2.7.0                     │
│                                                                 │
│  ┌──────────────┐  ┌──────────────┐  ┌─────────────────────┐ │
│  │ UI Timers    │  │ UI Builders  │  │ Command Handlers    │ │
│  │              │  │              │  │                     │ │
│  │ • hudTick    │  │ • ShowHUD    │  │ • /hydroui show    │ │
│  │   (0.1s)     │  │ • ShowVoting │  │ • hydroui.vote.*   │ │
│  │ • counterTick│  │ • ShowCount  │  │ • hydroui.join     │ │
│  │   (1.0s)     │  │ • ShowEditor │  │ • hydroui.editor   │ │
│  │ • editorTick │  │ • ShowCounter│  │                     │ │
│  │   (0.5s)     │  │              │  │                     │ │
│  └──────────────┘  └──────────────┘  └─────────────────────┘ │
│                                                                 │
│  ┌──────────────────────────────────────────────────────────┐ │
│  │              Data Fetching (via Plugin Refs)              │ │
│  │  GetHudData() → GetVotingState() → GetPlayerCounts()     │ │
│  └──────────────────────────────────────────────────────────┘ │
└─────────────────────────────┬─────────────────────────────────┘
                              │ Plugin.Call()
                              ▼
┌───────────────────────────────────────────────────────────────┐
│                    HydroRust Plugin v5.2.0                     │
│                                                                 │
│  ┌────────────────────────────────────────────────────────┐   │
│  │                   UI Endpoints                          │   │
│  │  • UI_GetHudData(player) → track, lap, speed, etc.     │   │
│  │  • UI_GetVotingState(player) → votes, countdown        │   │
│  │  • UI_GetCounts() → online, lobby, queue, race         │   │
│  │  • UI_ListTrackNames() → track list                    │   │
│  │  • UI_GetEditorContext(player) → editor data           │   │
│  └────────────────────────────────────────────────────────┘   │
│                                                                 │
│  ┌────────────────────┐  ┌────────────────────────────────┐   │
│  │  Race Flow         │  │  Physics System (0.1s)         │   │
│  │                    │  │                                 │   │
│  │  Voting (30s)      │  │  • CheckIfGrounded()           │   │
│  │      ↓             │  │  • ApplyStabilization()        │   │
│  │  Countdown (5s)    │  │    - Pitch/Roll correction     │   │
│  │      ↓             │  │    - Angular velocity damping  │   │
│  │  Running           │  │  • ApplyStickToWaterForce()    │   │
│  │                    │  │  • ApplyAirborneForces()       │   │
│  │  • Vote tracking   │  │    - Extra drag (+0.5)         │   │
│  │  • Timer countdown │  │    - Extra gravity (×0.5)      │   │
│  │  • Mode selection  │  │    - Velocity clamping         │   │
│  └────────────────────┘  └────────────────────────────────┘   │
│                                                                 │
│  ┌────────────────────────────────────────────────────────┐   │
│  │              Configuration System                       │   │
│  │  • Controls: 17 physics parameters                      │   │
│  │  • Lobby: position, radius                              │   │
│  │  • Race: voting/countdown duration                      │   │
│  └────────────────────────────────────────────────────────┘   │
└─────────────────────────────┬─────────────────────────────────┘
                              │ Chat Commands
                              ▼
┌───────────────────────────────────────────────────────────────┐
│                        RUST GAME SERVER                        │
│                                                                 │
│  Player Actions:                                                │
│  • /hydro vote <normal|battle>                                 │
│  • /hydro join                                                 │
│  • /hydro leave                                                │
│                                                                 │
│  Physics Engine:                                                │
│  • Rigidbody updates                                           │
│  • Boat movement                                               │
│  • Collision detection                                         │
└───────────────────────────────────────────────────────────────┘
```

## Data Flow

### HUD Update Flow (every 0.1s):
```
1. Timer triggers → UpdateAllHuds()
2. For each player:
   a. Fetch data from HydroRust via UI_GetHudData()
   b. Compare JSON with last known state
   c. If changed, rebuild HUD UI elements
   d. Send CUI update to client
```

### Player Counter Flow (every 1.0s):
```
1. Timer triggers → UpdateAllCounters()
2. Call HydroRust.UI_GetCounts()
3. HydroRust queries:
   - Online: BasePlayer.activePlayerList.Count
   - Lobby: HydroLobby plugin OR proximity check
   - Queue: playerData.IsInQueue count
   - InRace: playerData.IsInRace count
4. Compare with last state
5. If changed, rebuild counter UI
```

### Voting Flow:
```
1. Admin: /hydro startvote
2. HydroRust: StartVoting() → set voting flags, start timer
3. HydroUI: UpdateAllHuds() detects voting=true
4. HydroUI: ShowVotingModal() creates center overlay
5. Player clicks button → hydroui.vote.normal/battle
6. Console command → RunChat("/hydro vote normal")
7. HydroRust: HandleVote() updates vote counts
8. HydroUI: Next update shows new counts
9. Timer expires → EndVoting() → StartCountdown()
```

### Countdown Flow:
```
1. HydroRust: StartCountdown() → set countdown=5
2. Every 1s: UpdateRaceTimers() → decrement countdown
3. HydroUI: ShowCountdown() displays large number
4. At 0: StartRace() → countdown=0
5. HydroUI: Hides countdown, shows race HUD
```

### Physics Update Flow (every 0.1s):
```
1. Timer triggers → UpdatePhysics()
2. For each boat with player:
   a. CheckIfGrounded() via raycast + water level
   b. ApplyStabilization():
      - Calculate pitch/roll angles
      - Clamp to max degrees
      - Apply corrective torques
      - Damp angular velocity
   c. If grounded: ApplyStickToWaterForce()
   d. If airborne: ApplyAirborneForces()
   e. Update player speed in data
```

## Configuration Files

### oxide/config/HydroRust.json
```json
{
  "Controls": {
    "GroundedStabilizeStrength": 50.0,
    "AirborneStabilizeStrength": 20.0,
    "RollDamping": 0.8,
    "PitchDamping": 0.8,
    "StickToWaterForce": 15.0,
    "AirborneBoostScale": 0.3,
    "AirborneExtraDrag": 0.5,
    "MaxPitchDegrees": 25.0,
    "MaxRollDegrees": 30.0,
    "MaxVerticalVelocity": 10.0,
    "CenterOfMassOffset": -0.5,
    "GroundCheckDistance": 2.0,
    "WaterProximityThreshold": 0.5,
    "BaseDrag": 0.5,
    "GravityMultiplier": 0.5
  },
  "Lobby": {
    "Position": { "x": 0, "y": 0, "z": 0 },
    "Radius": 50.0
  },
  "Race": {
    "VotingDuration": 30,
    "CountdownDuration": 5
  }
}
```

### oxide/config/HydroUI.json
```json
{
  "UI Settings": {
    "DefaultScale": 1.0,
    "HudRefreshRate": 0.1,
    "CounterRefreshRate": 1.0,
    "EditorRefreshRate": 0.5,
    "TracksPerPage": 5
  },
  "Chat Commands": {
    "JoinTemplate": "/hydro join",
    "LeaveTemplate": "/hydro leave",
    "VoteNormalTemplate": "/hydro vote normal",
    "VoteBattleTemplate": "/hydro vote battle"
  },
  "Colors": {
    "Primary": "0.2 0.4 0.6 0.95",
    "Secondary": "0.15 0.15 0.15 0.9",
    "Accent": "0.3 0.6 0.9 1",
    "Text": "1 1 1 1",
    "Success": "0.2 0.8 0.2 1",
    "Warning": "0.9 0.6 0.1 1",
    "Danger": "0.8 0.2 0.2 1"
  }
}
```

### oxide/data/HydroUI_Prefs.json
```json
{
  "76561198012345678": {
    "Scale": 1.0,
    "CompactHUD": false,
    "Theme": "default"
  }
}
```
