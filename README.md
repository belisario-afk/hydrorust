# HydroRust Plugin Suite

Complete hydrofoil racing system for Rust game servers with full UI and physics stabilization.

## Plugins

### HydroRust.cs v5.2.0
Core racing plugin with physics stabilization and UI endpoints.

### HydroUI.cs v2.7.0
Complete UI system for race management, voting, and player interaction.

## Features

### HydroRust Features
- **Physics Stabilization**: Prevents boats from flying/pitching up excessively
  - Separate grounded/airborne stabilization
  - Pitch/roll torque stabilizers with configurable damping
  - Stick-to-water force when grounded
  - Reduced boost effectiveness while airborne
  - Extra drag and gravity in air
  - Pitch/roll and vertical velocity clamping
  - Lower center of mass on boat spawn

- **Race Management**:
  - Voting system for race modes (normal/battle)
  - Countdown system with per-second tick
  - Track management with laps and checkpoints
  - Player statistics tracking

- **UI API Endpoints**:
  - `UI_GetHudData(BasePlayer)` - HUD information
  - `UI_GetVotingState(BasePlayer)` - Voting status
  - `UI_GetCounts()` - Player counts (Online/Lobby/Queue/Race)
  - `UI_ListTrackNames()` - Track list
  - `UI_GetEditorContext(BasePlayer)` - Editor context

- **Chat Commands**:
  - `/hydro join` - Join lobby
  - `/hydro leave` - Leave lobby
  - `/hydro vote <normal|battle>` - Vote for race mode
  - `/hydro stats` - View statistics
  - `/hydro startvote` - Start voting (admin only)

### HydroUI Features
- **Start Menu**: Welcome screen (locked until lobby join) + play menu
- **Full HUD**: Progress bars, speed, lap info, position, race mode indicators
- **Player Counter**: Top-right panel showing Online/Lobby/Queue/Race counts (updates every second)
- **Voting Modal**: Center modal with vote buttons, live counts, and countdown
- **Countdown Display**: Large center numeric countdown during race start
- **Track Editor**: Side panel with pagination and track management
- **Player Quick Panel**: Action buttons (Join/Leave/Stats)
- **Menu Toggle**: Button (☰) to open/close menus
- **Preferences**: Persistent player settings (scale, compact HUD, theme)

## Installation

1. Download `HydroRust.cs` and `HydroUI.cs`
2. Place both files in your server's `oxide/plugins/` directory
3. The plugins will auto-load and create default configuration files

## Configuration

### HydroRust Configuration

Located at `oxide/config/HydroRust.json`:

```json
{
  "Controls": {
    "GroundedStabilizeStrength": 50.0,
    "AirborneStabilizeStrength": 30.0,
    "RollDamping": 0.8,
    "PitchDamping": 0.8,
    "StickToWaterForce": 10.0,
    "AirborneBoostScale": 0.3,
    "AirborneExtraDrag": 2.0,
    "MaxPitchDegrees": 30.0,
    "MaxRollDegrees": 35.0,
    "MaxVerticalVelocity": 15.0,
    "ExtraGravityInAir": 5.0,
    "CenterOfMassOffset": -0.5
  },
  "Lobby": {
    "Position": { "x": 0.0, "y": 0.0, "z": 0.0 },
    "Radius": 50.0
  },
  "Race": {
    "VotingDurationSeconds": 30,
    "CountdownSeconds": 5,
    "MinPlayersForVoting": 2
  }
}
```

### HydroUI Configuration

Located at `oxide/config/HydroUI.json`:

```json
{
  "UI Settings": {
    "Default HUD Visible": true,
    "Default Scale": 1.0,
    "HUD Update Interval": 0.1,
    "Counter Update Interval": 1.0,
    "Editor Update Interval": 0.5,
    "Tracks Per Page": 10
  },
  "Chat Templates": {
    "Join Command": "/hydro join",
    "Leave Command": "/hydro leave",
    "Stats Command": "/hydro stats",
    "Vote Command": "/hydro vote",
    "Vote Normal Command": "/hydro vote normal",
    "Vote Battle Command": "/hydro vote battle"
  },
  "Colors": {
    "Primary": "0.2 0.6 0.9 0.95",
    "Secondary": "0.1 0.1 0.1 0.9",
    "Text": "1 1 1 1",
    "Success": "0.2 0.8 0.2 0.95",
    "Warning": "0.9 0.7 0.2 0.95",
    "Danger": "0.9 0.2 0.2 0.95",
    "Background": "0.05 0.05 0.05 0.85"
  }
}
```

## Physics Tuning Guide

### Stabilization Strength
- **GroundedStabilizeStrength**: Higher values make boats more stable on water (default: 50)
- **AirborneStabilizeStrength**: Lower than grounded for natural airtime feel (default: 30)

### Damping
- **RollDamping**: Reduces side-to-side rolling (default: 0.8)
- **PitchDamping**: Reduces front-to-back pitching (default: 0.8)

