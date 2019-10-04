using Unity.ClusterRendering;
using UnityEngine;

[RequireComponent(typeof(UnityEngine.UI.Text))]
public class DebugComponent : MonoBehaviour
{

    // Start is called before the first frame update
    void Start()
    {
        var text = this.GetComponent<UnityEngine.UI.Text>();
        if (ClusterSynch.Active)
        {
            text.text = $"Node {ClusterSynch.Instance.DynamicLocalNodeId}";
        }
        else
        {
            text.text = "Cluster Rendering inactive";
        }
    }

    void Update()
    {
        var text = this.GetComponent<UnityEngine.UI.Text>();
        if (ClusterSynch.Active)
        {
            text.text = ClusterSynch.Instance.GetDebugString();

            if (Input.GetKeyUp(KeyCode.K) || Input.GetKeyUp(KeyCode.Q))
                ClusterSynch.Instance.ShutdownAllClusterNodes();
        }
        else
        {
            if (ClusterSynch.Terminated)
                Application.Quit(0);

            text.text = "Cluster Rendering inactive";
        }
    }

}
