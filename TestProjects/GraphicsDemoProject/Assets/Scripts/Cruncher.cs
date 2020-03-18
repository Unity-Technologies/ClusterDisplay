using System;
using System.Collections;
using System.Collections.Generic;
using Unity.ClusterRendering;
using UnityEngine;
using Random = UnityEngine.Random;

// just crunch numbers to slow the machine down
public class Cruncher : MonoBehaviour
{
    void Update()
    {
        // if cluster is active and we are master
        if (ClusterSynch.Active && 
            ClusterSynch.Instance.DynamicLocalNodeId == 0 &&
            Input.GetKey(KeyCode.Space))
        {
            var sum = 0f;
            for (var i = 0; i != 10e6; ++i)
                sum += Random.value;
            Debug.Log(sum);
        }
    }
}
