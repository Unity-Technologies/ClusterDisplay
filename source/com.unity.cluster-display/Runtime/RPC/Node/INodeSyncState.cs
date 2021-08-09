using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    public interface IRepeaterNodeSyncState : INodeSyncState
    {
        UInt64 EmitterNodeIdMask { get; }
        void OnUnhandledNetworkMessage(MessageHeader msgHeader);
        void OnNonMatchingFrame(byte originID, ulong frameNumber);
        void OnPumpedMsg();
        void OnPublishingMsg();
    }

    public interface INodeSyncState
    {
        UDPAgent NetworkAgent { get; }
    }

    public interface IEmitterNodeSyncState : INodeSyncState
    {
    }
}
