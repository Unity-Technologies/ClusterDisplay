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
        if (ClusterSync.Active)
        {
            text.text = $"Node {ClusterSync.Instance.DynamicLocalNodeId}";
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
        if (ClusterSync.Active)
        {
            //if (frmaes % 60 == 0)
            {
                text.text = ClusterSync.Instance.GetDebugString();
            }

            if (Input.GetKeyUp(KeyCode.K) || Input.GetKeyUp(KeyCode.Q))
                ClusterSync.Instance.ShutdownAllClusterNodes();
            
        }
        else
        {
            if (ClusterSync.Terminated)
                Application.Quit(0);

            text.text = "Cluster Rendering inactive";
        }
    }

}
