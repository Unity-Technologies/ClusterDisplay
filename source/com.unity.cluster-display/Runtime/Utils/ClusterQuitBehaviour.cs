using Unity.ClusterDisplay;
using Unity.ClusterDisplay.Utils;
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
            if (!ServiceLocator.TryGet(out ClusterSync clusterSync))
            {
                return;
            }

            if (clusterSync.StateAccessor.IsActive)
            {
                if (Input.GetKeyUp(KeyCode.K) || Input.GetKeyUp(KeyCode.Q))
                {
                    clusterSync.ShutdownAllClusterNodes();
                }
            }
            else if (clusterSync.StateAccessor.IsTerminated)
                Application.Quit(0);
        }
    }
}
