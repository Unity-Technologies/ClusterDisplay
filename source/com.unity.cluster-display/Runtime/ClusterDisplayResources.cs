using UnityEngine;

namespace Unity.ClusterDisplay
{
    /// <summary>
    /// This scriptable object can be thought of as an (X)RP RenderPipelineAsset for configuration
    /// but for cluster display. There is a default instance of this scriptable object inside
    /// the cluster display package that automatically gets loaded when the ClusterRenderer
    /// class is initialized.
    /// </summary>
    [CreateAssetMenu(fileName = "Data", menuName = "Cluster Display/ClusterDisplayResources", order = 1)]
    public class ClusterDisplayResources : ScriptableObject
    {
        [System.Serializable]
        public struct PayloadLimits
        {
            public uint maxSingleRPCParameterByteSize;
            public uint maxSingleRPCByteSize;
            public uint maxRpcByteBufferSize;

            public uint maxFrameNetworkByteBufferSize;
            public uint maxMTUSize;
        }

        [SerializeField] private PayloadLimits m_PayloadLimits = new PayloadLimits
        {
            maxSingleRPCParameterByteSize = Constants.DefaultMaxSingleRPCParameterByteSize,
            maxSingleRPCByteSize = Constants.DefaultMaxSingleRPCByteSize,
            maxRpcByteBufferSize = Constants.DefaultMaxRpcByteBufferSize,

            maxFrameNetworkByteBufferSize = Constants.DefaultMaxFrameNetworkByteBufferSize,
            maxMTUSize = Constants.DefaultMaxMTUSize,
        };

        public PayloadLimits NetworkPayloadLimits => m_PayloadLimits;

        public uint MaxRpcByteBufferSize => m_PayloadLimits.maxRpcByteBufferSize;
        public uint MaxFrameNetworkByteBufferSize => m_PayloadLimits.maxFrameNetworkByteBufferSize;
        public uint MaxMTUSize => m_PayloadLimits.maxMTUSize;

        private void OnValidate()
        {
            if (m_PayloadLimits.maxRpcByteBufferSize > m_PayloadLimits.maxFrameNetworkByteBufferSize)
            {
                Debug.LogError($"RPC byte buffer cannot be larger then the frame network byte buffer size.");
                m_PayloadLimits.maxRpcByteBufferSize = m_PayloadLimits.maxFrameNetworkByteBufferSize;
            }

            if (m_PayloadLimits.maxMTUSize > ushort.MaxValue)
            {
                Debug.LogErrorFormat($"Maximum size of UDP packet is: {ushort.MaxValue} bytes.");
                m_PayloadLimits.maxMTUSize = ushort.MaxValue;
            }
        }
    }
}
