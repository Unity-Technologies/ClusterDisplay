using System.Collections;
using System.Collections.Generic;
using Unity.ClusterDisplay.RPC;
using UnityEngine;

public class Hello : MonoBehaviour
{
    private void Awake()
    {
        World(1.4f);
    }

    [ClusterRPC]
    public void World(float plz) {if(RPCBufferIO.CaptureExecution){}Debug.Log(plz);}}
