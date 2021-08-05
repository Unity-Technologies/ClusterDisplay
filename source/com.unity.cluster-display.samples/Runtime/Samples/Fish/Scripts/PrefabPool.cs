using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PrefabPool : SingletonMonoBehaviour<PrefabPool>
{
    [SerializeField] private PoolablePrefab[] prefabs;

    private class Pool
    {
        public readonly GameObject container;

        public readonly List<GameObject> activePrefabs = new List<GameObject>(100);
        public readonly List<GameObject> pooledPrefabs = new List<GameObject>(100);

        public Pool(GameObject _container) => container = _container;
    }

    private readonly Dictionary<PoolablePrefab, Pool> pools = new Dictionary<PoolablePrefab, Pool>();

    private void CreatePooledInstance (Pool pool, GameObject prefab)
    {
        GameObject instance = Instantiate(prefab, pool.container.transform, true);
        instance.SetActive(false);
        pool.pooledPrefabs.Add(instance);
    }

    private void IncreasePoolSize (Pool pool, GameObject prefab, int count)
    {
        for (int i = 0; i < count; i++)
            CreatePooledInstance(pool, prefab);
    }

    private void PoolPrefab (PoolablePrefab poolablePrefab)
    {
        var container = new GameObject(poolablePrefab.PoolName);
        container.transform.parent = transform;

        var pool = new Pool(container);
        IncreasePoolSize(pool, poolablePrefab.Prefab, poolablePrefab.initialPoolSize);
        pools.Add(poolablePrefab, pool);
    }

    private GameObject ActivateOne (Pool pool)
    {
        var instance = pool.pooledPrefabs[pool.pooledPrefabs.Count - 1];
        pool.pooledPrefabs.RemoveAt(pool.pooledPrefabs.Count - 1);
        pool.activePrefabs.Add(instance);
        return instance;
    }

    private void DeactivateOne (Pool pool, GameObject instance)
    {
        var index = pool.activePrefabs.IndexOf(instance);
        if (index == -1)
        {
            Debug.LogError($"Unable to find isntance: \"{instance.name}\" in active instance pool.");
            return;
        }

        pool.activePrefabs.RemoveAt(index);

        instance.SetActive(false);
        instance.transform.parent = pool.container.transform;

        pool.pooledPrefabs.Add(instance);
    }

    public GameObject Spawn (PoolablePrefab poolablePrefab, Transform parent = null, bool setActive = true)
    {
        if (!pools.TryGetValue(poolablePrefab, out var pool))
            PoolPrefab(poolablePrefab);

        if (pool.pooledPrefabs.Count == 0)
            IncreasePoolSize(pool, poolablePrefab.Prefab, poolablePrefab.poolIncreaseSize);

        var instance = ActivateOne(pool);

        if (parent != null)
            instance.transform.parent = parent;

        if (setActive)
            instance.SetActive(true);

        return instance;
    }

    public void Despawn (PoolablePrefab poolablePrefab, GameObject instance)
    {
        if (instance == null)
        {
            Debug.LogError("Cannot despawn null GameObject.");
            return;
        }

        if (pools.TryGetValue(poolablePrefab, out var pool))
            DeactivateOne(pool, instance);
    }

    private void Awake()
    {
        if (prefabs != null && prefabs.Length > 0)
            for (int i = 0; i < prefabs.Length; i++)
                PoolPrefab(prefabs[i]);
    }
}
