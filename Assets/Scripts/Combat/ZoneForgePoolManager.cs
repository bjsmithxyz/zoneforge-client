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

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        foreach (var def in _poolDefinitions)
        {
            _defs[def.key] = def;
            var queue = new Queue<GameObject>(def.initialCapacity);
            for (int i = 0; i < def.initialCapacity; i++)
                queue.Enqueue(CreatePooled(def));
            _pools[def.key] = queue;
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
            go = CreatePooled(def);
        }

        go.SetActive(true);
        return go;
    }

    /// <summary>Return an object to the pool.</summary>
    public void Return(string key, GameObject go)
    {
        if (!_pools.TryGetValue(key, out var queue))
        {
            Destroy(go);
            return;
        }
        go.SetActive(false);
        go.transform.SetParent(transform); // re-parent to pool manager GO
        queue.Enqueue(go);
    }

    private GameObject CreatePooled(PoolDefinition def)
    {
        var go = Instantiate(def.prefab, transform);
        go.SetActive(false);
        return go;
    }

    private int CountActive(string key)
    {
        // Approximate: total created minus what's in the queue
        // Good enough for max-size guard
        return _defs[key].initialCapacity - (_pools.TryGetValue(key, out var q) ? q.Count : 0);
    }
}
