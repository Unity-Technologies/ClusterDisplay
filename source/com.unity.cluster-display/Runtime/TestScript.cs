using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestScript : MonoBehaviour
{
    [Unity.ClusterDisplay.RPC.ClusterRPC]
    private void Awake()
    {
        Debug.Log("HELLO WORLD 59");
    }
}
