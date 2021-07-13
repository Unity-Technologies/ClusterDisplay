using Type = System.Type;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.ClusterDisplay.RPC
{
    public abstract class ComponentReflector<T> : ComponentReflectorBase, IRPCStatus, ISerializationCallbackReceiver where T : Component
    {
        [SerializeField] protected T m_TargetInstance;
        [SerializeField] protected Type m_TargetType;

        public T TargetInstance => m_TargetInstance;

        private RPCStateManager m_RPCStateManager = new RPCStateManager();

        protected virtual void OnCache() {}
        private void Cache ()
        {
            OnCache();
        }

        public void Setup (T instance)
        {
            m_TargetInstance = instance;
            m_TargetType = m_TargetInstance.GetType();
        }

        private void OnValidate()
        {
            Cache();
        }

        public void OnAfterDeserialize() {}
        public void OnBeforeSerialize() {}

        public bool GetStatus(ushort rpcId)
        {
            return false;
        }

        public void SetStatus(ushort rpcId)
        {
        }
    }
}
