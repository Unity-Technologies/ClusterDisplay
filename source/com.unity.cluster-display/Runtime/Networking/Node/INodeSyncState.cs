using System;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Unity.ClusterDisplay.RPCs")]

namespace Unity.ClusterDisplay
{
    internal interface IRepeaterNodeSyncState : INodeSyncState
    {
        UInt64 EmitterNodeIdMask { get; }
        void OnUnhandledNetworkMessage(MessageHeader msgHeader);
        void OnNonMatchingFrame(byte originID, ulong frameNumber);
        void OnReceivedEmitterFrameData();
    }

    internal interface INodeSyncState
    {
        UDPAgent NetworkAgent { get; }
    }

    internal interface IEmitterNodeSyncState : INodeSyncState
    {
    }
}
