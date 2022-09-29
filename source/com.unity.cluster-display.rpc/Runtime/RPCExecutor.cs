using System.Linq;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Unity.ClusterDisplay.RPC
{
    #if UNITY_EDITOR
    [InitializeOnLoad]
    #endif
    /// <summary>
    /// The purpose of this class is to manage and insert the player loop and execute queued 
    /// RPCs sent by the emitter node. When an RPC's RPCExecutionStage is set to Automatic,
    /// we get the current RPCExecutionStage + 1 within the frame from this class and embed
    /// into the RPC's header.
    /// </summary>
    internal static class RPCExecutor
    {
        #if UNITY_EDITOR
        static RPCExecutor () => SetupDelegates();
        #endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void SetupDelegates ()
        {
            ClusterDebug.Log("Setting up initialization delegates for RPC executor.");

            ClusterSync.onPostEnableClusterDisplay -= TrySetup;
            ClusterSync.onPostEnableClusterDisplay += TrySetup;

            ClusterSync.onDisableCLusterDisplay -= RemovePlayerLoops;
            ClusterSync.onDisableCLusterDisplay += RemovePlayerLoops;
        }

        public static RPCExecutionStage CurrentExecutionStage => m_CurrentExecutionStage;
        /// <summary>
        /// Throughout the frame, this is modified depending on the stage we are in: FixedUpdate, Update or LateUpdate.
        /// </summary>
        static RPCExecutionStage m_CurrentExecutionStage = RPCExecutionStage.AfterInitialization;

        public class RPCQueue {}

        public class BeforeFixedUpdateRPCQueue : RPCQueue {}
        public class AfterFixedUpdateRPCQueue : RPCQueue {}
        public class BeforeUpdateRPCQueue : RPCQueue {}
        public class AfterUpdateRPCQueue : RPCQueue {}
        public class BeforeLateUpdateRPCQueue : RPCQueue {}
        public class AfterLateUpdateRPCQueue : RPCQueue {}

        /// <summary>
        /// Inserts our callbacks throughout the player loop.
        /// </summary>
        /// <returns></returns>
        public static void TrySetup ()
        {
            PlayerLoopExtensions.InsertBefore<BeforeFixedUpdateRPCQueue, FixedUpdate>(BeforeFixedUpdate);
            PlayerLoopExtensions.InsertAfter<AfterFixedUpdateRPCQueue, FixedUpdate>(AfterFixedUpdate);
            PlayerLoopExtensions.InsertBefore<BeforeUpdateRPCQueue, Update>(BeforeUpdate);
            PlayerLoopExtensions.InsertAfter<AfterUpdateRPCQueue, Update>(AfterUpdate);
            PlayerLoopExtensions.InsertBefore<BeforeLateUpdateRPCQueue, PreLateUpdate>(BeforeLateUpdate);
            PlayerLoopExtensions.InsertAfter<AfterLateUpdateRPCQueue, PostLateUpdate>(AfterLateUpdate);
        }

        public static void RemovePlayerLoops() => PlayerLoopExtensions.RemovePlayerLoops<RPCQueue>();

        public static void BeforeFixedUpdate ()
        {
            m_CurrentExecutionStage = RPCExecutionStage.BeforeFixedUpdate;
            RPCInterfaceRegistry.BeforeFixedUpdate();
        }

        public static void AfterFixedUpdate ()
        {
            m_CurrentExecutionStage = RPCExecutionStage.AfterFixedUpdate;
            RPCInterfaceRegistry.AfterFixedUpdate();
        }

        public static void BeforeUpdate ()
        {
            m_CurrentExecutionStage = RPCExecutionStage.BeforeUpdate;
            RPCInterfaceRegistry.BeforeUpdate();
        }

        public static void AfterUpdate ()
        {
            m_CurrentExecutionStage = RPCExecutionStage.AfterUpdate;
            RPCInterfaceRegistry.AfterUpdate();
        }

        public static void BeforeLateUpdate ()
        {
            m_CurrentExecutionStage = RPCExecutionStage.BeforeLateUpdate;
            RPCInterfaceRegistry.BeforeLateUpdate();
        }

        public static void AfterLateUpdate ()
        {
            m_CurrentExecutionStage = RPCExecutionStage.AfterLateUpdate;
            RPCInterfaceRegistry.AfterLateUpdate();
        }
    }
}
