using System.Collections;
using System.Collections.Generic;
using Unity.ClusterDisplay;
using UnityEngine;

public class FoodSpawner : MonoBehaviour
{
    [SerializeField] private PoolablePrefab foodPrefab;

    private void Start()
    {
        World.SetFoodParent(transform);
        World.SetFoodPrefab(foodPrefab);

        StartCoroutine(FoodSpawnerCoroutine());
    }

    private IEnumerator FoodSpawnerCoroutine ()
    {
        WaitForSeconds waitForSeconds = new WaitForSeconds(0.5f);
        while (true)
        {
            yield return waitForSeconds;

            World.SpawnFood(new Vector3(
                RandomWrapper.Range(-10, 10),
                RandomWrapper.Range(-10, 10),
                RandomWrapper.Range(-10, 10)));
        }
    }

    private void OnDestroy()
    {
        World.Dispose();
    }
}
