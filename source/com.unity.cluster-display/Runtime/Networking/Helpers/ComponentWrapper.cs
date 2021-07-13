using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.ClusterDisplay.RPC
{
    public class ComponentWrapper<InstanceType> : MonoBehaviour
        where InstanceType : UnityEngine.Component
    {
        [SerializeField] protected InstanceType instance;
        private void OnValidate()
        {
            instance = GetComponent<InstanceType>();
        }
    }
}
