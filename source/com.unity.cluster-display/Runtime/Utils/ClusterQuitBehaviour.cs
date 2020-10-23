using System;
using Unity.ClusterDisplay;
using UnityEngine;

namespace ClusterDisplay.Utils
{
	/// <summary>
	/// Listen to input command to quit the application on all nodes
	/// </summary>
	/// <remarks>If this is not present present upon receiving the quit message the nodes will only disconnect, but not quit the applciation.</remarks>
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