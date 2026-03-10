---
name: unity-spacetimedb-subscribe
description: Wire up a SpacetimeDB table subscription and row callbacks in the ZoneForge Unity C# client. Use this skill whenever the user wants to receive server data in Unity, listen for table updates, react to row inserts/updates/deletes, add a new table to the client subscription, or fix callbacks that aren't firing. The SpacetimeDB C# SDK has a strict ordering requirement (connect → subscribe → callbacks) that causes silent failures when violated — this skill enforces the correct pattern.
---

## The ordering rule

The SpacetimeDB C# SDK has a strict three-phase sequence. Violating the order causes silent failures — no errors, just no callbacks firing.

```
Build connection → OnConnect fires → Subscribe → OnSubscriptionApplied fires → Register callbacks
```

Never subscribe before `OnConnect`. Never register callbacks before `OnSubscriptionApplied`.

## Step 1: Build the connection

In `SpacetimeDBManager.cs` (or wherever the connection is managed):

```csharp
private DbConnection _conn;

void Start()
{
    _conn = DbConnection.Builder()
        .WithUri("http://localhost:3000")
        .WithModuleName("zoneforge-server")
        .OnConnect(OnConnect)
        .OnConnectError(OnConnectError)
        .OnDisconnect(OnDisconnect)
        .Build();
}
```

The connection is asynchronous — `OnConnect` fires when the handshake completes. Do not try to subscribe or query data before then.

## Step 2: Subscribe inside OnConnect

Add every table you need to the subscription query list. Using `SELECT * FROM table_name` is the standard pattern:

```csharp
void OnConnect(DbConnection conn, Identity identity, string token)
{
    conn.SubscriptionBuilder()
        .OnApplied(OnSubscriptionApplied)
        .Subscribe(new[]
        {
            "SELECT * FROM player",
            "SELECT * FROM zone",
            "SELECT * FROM tile",        // ← add new tables here
            "SELECT * FROM zone_object", // ← add new tables here
        });
}
```

Subscriptions are additive — you can call `Subscribe` multiple times if needed, but a single call with all tables is simpler to maintain.

## Step 3: Register callbacks inside OnSubscriptionApplied

`OnSubscriptionApplied` fires after the initial snapshot of subscribed data is delivered. Register row callbacks here — the local cache is populated at this point and it's safe to iterate rows.

```csharp
void OnSubscriptionApplied(SubscriptionEventContext ctx)
{
    // Row event callbacks
    ctx.Db.Player.OnInsert += OnPlayerInserted;
    ctx.Db.Player.OnUpdate += OnPlayerUpdated;
    ctx.Db.Player.OnDelete += OnPlayerDeleted;

    ctx.Db.Tile.OnInsert += OnTileInserted;
    ctx.Db.Tile.OnDelete += OnTileDeleted;

    // Safe to read initial data here too
    foreach (var tile in ctx.Db.Tile.Iter())
    {
        SpawnTile(tile);
    }
}
```

## Step 4: FrameTick — never skip this

The SDK does not process messages automatically. Call `FrameTick()` in every `Update()` frame or no callbacks will ever fire:

```csharp
void Update()
{
    _conn?.FrameTick();
}
```

This is the most common cause of "my callbacks never fire" — missing `FrameTick()`.

## Callback signatures

```csharp
// Insert — new row arrived
void OnPlayerInserted(EventContext ctx, Player newPlayer)
{
    Debug.Log($"Player {newPlayer.Name} connected");
    SpawnPlayerObject(newPlayer);
}

// Update — row changed (old and new values both available)
void OnPlayerUpdated(EventContext ctx, Player oldPlayer, Player newPlayer)
{
    if (oldPlayer.Online != newPlayer.Online)
    {
        UpdatePlayerVisibility(newPlayer);
    }
}

// Delete — row removed
void OnPlayerDeleted(EventContext ctx, Player player)
{
    DespawnPlayerObject(player.Identity);
}
```

## Calling reducers

Generated reducer classes live in `Assets/Scripts/autogen/`. Call them via the static `Reducer` class:

```csharp
// Call a reducer — fire and forget (reducers don't return data)
Reducer.PlaceTile(_conn, zoneId, x, y, TileKind.Grass);
Reducer.RenameZone(_conn, zoneId, newName);
```

Reducers don't return data — the result comes back as a table row insert/update that triggers your callbacks.

## Reading data from the local cache

After `OnSubscriptionApplied`, data is available in the local cache:

```csharp
// Iterate all rows
foreach (var tile in _conn.Db.Tile.Iter())
{
    // ...
}

// Find by primary key
var player = _conn.Db.Player.FindByIdentity(identity);
```

The cache updates automatically as the server sends changes — you don't need to poll.

## Handling connection errors

```csharp
void OnConnectError(Exception e)
{
    Debug.LogError($"SpacetimeDB connection failed: {e.Message}");
    // Check: is spacetime start running? Is the module published?
}

void OnDisconnect(DbConnection conn, Exception? e)
{
    if (e != null) Debug.LogWarning($"Disconnected with error: {e.Message}");
}
```

## Checklist

- [ ] `DbConnection.Builder()` called in `Start()` or equivalent
- [ ] `OnConnect`, `OnConnectError`, `OnDisconnect` callbacks registered on builder
- [ ] `Subscribe(...)` called inside `OnConnect` — not in `Start()` or `Awake()`
- [ ] New table added to the `Subscribe(new[] { ... })` query list
- [ ] `OnSubscriptionApplied` registered on `SubscriptionBuilder`
- [ ] Row callbacks registered inside `OnSubscriptionApplied`
- [ ] `_conn.FrameTick()` called every `Update()` frame
- [ ] Reducer called from game logic to trigger server-side mutations

## Common mistakes

| Symptom | Cause | Fix |
|---------|-------|-----|
| Callbacks never fire | Missing `FrameTick()` | Add `_conn?.FrameTick()` to `Update()` |
| Callbacks never fire | Registered before `OnSubscriptionApplied` | Move registration into `OnSubscriptionApplied` |
| No data after connect | Subscribe called before `OnConnect` | Move `Subscribe(...)` into `OnConnect` callback |
| Autogen types unresolved | Schema changed, bindings stale | Run `spacetime generate`, then Assets → Reimport All |
| Server table not visible | Table not marked `public` in Rust | Add `public` to `#[table(...)]` in server code |
