using System;
using Unity.ClusterDisplay;
using UnityEngine;

namespace ClusterDisplay.Utils
{
    public class ClusterQuitBehaviour : MonoBehaviour
    {
        private void Update()
        {
            if (ClusterSync.Active)
            {
                if (Input.GetKeyUp(KeyCode.K) || Input.GetKeyUp(KeyCode.Q))
                    ClusterSync.Instance.ShutdownAllClusterNodes();

            }
            else
            {
                if (ClusterSync.Terminated)
                    Application.Quit(0);
            }
        }
    }
}