using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.ClusterDisplay.RPC
{
    [System.Serializable]
    /// <summary>
    /// This enum is used to define and RPC's execution stage either implicitly
    /// or explicitly. When an RPC's execution stage is set to RPCExecutionStage.Automatic, the 
    /// RPC will get executed by slave nodes in the NEXT execution stage that it's called by the
    /// since the RPC is essentially the result of that stage. However, when you explicitly set
    /// the execution stage to something else besides RPCExecutionStage.Automatic, this behaviour
    /// is disabled and slaves will execute the RPC in the stage you've defined.
    /// </summary>
    public enum RPCExecutionStage : int
    {

        /// <summary>
        /// When the method is executed by master, RPCEmitter will determine
        /// which execution stage it currently is and send the RPC with that
        /// RPC execution stage.
        /// </summary>
        Automatic = 0,

        /// <summary>
        /// Execute immediately on receipt, this could potentially be executed
        /// before Awake, Start or OnEnable if the RPC is sent on the first frame.
        /// </summary>
        ImmediatelyOnArrival = 1,

        /// <summary>
        /// RPCs explicitly marked with this execution stage will automatically 
        /// get executed in the "BeforeFixedUpdateQueue". Futhermore, When the RPCExecutionStage
        /// is set to RPCExecutionStage.automatic and this RPC gets executed in Awake, OnEnable 
        /// or Start, the RPCExecutionStage is added by 1 to become the next stage which
        ///  in this case is RPCExecutionSTage.BeforeFixedUpdate.
        /// </summary>
        AfterInitialization = 2,

        BeforeFixedUpdate = 3,
        AfterFixedUpdate = 4,

        BeforeUpdate = 5,
        AfterUpdate = 6,

        BeforeLateUpdate = 7,
        AfterLateUpdate = 8
    }
}
