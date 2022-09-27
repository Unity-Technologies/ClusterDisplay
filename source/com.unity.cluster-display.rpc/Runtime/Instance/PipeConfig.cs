using UnityEngine;

namespace Unity.ClusterDisplay
{
    [System.Serializable]
    internal struct PipeConfig
    {
        [SerializeField] public RPCConfig[] configs;
    }
}
