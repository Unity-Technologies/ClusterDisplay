using System;
using System.Threading;
using Unity.ClusterDisplay;
using UnityEngine;

public class RandomSleep : MonoBehaviour
{
    private int frame = 0;

    private void Update()
    {
        if (ClusterDisplayState.TryGetRuntimeNodeId(out var nodeId))
        {
            frame++;
            if (frame % 100 == 0)
            {
                var rnd = new System.Random(nodeId + Time.frameCount);
                var sleept = rnd.Next(100, 2000);
                Thread.Sleep(sleept);
            }
        }
    }
}
