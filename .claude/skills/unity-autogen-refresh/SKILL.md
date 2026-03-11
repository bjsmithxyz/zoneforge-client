---
name: unity-autogen-refresh
description: Regenerate the SpacetimeDB C# client bindings for ZoneForge after a server schema change. Use this skill whenever the user mentions regenerating bindings, updating autogen, the schema changed, autogen types are out of date, "spacetime generate", or Unity showing errors after a server change. Also trigger proactively after any server table or reducer change — stale bindings are the most common source of Unity compile errors after server work.
---

## When to run this

Any time the server schema changes — tables added/removed/renamed, columns added/removed, reducers added/removed/renamed — the generated C# bindings in `Assets/Scripts/autogen/` become stale. Stale bindings cause Unity compile errors and missing types.

Run this after every `spacetime publish` that changed the schema.

## Prerequisites

- SpacetimeDB server is running (`spacetime start`)
- Server module is built (`cd server && spacetime build`)
- Server module is published (`spacetime publish --server local zoneforge-server`)

The `spacetime generate` command reads the compiled WASM binary, not the live server — so the binary must exist and be up to date.

## Command

Run from the **`client/`** directory:

```bash
cd client && spacetime generate \
  --lang csharp \
  --out-dir Assets/Scripts/autogen \
  --bin-path ../server/spacetimedb/target/wasm32-unknown-unknown/release/zoneforge_server.wasm
```

The `--bin-path` points at the compiled WASM — this must be rebuilt before generating if the schema changed.

## After running

1. **Switch to Unity** — the editor detects file changes automatically
2. **Assets → Reimport All** — run this if Unity doesn't pick up the new files automatically, or if you see "type not found" errors on previously valid code

Unity generates `.csproj` and `.sln` files; re-running `Assets → Open C# Project` is only needed if you've added new Assembly Definition files, not for routine autogen refreshes.

## Rules

- **Never edit files in `Assets/Scripts/autogen/`** — they are fully overwritten on every `spacetime generate` run. Any manual changes will be silently lost.
- All generated types, reducers, and event callbacks come from this folder. If something looks wrong in autogen, fix it on the server side and regenerate.
- The `autogen/` directory is listed in `.gitignore` (or should be) — it's a build artifact, not source.

## Verifying it worked

After reimporting, check:
- No red errors in the Unity Console referencing `autogen` types
- The `Assets/Scripts/autogen/` folder contains files with names matching your tables and reducers
- IntelliSense in VS Code/Rider resolves the generated types (may require reloading the project)

## Full deploy + generate sequence

If you're doing a full server update:

```bash
# Build
cd server && spacetime build

# Publish (add --delete-data only for breaking schema changes)
spacetime publish --server local zoneforge-server

# Regenerate bindings
cd ../client && spacetime generate \
  --lang csharp \
  --out-dir Assets/Scripts/autogen \
  --bin-path ../server/spacetimedb/target/wasm32-unknown-unknown/release/zoneforge_server.wasm
```

Then in Unity: **Assets → Reimport All**.

See the `zoneforge-deploy` skill for the full annotated workflow.
