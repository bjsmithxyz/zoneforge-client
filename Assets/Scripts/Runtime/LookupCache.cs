using System.Collections.Generic;
using SpacetimeDB.Types;

/// <summary>
/// Client-side row caches for hot lookups. Populated and maintained by
/// SpacetimeDBManager via OnSubscriptionApplied + table OnInsert/OnUpdate/OnDelete.
///
/// Reading code uses these dictionaries instead of full table .Iter() scans.
/// Empty until <c>SpacetimeDBManager.IsSubscribed == true</c>.
/// </summary>
public static class LookupCache
{
    public static readonly Dictionary<ulong, Ability>          Abilities  = new();
    public static readonly Dictionary<ulong, EnemyDefinition>  EnemyDefs  = new();
    public static readonly Dictionary<ulong, ItemDefinition>   ItemDefs   = new();
    // Composite key (playerId, abilityId) — there is at most one cooldown row per pair.
    public static readonly Dictionary<(ulong, ulong), PlayerCooldown> Cooldowns = new();

    /// <summary>Reset all caches. Called on disconnect / zone-rebuild flows.</summary>
    public static void Clear()
    {
        Abilities.Clear();
        EnemyDefs.Clear();
        ItemDefs.Clear();
        Cooldowns.Clear();
    }

    /// <summary>
    /// Wire row callbacks against an active connection. Called once from
    /// SpacetimeDBManager.OnSubscriptionApplied. Backfills initial rows that
    /// arrived before callbacks were registered.
    /// </summary>
    public static void HookCallbacks(SpacetimeDB.Types.RemoteTables db)
    {
        // Backfill (OnInsert does NOT fire for rows already present at subscribe).
        foreach (var a in db.Ability.Iter())       Abilities[a.Id]  = a;
        foreach (var d in db.EnemyDef.Iter())      EnemyDefs[d.Id]  = d;
        foreach (var d in db.ItemDef.Iter())       ItemDefs[d.Id]   = d;
        foreach (var cd in db.PlayerCooldown.Iter())
            Cooldowns[(cd.PlayerId, cd.AbilityId)] = cd;

        db.Ability.OnInsert += (_, a) => Abilities[a.Id] = a;
        db.Ability.OnDelete += (_, a) => Abilities.Remove(a.Id);

        db.EnemyDef.OnInsert += (_, d) => EnemyDefs[d.Id] = d;
        db.EnemyDef.OnDelete += (_, d) => EnemyDefs.Remove(d.Id);

        db.ItemDef.OnInsert += (_, d) => ItemDefs[d.Id] = d;
        db.ItemDef.OnDelete += (_, d) => ItemDefs.Remove(d.Id);

        db.PlayerCooldown.OnInsert += (_, cd) => Cooldowns[(cd.PlayerId, cd.AbilityId)] = cd;
        db.PlayerCooldown.OnUpdate += (_, _old, cd) => Cooldowns[(cd.PlayerId, cd.AbilityId)] = cd;
        db.PlayerCooldown.OnDelete += (_, cd) => Cooldowns.Remove((cd.PlayerId, cd.AbilityId));
    }
}
