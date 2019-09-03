using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Unity.ClusterRendering.SlaveStateMachine
{
    // SlaveState -------------------------------------------------------- 
    internal abstract class SlaveState : BaseState
    {
        protected SlavedNode m_Node;

        protected SlaveState(SlavedNode node)
        {
            m_Node = node;
        }

        public virtual SlaveState EnterState(SlaveState currentState)
        {
            base.EnterState(currentState);

            if (currentState != null && m_Node == null)
                m_Node = currentState.m_Node;

            return this;
        }

        public virtual void ExitState()
        {
        }

        public virtual bool ReadToProceedWithFrame => true;
    }

}