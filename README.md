# ZoneForge Client

Unity game client for ZoneForge — a tile-based multiplayer world builder. Connects to a SpacetimeDB backend for real-time persistent world state.

## Stack

- **Engine**: Unity 6 (URP)
- **Language**: C#
- **Backend SDK**: SpacetimeDB C# SDK
- **Render Pipeline**: Universal Render Pipeline (URP)

## Project Structure

```
Assets/
├── Art/
│   └── Materials/        # Material Maker graphs, exports, and presets
├── Scripts/
│   ├── autogen/          # Generated SpacetimeDB bindings (do not edit)
│   ├── Data/             # ScriptableObject definitions (WorldData, ZoneVisualData)
│   └── Editor/           # Unity Editor tools (MapEditorWindow)
├── Scenes/               # Unity scenes
└── Settings/             # URP renderer and quality settings
```

## Prerequisites

- Unity 6 (installed via Unity Hub)
- SpacetimeDB CLI — see [Setup Guide](https://github.com/bjsmithxyz/zoneforge/blob/main/Documentation/Setup%20Guide.md)
- A running SpacetimeDB server (local or cloud)

## Getting Started

1. Open the project in Unity Hub
2. Ensure the SpacetimeDB server module is published (see [zoneforge-server](https://github.com/bjsmithxyz/zoneforge-server))
3. Generate client bindings from the server project root:

```bash
spacetime generate --lang csharp \
  --out-dir Assets/Scripts/autogen \
  --bin-path ../server/spacetimedb/target/wasm32-unknown-unknown/release/zoneforge_server.wasm
```

4. Press Play in Unity — the client connects to `http://localhost:3000` by default

## Editor Tools

Open the map editor from the Unity menu bar:

```
ZoneForge → Map Editor
```

## Related

- [zoneforge-server](https://github.com/bjsmithxyz/zoneforge-server) — SpacetimeDB Rust backend
- [zoneforge](https://github.com/bjsmithxyz/zoneforge) — Umbrella repo and documentation
