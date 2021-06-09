using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public enum RPCExecutionStage : int
{

    /// <summary>
    /// When the method is executed by master, RPCEmitter will determine
    /// which execution stage it currently is and send the RPC with that
    /// RPC execution stage.
    /// </summary>
    Automatic = 0,

    /// <summary>
    /// Execute immediately on receipt, this could potentially be executed
    /// before Awake, Start or OnEnable if the RPC is sent on the first frame.
    /// </summary>
    ImmediatelyOnArrival = 1,

    BeforeFixedUpdate = 2,
    AfterFixedUpdate = 3,

    BeforeUpdate = 4,
    AfterUpdate = 5,

    BeforeLateUpdate = 6,
    AfterLateUpdate = 7
}

[System.Serializable]
public struct SerializedRPC
{
    [SerializeField] public ushort rpcId;
    [SerializeField] public bool isStatic;

    [SerializeField] public int rpcExecutionStage;

    [SerializeField] public string declaringAssemblyName;
    [SerializeField] public string declaryingTypeFullName;
    [SerializeField] public string declaringReturnTypeAssemblyName;
    [SerializeField] public string returnTypeFullName;
    [SerializeField] public string methodName;

    [SerializeField] public string[] declaringParameterTypeAssemblyNames;
    [SerializeField] public string[] parameterTypeFullNames;
    [SerializeField] public string[] parameterNames;

    public int ParameterCount => parameterNames.Length;

    public (string declaringParameterTypeAssemblyName, string parameterTypeFullName, string parameterName) this[int parameterIndex]
    {
        get =>
            parameterTypeFullNames != null ? (
                declaringParameterTypeAssemblyNames[parameterIndex],
                parameterTypeFullNames[parameterIndex], 
                parameterNames[parameterIndex]) 

            : (null, null, null);
    }
}

[System.Serializable]

public struct SerializedRPCs
{
    public SerializedRPC[] rpcs;
}
