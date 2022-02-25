using Unity.ClusterDisplay;
using UnityEngine;

[RequireComponent(typeof(UnityEngine.UI.Text))]
public class DebugComponent : MonoBehaviour
{
    private int frmaes;
    // Start is called before the first frame update
    void Start()
    {
        frmaes = 0;
        var text = this.GetComponent<UnityEngine.UI.Text>();
        if (ClusterDisplayState.IsActive)
        {
            text.text = $"Node {ClusterDisplayState.NodeID}";
        }
        else
        {
            text.text = "Cluster Rendering inactive";
        }
    }

    void Update()
    {
        frmaes++;
        var text = this.GetComponent<UnityEngine.UI.Text>();
        if (ClusterDisplayState.IsActive)
        {
            //if (frmaes % 60 == 0)
            {
                text.text = ClusterSyncDebug.GetDebugString();
            }
            
        }
        else
        {
            if (ClusterDisplayState.IsTerminated)
                Application.Quit(0);

            text.text = "Cluster Rendering inactive";
        }
    }

}
