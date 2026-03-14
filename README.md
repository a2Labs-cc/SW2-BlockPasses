<div align="center">

# [SwiftlyS2] BlockPasses

<a href="https://github.com/agasking1337/BlockPasses/releases/latest">
  <img src="https://img.shields.io/github/v/release/agasking1337/BlockPasses?label=release&color=07f223&style=for-the-badge">
</a>
<a href="https://github.com/agasking1337/BlockPasses/issues">
  <img src="https://img.shields.io/github/issues/agasking1337/BlockPasses?label=issues&color=E63946&style=for-the-badge">
</a>
<a href="https://github.com/agasking1337/BlockPasses/releases">
  <img src="https://img.shields.io/github/downloads/agasking1337/BlockPasses/total?label=downloads&color=3A86FF&style=for-the-badge">
</a>
<a href="https://github.com/agasking1337/BlockPasses/stargazers">
  <img src="https://img.shields.io/github/stars/agasking1337/BlockPasses?label=stars&color=e3d322&style=for-the-badge">
</a>

<br/>
<sub>Made by <a href="https://github.com/agasking1337" target="_blank" rel="noopener noreferrer">aga</a></sub>
<br/>
<sub>Original CounterStrikeSharp plugin by <a href="https://github.com/partiusfabaa/cs2-BlockerPasses" rel="noopener noreferrer" target="_blank">partiusfabaa</a></sub>

</div>

## Overview

**BlockPasses** blocks selected map passages with solid props until your player-count rule is met.  
It precaches required models during map load—**no external resource precacher is needed**.

## Support

Need help or have questions? Join our Discord server:

<p align="center">
  <a href="https://discord.gg/d853jMW2gh" target="_blank">
    <img src="https://img.shields.io/badge/Join%20Discord-5865F2?logo=discord&logoColor=white&style=for-the-badge" alt="Discord">
  </a>
</p>

## Download Shortcuts
<ul>
  <li>
    <code>📦</code>
    <strong>&nbsp;Download Latest Plugin Version</strong> ⇢
    <a href="https://github.com/agasking1337/BlockPasses/releases/latest" target="_blank" rel="noopener noreferrer">Click Here</a>
  </li>
  <li>
    <code>⚙️</code>
    <strong>&nbsp;Download Latest SwiftlyS2 Version</strong> ⇢
    <a href="https://github.com/swiftly-solution/swiftlys2/releases/latest" target="_blank" rel="noopener noreferrer">Click Here</a>
  </li>
</ul>

## Installation

1. Download/build the plugin (publish output lands in `build/publish/BlockPasses/`).
2. Copy the published plugin folder to your server:

```
.../game/csgo/addons/swiftlys2/plugins/BlockPasses/
```
3. Ensure the `resources/` folder (translations, gamedata) is alongside the DLL.
4. Start/restart the server.

## Configuration

The plugin uses SwiftlyS2's JSON config system.

- **File name**: `config.json` 
- **Location**: `addons/swiftlys2/configs/plugins/BlockPasses/`

On first run the config is created automatically.

### Key Configuration Options

- `Players`: Minimum player count before passages unblock (default: 6)
- `SpawnBlocksOnWarmup`: Whether blocks spawn during warmup period (default: false)
- `Debug`: Enables detailed logging for troubleshooting (default: false)
- `ChatPrefix`: Prefix for chat messages (default: "BlockPasses |")
- `ChatPrefixColor`: Color for chat prefix (default: "{GREEN}")
- `ModelPresets`: List of model paths used as presets by the editor

### Default Model Presets
- Dust Rollupdoor (`models/props/de_dust/hr_dust/dust_windows/dust_rollupdoor_96x128_surface_lod.vmdl`)
- Mirage Small Door (`models/props/de_mirage/small_door_b.vmdl`)
- Mirage Large Door (`models/props/de_mirage/large_door_c.vmdl`)
- Nuke Fence (`models/props/de_nuke/hr_nuke/chainlink_fence_001/chainlink_fence_001_256.vmdl`)

### Per-map Block Storage
Block placements are saved per-map in Swiftly's data folder:
`addons/swiftlys2/data/BlockPasses/<map>.json`

This file contains the placed blocks for that map (`ModelPath`, `Origin`, `Angles`, `Scale`, `Color`, etc.).

## Commands

| Command | Permission | Description |
|---------|------------|-------------|
| `bp_reload` | `blockpasses.reload` | Reloads config; new models take effect on next map change |
| `bp_menu` | `blockpasses.admin` | Opens interactive menu for block management |
| `bp_edit [0/off/false]` | `blockpasses.edit` | Enables/disables edit mode with automatic warmup management |
| `bp_add [modelpath]` | `blockpasses.admin` | Adds a new block at aim position; optional model path override |
| `bp_remove` | `blockpasses.admin` | Removes the block you are aiming at |
| `bp_up [step]` | `blockpasses.admin` | Moves aimed block up by step units (default: 5) |
| `bp_down [step]` | `blockpasses.admin` | Moves aimed block down by step units (default: 5) |
| `bp_rot [degrees]` | `blockpasses.admin` | Rotates aimed block by degrees (default: 15) |
| `bp_scale [delta]` | `blockpasses.admin` | Changes aimed block scale by delta (default: 0.1) |
| `bp_spawnall` | `blockpasses.admin` | Respawns all saved blocks for the current map (edit mode only) |
| `bp_save` | `blockpasses.admin` | Saves current edited blocks for the map |

### Editor Controls
When in edit mode:
- Press `E` to grab/drop a block
- Press `R` or `F` to rotate a block
- Use menu or commands for precise adjustments

## Building

```bash
dotnet build
```

## Credits
- Readme template by [criskkky](https://github.com/criskkky)
- Release workflow based on [K4ryuu/K4-Guilds-SwiftlyS2 release workflow](https://github.com/K4ryuu/K4-Guilds-SwiftlyS2/blob/main/.github/workflows/release.yml)
- Original CounterStrikeSharp plugin by [partiusfabaa](https://github.com/partiusfabaa/cs2-BlockerPasses)
