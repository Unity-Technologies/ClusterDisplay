namespace Unity.ClusterRendering.MasterStateMachine
{
    internal abstract class MasterState : BaseState
    {
        protected MasterNode m_Node;

        protected MasterState(MasterNode node ) : base()
        {
            m_Node = node;
        }

        public virtual MasterState EnterState(MasterState currentState)
        {
            base.EnterState(currentState);

            if (currentState != null && m_Node == null)
                m_Node = currentState.m_Node;

            return this;
        }
    }
}

