using System.Collections;
using System.Collections.Generic;
using Unity.ClusterDisplay.RPC;
using UnityEngine;

public class Hello : MonoBehaviour
{
    [ClusterRPC]
    public void World (float plz)
    {
        System.Console.WriteLine(plz);
    }
}
