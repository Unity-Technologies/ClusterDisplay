using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.Compilation;

namespace Unity.ClusterDisplay
{
    [CreateAssetMenu(fileName = "ClusterDisplayNetworkManager", menuName = "Cluster Display/Cluster Display Network Manager", order = 1)]
    public class ClusterDisplayNetworkManager : SingletonScriptableObject<ClusterDisplayNetworkManager>, ISerializationCallbackReceiver 
    {
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
