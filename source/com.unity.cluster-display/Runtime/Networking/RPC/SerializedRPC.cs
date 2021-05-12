﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct SerializedRPC
{
    [SerializeField] public ushort rpcId;
    [SerializeField] public bool isStatic;

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