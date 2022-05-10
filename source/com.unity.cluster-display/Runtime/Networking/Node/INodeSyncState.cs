﻿using System;
using System.Runtime.CompilerServices;
using Unity.ClusterDisplay.Utils;

[assembly: InternalsVisibleTo("Unity.ClusterDisplay.RPCs")]

namespace Unity.ClusterDisplay
{
    internal interface IRepeaterNodeSyncState
    {
        BitVector EmitterNodeIdMask { get; }
        void OnUnhandledNetworkMessage(MessageHeader msgHeader);
        void OnNonMatchingFrame(byte originID, ulong frameNumber);
        void OnReceivedEmitterFrameData();
    }
}
