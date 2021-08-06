using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PoolablePrefab", menuName = "ScriptableObjects/Poolable Prefab", order = 1)]
public class PoolablePrefab : ScriptableObject
{
    public string PoolName => name;

    [SerializeField] private GameObject prefab = null;
    public GameObject Prefab => prefab;
    public int initialPoolSize = 10;
    public int poolIncreaseSize = 5;
}
