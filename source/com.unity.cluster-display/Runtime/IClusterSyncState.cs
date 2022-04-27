using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    interface IClusterSyncState
    {
        UInt64 CurrentFrameID { get; }
        ClusterNode LocalNode { get; }
    }
}
