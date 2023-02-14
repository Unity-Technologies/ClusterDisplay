using System;
using System.Collections.Generic;
using Unity.ClusterDisplay.MissionControl.Capsule;

namespace Unity.ClusterDisplay.Tests
{
    public class ClusterSyncStub: IClusterSyncState
    {
        public string InstanceName { get; } = "";
        public NodeRole NodeRole { get; set; } = NodeRole.Unassigned;
        public bool EmitterIsHeadless { get; } = false;
        public bool IsClusterLogicEnabled { get; } = true;
        public bool IsTerminated { get; } = false;
        public ulong Frame { get; } = 0;
        public byte NodeID { get; set; } = 0;
        public byte RenderNodeID { get; set; } = 0;
        public bool RepeatersDelayedOneFrame { get; } = false;

        public IReadOnlyList<ChangeClusterTopologyEntry> UpdatedClusterTopology
        {
            get => m_UpdatedClusterTopology;
            set
            {
                m_UpdatedClusterTopology = value;
                UpdatedClusterTopologyChanged?.Invoke();
            }
        }
        IReadOnlyList<ChangeClusterTopologyEntry> m_UpdatedClusterTopology;
        public event Action UpdatedClusterTopologyChanged;

        public string GetDiagnostics()
        {
            throw new NotImplementedException();
        }
    }
}
