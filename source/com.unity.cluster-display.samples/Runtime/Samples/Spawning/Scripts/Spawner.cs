using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.ClusterDisplay;
using Unity.ClusterDisplay.RPC;
using UnityEngine;

public class Spawner : MonoBehaviour
{
    [SerializeField] private GameObject prefab;
    private List<ColorSetter> colorSetters = new List<ColorSetter>();

    private void Start() =>
        StartCoroutine(SpawnLoop());

    [ClusterRPC]
    public void SpawnPrefab(Vector3 position)
    {
        if (!SceneObjectsRegistry.TryGetSceneInstance(gameObject.scene.path, out var sceneObjectsRegistry))
            return;
        
        var instance = GameObject.Instantiate(prefab);
        instance.transform.position = position;
        
        var colorSetter = instance.GetComponent<ColorSetter>();
        colorSetters.Add(colorSetter);

        sceneObjectsRegistry.TryRegister(colorSetter);
    }
    
    private void RecolorAll ()
    {
        for (int i = 0; i < colorSetters.Count; i++)
            colorSetters[i].SetColor(new Color(Random.value, Random.value, Random.value, 1f));
    }

    private IEnumerator SpawnLoop()
    {
        var waitForSeconds = new WaitForSeconds(1);
        yield return waitForSeconds;
        
        if (!ClusterDisplayState.IsEmitter)
            yield break;
        
        while (true)
        {
            yield return waitForSeconds;
            SpawnPrefab(new Vector3(Random.Range(-10, 10), Random.Range(-10, 10), 0f));
            RecolorAll();
        }
    }
}
