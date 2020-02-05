using System;
using System.Threading;
using Unity.ClusterRendering;
using UnityEngine;

namespace DefaultNamespace
{
    public class RandomSleep : MonoBehaviour
    {
        private int frame = 0;
        private void Update()
        {
            frame++;
            if (frame % 100 == 0)
            {
                var rnd = new System.Random(ClusterSynch.Instance.DynamicLocalNodeId + Time.frameCount);
                var sleept = rnd.Next(1, 20);
                Thread.Sleep(sleept);
            }
        }
    }
}