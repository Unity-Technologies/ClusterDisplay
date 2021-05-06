using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.Compilation;

namespace Unity.ClusterDisplay
{
    [CreateAssetMenu(fileName = "ClusterDisplayNetworkManager", menuName = "Cluster Display/Cluster Display Network Manager", order = 1)]
    public class ClusterDisplayNetworkManager : SingletonScriptableObject<ClusterDisplayNetworkManager>, ISerializationCallbackReceiver 
    {
        [SerializeField] private RPCRegistry rpcRegistry;
        [SerializeField] private ObjectRegistry objRegistry;

        public RPCRegistry RPCRegistry
        {
            get
            {
                if (rpcRegistry == null)
                    throw new System.Exception($"Missing instance of \"{nameof(RPCRegistry)}\".");
                return rpcRegistry;
            }
        }
        public ObjectRegistry ObjectRegistry
        {
            get
            {
                if (objRegistry == null)
                    throw new System.Exception($"Missing instance of \"{nameof(ObjectRegistry)}\".");
                return objRegistry;
            }
        }

        public void OnAfterDeserialize()
        {
        }

        public void OnBeforeSerialize()
        {
        }

        private void Awake()
        {
            DontDestroyOnLoad(this);
        }
    }
}
