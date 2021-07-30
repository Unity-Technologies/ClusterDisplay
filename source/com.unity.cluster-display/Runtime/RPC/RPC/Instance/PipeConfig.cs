using UnityEngine;

namespace Unity.ClusterDisplay
{
    [System.Serializable]
    public struct PipeConfig
    {
        [SerializeField] public RPCConfig[] configs;
    }
}
