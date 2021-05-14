using System;
using Unity.ClusterDisplay;
using UnityEngine;

namespace ClusterDisplay.Utils
{
	/// <summary>
	/// This class is responsible for listening to input commands to quit the application on all nodes.
	/// </summary>
	/// <remarks>If this class is not present upon reception of the quit message, the nodes only disconnect but don't quit the application.</remarks>
    public class ClusterQuitBehaviour : MonoBehaviour
    {
        private void Update()
        {
            if (ClusterDisplayState.IsActive)
            {
                if (Input.GetKeyUp(KeyCode.K) || Input.GetKeyUp(KeyCode.Q))
                {
                    if (ClusterSync.TryGetInstance(out var clusterSync))
                        clusterSync.ShutdownAllClusterNodes();
                }

            }
            else
            {
                if (ClusterDisplayState.IsTerminated)
                    Application.Quit(0);
            }
        }
    }
}