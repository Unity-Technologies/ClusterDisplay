using System.Collections;
using System.Collections.Generic;
using Unity.ClusterDisplay;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class MatchPosition : MonoBehaviour
{
    private Rigidbody rigidBody;
    private void OnValidate()
    {
        rigidBody = GetComponent<Rigidbody>();
    }

    // [RPC(rpcExecutionStage: RPCExecutionStage.ImmediatelyOnArrival)]
    public void Match (Vector3 position, Quaternion rotation)
    {
        transform.position = position;
        transform.rotation = rotation;
    }

    private void FixedUpdate()
    {
        if (ClusterDisplayState.IsEmitter)
        {
        }
    }
}
