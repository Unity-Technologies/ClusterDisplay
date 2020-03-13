using System;
using Unity.ClusterRendering;
using UnityEngine;

namespace ClusterRendering.Runtime.Utils
{
    public class ClusterQuitBehaviour : MonoBehaviour
    {
        private void Update()
        {
            if (ClusterSynch.Active)
            {
                if (Input.GetKeyUp(KeyCode.K) || Input.GetKeyUp(KeyCode.Q))
                    ClusterSynch.Instance.ShutdownAllClusterNodes();

            }
            else
            {
                if (ClusterSynch.Terminated)
                    Application.Quit(0);
            }
        }
    }
}