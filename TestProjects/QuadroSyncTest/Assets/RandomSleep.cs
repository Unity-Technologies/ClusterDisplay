using System;
using System.Threading;
using Unity.ClusterDisplay;
using UnityEngine;

public class RandomSleep : MonoBehaviour
{
    private int frame = 0;
    private void Update()
    {
        frame++;
        if (frame % 100 == 0)
        {
            var rnd = new System.Random(ClusterSync.Instance.DynamicLocalNodeId + Time.frameCount);
            var sleept = rnd.Next(1, 20);
            Thread.Sleep(sleept);
        }
    }
}