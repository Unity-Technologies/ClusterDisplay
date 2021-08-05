using System.Collections;
using System.Collections.Generic;
using Unity.ClusterDisplay;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public static class World
{
    private static NativeArray<float3> foodPositions = new NativeArray<float3>(128, Allocator.Persistent, NativeArrayOptions.ClearMemory);
    private static NativeArray<bool> foodAvailable = new NativeArray<bool>(128, Allocator.Persistent, NativeArrayOptions.ClearMemory);

    public static NativeArray<float3> FoodPositions => foodPositions;
    public static NativeArray<bool> FoodAvailable => foodAvailable;


    public static List<GameObject> food = new List<GameObject>(128);
    public static List<int> eatenFoodIndices = new List<int>();

    private static PoolablePrefab foodPrefab;
    private static Transform foodInstanceParent;

    public static void SetFoodPrefab(PoolablePrefab foodPrefab) => World.foodPrefab = foodPrefab;
    public static void SetFoodParent(Transform parent) => foodInstanceParent = parent;

    // [RPC]
    public static void SpawnFood (Vector3 foodPosition)
    {
        DeterministicUtils.LogCall(foodPosition);
        if (!PrefabPool.TryGetInstance(out var prefabPool))
            return;

        GameObject go = prefabPool.Spawn(foodPrefab, parent: foodInstanceParent, setActive: true);
        go.transform.position = foodPosition;

        int foodIndex = 0;
        if (eatenFoodIndices.Count > 0)
        {
            foodIndex = eatenFoodIndices[eatenFoodIndices.Count - 1];
            food[foodIndex] = go;
        }

        else
        {
            food.Add(go);
            foodIndex = food.Count - 1;
        }

        if (food.Count > foodPositions.Length)
        {
            NativeArray<float3> tempPositions = new NativeArray<float3>(foodPositions.Length, Allocator.Temp, NativeArrayOptions.ClearMemory);
            NativeArray<bool> tempAvailable = new NativeArray<bool>(foodAvailable.Length, Allocator.Temp, NativeArrayOptions.ClearMemory);

            tempPositions.CopyFrom(foodPositions);
            tempAvailable.CopyFrom(foodAvailable);

            Dispose();

            foodPositions = new NativeArray<float3>(food.Count + 128, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            foodAvailable = new NativeArray<bool>(food.Count + 128, Allocator.Persistent, NativeArrayOptions.ClearMemory);

            tempPositions.CopyTo(foodPositions);
            tempAvailable.CopyTo(foodAvailable);

            tempPositions.Dispose();
            tempAvailable.Dispose();
        }

        foodAvailable[foodIndex] = true;
        foodPositions[foodIndex] = foodPosition;
    }

    // [RPC]
    public static bool EatFood (int foodIndex)
    {
        DeterministicUtils.LogCall(foodIndex);
        if (food[foodIndex] == null)
            return false;

        // Debug.Log($"Eating food: {foodIndex}");
        if (!PrefabPool.TryGetInstance(out var prefabPool))
            return false;

        prefabPool.Despawn(foodPrefab, food[foodIndex]);

        food[foodIndex] = null;
        foodAvailable[foodIndex] = false;

        eatenFoodIndices.Add(foodIndex);
        return true;
    }

    public static void Dispose ()
    {
        if (foodPositions.IsCreated)
            foodPositions.Dispose();

        if (foodAvailable.IsCreated)
            foodAvailable.Dispose();
    }
}
