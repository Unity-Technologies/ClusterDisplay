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
        private static void SetupDelegates ()
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
        private static RPCExecutionStage m_CurrentExecutionStage = RPCExecutionStage.AfterInitialization;

        public class BeforeFixedUpdateType {}
        public class AfterFixedUpdateType {}
        public class BeforeUpdateType {}
        public class AfterUpdateType {}
        public class BeforeLateUpdateType {}
        public class AfterLateUpdateType {}

        private static bool TryFindIndexOfPlayerLoopSystem (
            PlayerLoopSystem inPlayerSystemLoop, 
            System.Type targetPlayerSystemLoop,
            out int index)
        {
            index = -1;
            for (int i = 0; i < inPlayerSystemLoop.subSystemList.Length; i++)
            {
                if (targetPlayerSystemLoop != inPlayerSystemLoop.subSystemList[i].type)
                    continue;

                index = i;
                break;
            }

            return index != -1;
        }

        private static bool TryInsertBefore (
            ref PlayerLoopSystem playerLoopSystemToInsertInto, 
            System.Type playerLoopToInsert, 
            System.Type beforePlayerLoopSystem, 
            PlayerLoopSystem.UpdateFunction updateDelegate)
        {
            if (!TryFindIndexOfPlayerLoopSystem(
                playerLoopSystemToInsertInto,
                beforePlayerLoopSystem,
                out var indexOfPlayerLoop))
            {
                ClusterDebug.LogError($"Cannot insert RPC executor of type: \"{playerLoopSystemToInsertInto.type.FullName}, unable to find insertion point BEFORE player loop system of type: {beforePlayerLoopSystem.FullName}");
                return false;
            }

            return TryInsertAt(
                ref playerLoopSystemToInsertInto, 
                new PlayerLoopSystem
                {
                    type = playerLoopToInsert,
                    updateDelegate = updateDelegate
                }, 
                indexOfPlayerLoop, 
                updateDelegate);
        }

        private static bool TryInsertAfter (
            ref PlayerLoopSystem playerLoopSystemToInsertInto, 
            System.Type playerLoopToInsert, 
            System.Type beforePlayerLoopSystem, 
            PlayerLoopSystem.UpdateFunction updateDelegate)
        {
            if (!TryFindIndexOfPlayerLoopSystem(
                playerLoopSystemToInsertInto,
                beforePlayerLoopSystem,
                out var indexOfPlayerLoop))
            {
                ClusterDebug.LogError($"Cannot insert RPC executor of type: \"{playerLoopSystemToInsertInto.type.FullName}, unable to find insertion point AFTER player loop system of type: {beforePlayerLoopSystem.FullName}");
                return false;
            }

            return TryInsertAt(
                ref playerLoopSystemToInsertInto, 
                new PlayerLoopSystem
                {
                    type = playerLoopToInsert,
                    updateDelegate = updateDelegate
                }, 
                indexOfPlayerLoop + 1, 
                updateDelegate);
        }

        private static bool TryInsertAt (
            ref PlayerLoopSystem playerLoopSystemToInsertInto,
            PlayerLoopSystem playerLoopToInsert, 
            int indexToInsertPlayerLoop, 
            PlayerLoopSystem.UpdateFunction updateDelegate)
        {
            var list = playerLoopSystemToInsertInto.subSystemList.ToList();
            if (list.Any(playerLoop => playerLoop.type == playerLoopToInsert.type))
                return true;
            
            list.Insert(indexToInsertPlayerLoop, playerLoopToInsert);
            playerLoopSystemToInsertInto.subSystemList = list.ToArray();
            ClusterDebug.Log($"Inserted: \"{playerLoopToInsert.type.FullName} into current player loop.");
            return true;
        }

        /// <summary>
        /// Inserts our callbacks throughout the player loop.
        /// </summary>
        /// <returns></returns>
        public static void TrySetup ()
        {
            var defaultPlayerSystemLoop = PlayerLoop.GetCurrentPlayerLoop();

            if (!TryInsertBefore(
                ref defaultPlayerSystemLoop,
                typeof(BeforeFixedUpdateType),
                typeof(FixedUpdate),
                BeforeFixedUpdate))
            {
                ClusterDebug.Log($"Unable to player loop of type: \"{nameof(BeforeFixedUpdateType)} before: \"{nameof(FixedUpdate)}");
                return;
            }

            if (!TryInsertAfter(
                ref defaultPlayerSystemLoop,
                typeof(AfterFixedUpdateType),
                typeof(FixedUpdate),
                AfterFixedUpdate))
            {
                ClusterDebug.Log($"Unable to player loop of type: \"{nameof(AfterFixedUpdateType)} before: \"{nameof(FixedUpdate)}");
                return;
            }

            if (!TryInsertBefore(
                ref defaultPlayerSystemLoop,
                typeof(BeforeUpdateType),
                typeof(Update),
                BeforeUpdate))
            {
                ClusterDebug.Log($"Unable to player loop of type: \"{nameof(BeforeUpdateType)} before: \"{nameof(Update)}");
                return;
            }

            if (!TryInsertAfter(
                ref defaultPlayerSystemLoop,
                typeof(AfterUpdateType),
                typeof(Update),
                AfterUpdate))
            {
                ClusterDebug.Log($"Unable to player loop of type: \"{nameof(AfterUpdateType)} before: \"{nameof(Update)}");
                return;
            }

            if (!TryInsertBefore(
                ref defaultPlayerSystemLoop,
                typeof(BeforeLateUpdateType),
                typeof(PreLateUpdate),
                BeforeLateUpdate))
            {
                ClusterDebug.Log($"Unable to player loop of type: \"{nameof(BeforeLateUpdate)} before: \"{nameof(PreLateUpdate)}");
                return;
            }

            if (!TryInsertAfter(
                ref defaultPlayerSystemLoop,
                typeof(AfterLateUpdateType),
                typeof(PostLateUpdate),
                AfterLateUpdate))
            {
                ClusterDebug.Log($"Unable to player loop of type: \"{nameof(AfterLateUpdateType)} before: \"{nameof(PostLateUpdate)}");
                return;
            }

            PlayerLoop.SetPlayerLoop(defaultPlayerSystemLoop);
        }

        public static void RemovePlayerLoops ()
        {
            var defaultPlayerSystemLoop = PlayerLoop.GetCurrentPlayerLoop();

            var list = defaultPlayerSystemLoop.subSystemList.ToList();
            var indexOf = list.FindIndex(playerLoopSystem => playerLoopSystem.type == typeof(BeforeFixedUpdateType));
            if (indexOf != -1)
                list.RemoveAt(indexOf);
            defaultPlayerSystemLoop.subSystemList = list.ToArray();

            PlayerLoop.SetPlayerLoop(defaultPlayerSystemLoop);
        }

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
