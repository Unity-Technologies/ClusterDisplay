using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestScript : MonoBehaviour
{
    private void Start()
    {
        StartCoroutine(TestCoroutine());
    }

    private IEnumerator TestCoroutine ()
    {
        yield return null;
        if (Unity.ClusterDisplay.ClusterDisplayState.IsEmitter)
        {
            SimpleMethodTest();
            SimpleStringTest("This is a string.");
        }
    }

    [Unity.ClusterDisplay.RPC.ClusterRPC]
    public void SimpleMethodTest ()
    {
        Debug.Log("HELLO WORLD 9");
    }

    [Unity.ClusterDisplay.RPC.ClusterRPC]
    public void SimpleStringTest (string message)
    {
        Debug.Log(message);
    }
}
