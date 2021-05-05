using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    public class ClusterDisplayNetworkManager : SingletonMonoBehaviour<ClusterDisplayNetworkManager>
    {
        [SerializeField] private RPCRegistry rpcRegistry;
        [SerializeField] private ObjectRegistry objRegistry;

        public RPCRegistry RPCRegistry => rpcRegistry;
        public ObjectRegistry ObjectRegistry => objRegistry;

        private void Awake()
        {
            DontDestroyOnLoad(this);
        }
    }
}
