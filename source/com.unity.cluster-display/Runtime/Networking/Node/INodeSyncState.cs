using System;

namespace Unity.ClusterDisplay
{
    public interface IRepeaterNodeSyncState : INodeSyncState
    {
        UInt64 EmitterNodeIdMask { get; }
        void OnUnhandledNetworkMessage(MessageHeader msgHeader);
        void OnNonMatchingFrame(byte originID, ulong frameNumber);
        void OnReceivedEmitterFrameData();
    }

    public interface INodeSyncState
    {
        UDPAgent NetworkAgent { get; }
    }

    public interface IEmitterNodeSyncState : INodeSyncState
    {
    }
}
