using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    partial class ClusterSync
    {
        public interface IStateAccessor
        {
            public bool IsEmitter { get; }
            public bool EmitterIsHeadless { get; }
            public bool IsRepeater { get; }
            public bool IsClusterLogicEnabled { get; }
            public bool IsActive { get; }
            public bool IsTerminated { get; }
            public ulong Frame { get; }
            public ushort NodeID { get; }
        }

        private class SyncState : IStateAccessor
        {
            private bool m_IsEmitter;
            private bool m_EmitterIsHeadless;
            private bool m_IsRepeater;
            private bool m_IsClusterLogicEnabled;
            private bool m_IsActive;
            private bool m_IsTerminated;
            private ulong m_Frame;
            private ushort m_NodeID;

            public bool IsEmitter => m_IsEmitter;
            public bool EmitterIsHeadless => !Application.isEditor && m_EmitterIsHeadless;
            public bool IsRepeater => m_IsRepeater;
            public bool IsClusterLogicEnabled => m_IsClusterLogicEnabled;
            public bool IsActive => m_IsActive;
            public bool IsTerminated => m_IsTerminated;
            public ulong Frame => m_Frame;
            public ushort NodeID => m_NodeID;

            public void SetIsActive(bool isActive) => m_IsActive = isActive;
            public void SetClusterLogicEnabled(bool clusterLogicEnabled) => m_IsClusterLogicEnabled = clusterLogicEnabled;
            public void SetIsEmitter(bool isEmitter) => m_IsEmitter = isEmitter;
            public void SetEmitterIsHeadless(bool headlessEmitter) => m_EmitterIsHeadless = headlessEmitter;
            public void SetIsRepeater(bool isRepeater) => m_IsRepeater = isRepeater;
            public void SetIsTerminated(bool isTerminated) => m_IsTerminated = isTerminated;
            public void SetFrame(ulong frame) => m_Frame = frame;
            public void SetNodeID(ushort nodeId) => m_NodeID = nodeId;
        }

        SyncState m_State = new();

        public IStateAccessor StateAccessor => m_State;
    }
}
