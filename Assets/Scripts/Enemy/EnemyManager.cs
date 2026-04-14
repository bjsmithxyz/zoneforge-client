using System.Collections.Generic;
using UnityEngine;
using SpacetimeDB.Types;

/// <summary>
/// Scene singleton. Subscribes to SpacetimeDBManager enemy events and maintains
/// one capsule GameObject per Enemy row. Mirrors PlayerManager pattern.
/// Requires CombatManager to be present in the scene before OnConnected fires.
/// </summary>
public class EnemyManager : MonoBehaviour
{
    public static EnemyManager Instance { get; private set; }

    private readonly Dictionary<ulong, GameObject> _enemies = new();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        SpacetimeDBManager.OnEnemyInserted += OnEnemyInserted;
        SpacetimeDBManager.OnEnemyUpdated  += OnEnemyUpdated;
        SpacetimeDBManager.OnEnemyDeleted  += OnEnemyDeleted;
        SpacetimeDBManager.OnConnected     += OnConnected;
    }

    void OnDestroy()
    {
        SpacetimeDBManager.OnEnemyInserted -= OnEnemyInserted;
        SpacetimeDBManager.OnEnemyUpdated  -= OnEnemyUpdated;
        SpacetimeDBManager.OnEnemyDeleted  -= OnEnemyDeleted;
        SpacetimeDBManager.OnConnected     -= OnConnected;
    }

    void OnConnected()
    {
        // Backfill: initial rows arrive before SpacetimeDBManager registers callbacks
        foreach (var enemy in SpacetimeDBManager.Conn.Db.Enemy.Iter())
        {
            if (!_enemies.ContainsKey(enemy.Id))
                SpawnEnemy(enemy);
        }
    }

    void OnEnemyInserted(Enemy enemy)
    {
        if (_enemies.ContainsKey(enemy.Id)) return;
        SpawnEnemy(enemy);
    }

    void OnEnemyUpdated(Enemy oldEnemy, Enemy newEnemy)
    {
        if (!_enemies.TryGetValue(newEnemy.Id, out var go)) return;
        go.GetComponent<EnemyController>()?.ReceiveUpdate(newEnemy);
        CombatManager.Instance?.RegisterEnemyPosition(
            newEnemy.Id,
            new Vector3(newEnemy.PositionX, 1f, newEnemy.PositionY));
    }

    void OnEnemyDeleted(Enemy enemy)
    {
        if (!_enemies.TryGetValue(enemy.Id, out var go)) return;
        Destroy(go);
        _enemies.Remove(enemy.Id);
    }

    /// <summary>Returns the GameObject for an enemy id, or null if not spawned.</summary>
    public GameObject GetEnemyObject(ulong enemyId) =>
        _enemies.TryGetValue(enemyId, out var go) ? go : null;

    void SpawnEnemy(Enemy enemy)
    {
        // Look up the definition for color/name — iterate since no direct key access
        EnemyDefinition def = null;
        foreach (var d in SpacetimeDBManager.Conn.Db.EnemyDef.Iter())
        {
            if (d.Id == enemy.EnemyDefId) { def = d; break; }
        }

        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = $"Enemy_{enemy.Id}_{def?.Name ?? "Unknown"}";
        go.transform.localScale = new Vector3(0.7f, 0.75f, 0.7f);

        var rend = go.GetComponent<Renderer>();
        var mat  = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = GetEnemyColor(def?.EnemyType ?? EnemyType.Melee);
        rend.material = mat;

        // Disable collider — server owns position; no physics needed
        var col = go.GetComponent<CapsuleCollider>();
        if (col != null) col.enabled = false;

        var ctrl = go.AddComponent<EnemyController>();
        ctrl.Init(enemy);

        var hb = go.AddComponent<EnemyHealthBar>();
        hb.Init(enemy, def);

        _enemies[enemy.Id] = go;
        CombatManager.Instance?.RegisterEnemyPosition(
            enemy.Id,
            new Vector3(enemy.PositionX, 1f, enemy.PositionY));

        Debug.Log($"[EnemyManager] Spawned {go.name} at ({enemy.PositionX}, {enemy.PositionY})");
    }

    static Color GetEnemyColor(EnemyType type) => type switch
    {
        EnemyType.Melee  => new Color(1f, 0.45f, 0.1f),  // orange
        EnemyType.Ranged => new Color(1f, 0.90f, 0.1f),  // yellow
        EnemyType.Caster => new Color(0.6f, 0.1f, 0.9f), // purple
        _                => Color.white,
    };
}
