using System;
using System.Threading;
using Unity.ClusterRendering;
using UnityEngine;

namespace DefaultNamespace
{
    public class RandomSleep : MonoBehaviour
    {
        private void Update()
        {
          var rnd  = new System.Random(ClusterSynch.Instance.DynamicLocalNodeId+Time.frameCount);
          var sleept = rnd.Next(1, 20);
          Thread.Sleep(sleept);
        }
    }
}