### Forces
- **StickToWaterForce**: Pushes boat down when on water (default: 10)
- **ExtraGravityInAir**: Additional downward force when airborne (default: 5)
- **AirborneExtraDrag**: Air resistance multiplier (default: 2)

### Limits
- **MaxPitchDegrees**: Maximum nose-up/down angle (default: 30)
- **MaxRollDegrees**: Maximum left/right roll angle (default: 35)
- **MaxVerticalVelocity**: Terminal velocity cap (default: 15)

### Boost
- **AirborneBoostScale**: Boost effectiveness while airborne (default: 0.3, 30% power)

### Center of Mass
- **CenterOfMassOffset**: Negative values lower center of mass for stability (default: -0.5)

## Permissions

- `hydrorust.use` - Basic plugin usage
- `hydrorust.admin` - Admin commands (startvote, track management)

## Dependencies

### Required
- Oxide/uMod for Rust
- Rust game server

### Optional
- **HydroLobby** - Provides accurate lobby counts (falls back to proximity detection)
- **ImageLibrary** - Enhanced UI graphics support

## Reload Commands

After making changes or updating plugins:

```
oxide.reload HydroRust
oxide.reload HydroUI
```

## Usage Flow

1. **Player Joins**: Sees welcome screen, clicks "Join Lobby"
2. **Welcome Unlocks**: Can now access play menu and track editor
3. **Admin Starts Vote**: Uses `/hydro startvote` or trigger via plugin
4. **Players Vote**: Center modal appears with Normal/Battle buttons
5. **Countdown Begins**: Large center number counts down (5...4...3...2...1...GO!)
6. **Race Starts**: HUD shows track info, position, lap progress
7. **Physics Active**: Boats stabilize automatically, reduced flying
8. **Race Ends**: Finish time displayed, statistics saved

## UI Components

### Always Visible
- **Menu Toggle Button** (☰): Bottom-left, opens quick panel
- **Player Counter**: Top-right, shows Online/Lobby/Queue/Race

### HUD (Default Visible)
- Track name with mode indicator [Race]/[Battle]/[Voting]
- Progress bar (checkpoint to checkpoint)
- Boost bar
- Speed display
- Lap counter (current/total)
- Checkpoint counter (current/total)
- Position (current/total racers)
- Finish time (when completed)

### Overlays (Context-Dependent)
- **Voting Modal**: Center, appears during voting phase
- **Countdown**: Center, large numbers during race start

### Menus (On Demand)
- **Start Menu**: Full-screen welcome or play menu
- **Track Editor**: Right side panel with track list and controls
- **Player Quick Panel**: Left side, quick action buttons

## Race Modes

### Normal Mode (IsRace=true)
- Standard racing with laps and checkpoints
- Position tracking
- Time trials

### Battle Mode (IsRace=false)
- Combat-oriented racing
- Alternative scoring system

## Troubleshooting

### Boats Still Flying
- Increase `GroundedStabilizeStrength`
- Increase `StickToWaterForce`
- Decrease `MaxPitchDegrees`
- Increase `ExtraGravityInAir`

### Too Much Stabilization (Feels Stiff)
- Decrease `GroundedStabilizeStrength`
- Increase damping values (closer to 1.0)
- Decrease `StickToWaterForce`

### Boost Too Weak in Air
- Increase `AirborneBoostScale` (0.3 = 30% power)

### UI Not Showing
- Ensure HydroUI is loaded: `oxide.plugins`
- Check console for errors: `oxide.show con`
- Verify HudVisible setting in player prefs

### Player Counter Not Updating
- Check that counter timer is running (every 1 second)
- Verify HydroRust plugin is loaded
- For accurate lobby counts, install HydroLobby plugin

### Voting Modal Not Appearing
- Ensure voting is started: `/hydro startvote`
- Check minimum players requirement (default: 2)
- Verify HydroRust.Call("UI_GetVotingState") returns data

## Data Files

- `oxide/data/HydroRust.json` - Tracks and player statistics
- `oxide/data/HydroUI_Prefs.json` - Player UI preferences

## Development Notes

### Update Timers
- **HUD**: 0.10s (10Hz) - Fast updates for smooth progress bars
- **Counter**: 1.0s (1Hz) - Efficient player count updates
- **Editor**: 0.5s (2Hz) - Responsive track list updates
- **Physics**: 0.1s (10Hz) - Smooth boat stabilization

### Performance Optimization
- UI only rebuilds when data changes (JSON comparison)
- Timers destroyed on player disconnect
- Physics only processes active boats with drivers

## Version History

### v5.2.0 (HydroRust) / v2.7.0 (HydroUI)
- Complete physics stabilization system
- Full UI restoration with all overlays
- Voting and countdown systems
- Player counter with real-time updates
- Track editor with pagination
- Player preferences persistence
- Lobby proximity fallback

## License

These plugins are designed for Rust game servers running Oxide/uMod.

## Support

For issues, suggestions, or contributions, please refer to the repository.

## Credits

- HydroRust Plugin Suite
- Designed for Oxide/uMod for Rust
