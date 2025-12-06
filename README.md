<div align="center">

# BlockPasses (SwiftlyS2)

[![GitHub Release](https://img.shields.io/github/v/release/agasking1337/BlockPasses?color=FFFFFF&style=flat-square)](https://github.com/agasking1337/BlockPasses/releases/latest)
[![GitHub Issues](https://img.shields.io/github/issues/agasking1337/BlockPasses?color=FF0000&style=flat-square)](https://github.com/agasking1337/BlockPasses/issues)
[![GitHub Downloads](https://img.shields.io/github/downloads/agasking1337/BlockPasses/total?color=blue&style=flat-square)](https://github.com/agasking1337/BlockPasses/releases)
[![GitHub Stars](https://img.shields.io/github/stars/agasking1337/BlockPasses?style=social)](https://github.com/agasking1337/BlockPasses/stargazers)<br/>
  <sub>Original CounterStrikeSharp plugin by <a href="https://github.com/partiusfabaa/cs2-BlockerPasses" rel="noopener noreferrer" target="_blank">partiusfabaa</a></sub>
  <br/>
</div>

## Overview

**BlockPasses** blocks selected map passages with solid props until your player-count rule is met.  
It precaches required models during map load—**no external resource precacher is needed**.

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

## Features
- Solid blocking props (players cannot walk through).
- Built-in precache registration via `OnPrecacheResource`; no external precacher required.
- Per-map configurable entities (model, origin, angles, optional scale placeholder).
- Simple reload command.

## Plugin Setup
1. Download the latest release and place contents into:
   ```
   <server>/game/csgo/addons/swiftlys2/plugins/BlockPasses/
   ```
2. Start the server once to generate the config, or place your own at:
   ```
   <server>/game/csgo/addons/swiftlys2/configs/plugins/BlockPasses/config.json
   ```
3. Change map or restart to ensure precache runs on load.

## Configuration Guide
- File: `addons/swiftlys2/configs/plugins/BlockPasses/config.json`
- Fields:
  - `Players` — minimum player count before passages unblock.
  - `Message` — chat message shown when blocks are active.
  - `Maps` — per-map list of entities:
    - `ModelPath` (vmdl)
    - `Origin` (x y z)
    - `Angles` (pitch yaw roll)
    - `Scale` (optional placeholder)

## Commands
- `bp_reload` (permission: `blockpasses.reload`)  
  Reloads config; new models take effect on next map change.

## Building
```bash
dotnet restore
dotnet publish BlockPasses.csproj -c Release -o build/publish
```

## Notes
- Models are registered into the manifest during `OnPrecacheResource` and also precached to filesystem on load.
