using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.ClusterDisplay.Networking
{
    public class ComponentWrapper<InstanceType> : MonoBehaviour
        where InstanceType : UnityEngine.Component
    {
        protected InstanceType instance;
        public void Setup (InstanceType instance) => this.instance = instance;
    }
}
