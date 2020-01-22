using Unity.ClusterRendering;
using UnityEngine;
using UnityEngine.Playables;

[RequireComponent(typeof(UnityEngine.UI.Text))]
public class DebugComponent : MonoBehaviour
{
    private int frmaes;
    // Start is called before the first frame update
    void Start()
    {
        frmaes = 0;
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
        frmaes++;
        var text = this.GetComponent<UnityEngine.UI.Text>();
        if (ClusterSynch.Active)
        {
            //if (frmaes % 60 == 0)
            {
                text.text = ClusterSynch.Instance.GetDebugString();
            }

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
