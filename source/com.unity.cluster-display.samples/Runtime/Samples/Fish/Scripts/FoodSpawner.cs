using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.ClusterDisplay;
using Unity.ClusterDisplay.Graphics;

public class FoodSpawner : MonoBehaviour
{
    [SerializeField] private PoolablePrefab foodPrefab = null;

    private void Start()
    {
        World.SetFoodParent(transform);
        World.SetFoodPrefab(foodPrefab);
    }

    private void Update()
    {
        if (ClusterDisplayState.IsEmitter)
            if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space))
                World.SpawnFood(FishUtils.GetWorldInteractionPosition());
    }

    private void OnDestroy() => World.Dispose();
}
