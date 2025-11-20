# HydroRust Boat Racing Plugin

Complete boat racing system for Rust game servers using Oxide/uMod framework.

## Plugins

### HydroRust v5.2.0
Core racing plugin with physics stabilization and UI integration.

**Features:**
- Race flow management (Voting → Countdown → Running)
- Physics stabilization for boats to prevent flying and excessive roll
- Vote tracking system for race modes (Normal/Battle)
- UI endpoints for HydroUI integration
- Configurable physics parameters

**Physics Stabilization:**
- Stronger pitch/roll stabilizer torques (grounded vs airborne)
- Downward stick-to-water force when grounded
- Reduced boost effectiveness while airborne
- Extra drag and downward force in air
- Clamped pitch/roll and vertical velocity
- Lower center of mass on spawn for stability

**Configuration:**
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
    "CenterOfMassOffset": -0.5
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

**Chat Commands:**
- `/hydro vote <normal|battle>` - Vote for race mode
- `/hydro join` - Join race queue
- `/hydro leave` - Leave race queue
- `/hydro startvote` - Start voting (admin only)

### HydroUI v2.7.0
Complete UI system for HydroRust with rich visual interface.

**Features:**
- Welcome screen with lobby join requirement
- HUD with progress/boost bars, speed, lap counter, compass
- Player counter panel (Online | Lobby | Queue | Race) - updates every second
- Center voting modal with live vote counts and countdown
- Large center countdown display for race start
- Track editor side panel with pagination
- Player quick panel for Join/Leave/Stats/Vote
- Menu toggle system
- Persistent player preferences (scale, compact HUD, theme)

**UI Components:**
1. **HUD** - Always visible, shows:
   - Track name and race mode tag ([Race]/[Battle]/[Voting])
   - Speed indicator
   - Lap/Checkpoint info
   - Position in race
   - Progress bar
   - Boost bar
   - Compass direction

2. **Player Counter** - Top right panel:
   - Online players count
   - Lobby players count (via HydroLobby or proximity fallback)
   - Queue players count
   - Racing players count
   - Updates every 1 second

3. **Voting Modal** - Center overlay during voting:
   - Countdown timer
   - Live vote counts (Normal vs Battle)
   - Clickable vote buttons
   - Shows your current vote

4. **Countdown Display** - Large center number:
   - Shows seconds remaining until race start
   - Changes color as countdown nears zero
   - Shows "GO!" at race start

5. **Track Editor** - Left side panel:
   - Summary tab with track statistics
   - Tracks tab with paginated list
   - Inspector tab for track details
   - Supports track management actions

**Chat Commands:**
- `/hydroui show` - Show UI
- `/hydroui hide` - Hide UI
- `/hydroui editor` - Open track editor
- `/hydroui scale <0.5-2.0>` - Set UI scale

**Console Commands:**
- `hydroui.vote.normal` - Vote for normal race
- `hydroui.vote.battle` - Vote for battle race
- `hydroui.join` - Join queue
- `hydroui.leave` - Leave queue
- `hydroui.togglemenu` - Toggle menu
- `hydroui.openeditor` - Open track editor
- `hydroui.closeeditor` - Close track editor

## Installation

1. Copy `HydroRust.cs` to `oxide/plugins/`
2. Copy `HydroUI.cs` to `oxide/plugins/`
3. Reload plugins: `oxide.reload HydroRust` and `oxide.reload HydroUI`

## Dependencies

- **Required:** Oxide/uMod for Rust
- **Optional:** HydroLobby plugin (for accurate lobby player count)
- **Optional:** ImageLibrary plugin (for enhanced visuals)

## Configuration

Both plugins will generate configuration files on first load:
- `oxide/config/HydroRust.json`
- `oxide/config/HydroUI.json`

Edit these files to customize physics parameters, UI colors, and chat command templates.

## Usage

1. Players join the server and see the welcome screen
2. Use `/hydro join` to enter the race queue
3. Admin starts voting with `/hydro startvote`
4. Players see voting modal and click to vote
5. After voting, countdown appears in center of screen
6. Race starts and HUD shows progress
7. Player counter updates every second with accurate counts

## Physics Behavior

Boats now:
- Stop violently pitching up on bumps
- No longer fly when boosting off wave crests
- Have reduced left/right roll on sharp turns
- Can still get airtime briefly but won't glide/fly
- Boosting while airborne gives limited extra speed
- Quickly settle back to water surface

## Testing

The implementation includes:
- Accurate player counts updating every second
- Center voting modal with live countdown and vote counts
- Large center countdown during race start
- HUD showing track title, race mode, checkpoints, laps, position
- Physics preventing boats from flying or excessive rolling
- Smooth transitions between voting, countdown, and racing phases

## Version History

**HydroRust 5.2.0:**
- Added UI endpoints for HydroUI integration
- Implemented physics stabilization system
- Added voting and countdown race flow
- Added configurable physics parameters

**HydroUI 2.7.0:**
- Complete UI restoration with all features
- Player counter panel with 1-second refresh
- Center voting modal with live counts
- Large center countdown display
- Track editor with pagination
- Persistent player preferences
- Full HUD with progress/boost bars
