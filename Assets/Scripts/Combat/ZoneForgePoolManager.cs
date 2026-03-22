using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton pool manager. Pre-allocates GameObjects at Awake and recycles
/// them throughout the session. Register pool definitions in the Inspector.
/// </summary>
public class ZoneForgePoolManager : MonoBehaviour
{
    public static ZoneForgePoolManager Instance { get; private set; }

    [Serializable]
    public class PoolDefinition
    {
        public string key;
        public GameObject prefab;
        public int initialCapacity = 10;
        public int maxSize = 60;
    }

    [SerializeField] private List<PoolDefinition> _poolDefinitions = new();

    private readonly Dictionary<string, Queue<GameObject>> _pools = new();
    private readonly Dictionary<string, PoolDefinition> _defs = new();
    private readonly Dictionary<string, int> _totalCreated = new();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        foreach (var def in _poolDefinitions)
        {
            if (def.prefab == null)
            {
                Debug.LogError($"[PoolManager] PoolDefinition '{def.key}' has no prefab assigned — skipping.");
                continue;
            }
            _defs[def.key] = def;
            var queue = new Queue<GameObject>(def.initialCapacity);
            for (int i = 0; i < def.initialCapacity; i++)
                queue.Enqueue(CreatePooled(def.key, def));
            _pools[def.key] = queue;
            _totalCreated[def.key] = def.initialCapacity;
            Debug.Log($"[PoolManager] Pre-allocated {def.initialCapacity}x '{def.key}'");
        }
    }

    /// <summary>Retrieve an object from the pool. Returns null if pool is empty and at max size.</summary>
    public GameObject Get(string key)
    {
        if (!_pools.TryGetValue(key, out var queue))
        {
            Debug.LogWarning($"[PoolManager] Unknown pool key: '{key}'");
            return null;
        }

        GameObject go;
        if (queue.Count > 0)
        {
            go = queue.Dequeue();
        }
        else
        {
            // Pool exhausted — grow if under max
            if (!_defs.TryGetValue(key, out var def) || CountActive(key) >= def.maxSize)
            {
                Debug.LogWarning($"[PoolManager] Pool '{key}' exhausted and at max size");
                return null;
            }
            go = CreatePooled(key, def);
        }

        go.SetActive(true);
        return go;
    }

    /// <summary>Return an object to the pool.</summary>
    public void Return(string key, GameObject go)
    {
        if (go == null) return;
        if (!_pools.TryGetValue(key, out var queue))
        {
            Destroy(go);
            return;
        }
        go.SetActive(false);
        go.transform.SetParent(transform); // re-parent to pool manager GO
        queue.Enqueue(go);
    }

    private GameObject CreatePooled(string key, PoolDefinition def)
    {
        if (!_totalCreated.ContainsKey(key)) _totalCreated[key] = 0;
        _totalCreated[key]++;
        var go = Instantiate(def.prefab, transform);
        go.SetActive(false);
        return go;
    }

    private int CountActive(string key)
    {
        int total = _totalCreated.TryGetValue(key, out var t) ? t : 0;
        int queued = _pools.TryGetValue(key, out var q) ? q.Count : 0;
        return total - queued;
    }
}
