# ZoneForge Client

Unity game client for ZoneForge — a 3D multiplayer RPG. Players explore zones built in the standalone ZoneForge Editor. Connects to a SpacetimeDB backend for real-time persistent world state.

## Stack

- **Engine**: Unity 2022.3 LTS (3D URP)
- **Language**: C#
- **Backend SDK**: SpacetimeDB C# SDK

## Project Structure

```
Assets/
├── Art/
│   └── Materials/        # Material Maker graphs, exports, and presets
├── Scripts/
│   ├── autogen/          # Generated SpacetimeDB bindings (do not edit)
│   ├── Data/             # ScriptableObject definitions (WorldData, ZoneVisualData)
│   ├── Network/          # SpacetimeDBManager — connect/subscribe/tick
│   └── Zone/             # ZoneController and runtime zone logic (3D)
├── Scenes/               # Unity scenes
└── Settings/             # URP renderer and quality settings
```

## Prerequisites

- Unity 2022.3 LTS (installed via Unity Hub)
- SpacetimeDB CLI — see [Getting Started](https://github.com/bjsmithxyz/zoneforge/blob/main/docs/guides/Getting_Started.md)
- A running SpacetimeDB server (local or cloud)

## Getting Started

1. Open the project in Unity Hub
2. Ensure the SpacetimeDB server module is published (see [zoneforge-server](https://github.com/bjsmithxyz/zoneforge-server))
3. Generate client bindings (run from this directory):

```bash
spacetime generate --lang csharp \
  --out-dir Assets/Scripts/autogen \
  --bin-path ../server/spacetimedb/target/wasm32-unknown-unknown/release/zoneforge_server.wasm
```

4. Press Play in Unity — the client connects to `http://localhost:3000` by default

## Related

- [zoneforge-server](https://github.com/bjsmithxyz/zoneforge-server) — SpacetimeDB Rust backend
- [zoneforge-editor](https://github.com/bjsmithxyz/zoneforge-editor) — Standalone world editor
- [zoneforge](https://github.com/bjsmithxyz/zoneforge) — Umbrella repo and documentation
