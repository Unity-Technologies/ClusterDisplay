using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;

namespace Unity.ClusterDisplay
{
    public static class RPCExecutor
    {
        public static RPCExecutionStage CurrentExecutionStage => m_CurrentExecutionStage;
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
                Debug.LogError($"Cannot insert RPC executor of type: \"{playerLoopSystemToInsertInto.type.FullName}, unable to find insertion point BEFORE player loop system of type: {beforePlayerLoopSystem.FullName}");
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
                Debug.LogError($"Cannot insert RPC executor of type: \"{playerLoopSystemToInsertInto.type.FullName}, unable to find insertion point AFTER player loop system of type: {beforePlayerLoopSystem.FullName}");
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
            list.Insert(indexToInsertPlayerLoop, playerLoopToInsert);
            playerLoopSystemToInsertInto.subSystemList = list.ToArray();
            Debug.Log($"Inserted: \"{playerLoopToInsert.type.FullName} into current player loop.");
            return true;
        }

        public static bool TrySetup ()
        {
            var defaultPlayerSystemLoop = PlayerLoop.GetCurrentPlayerLoop();

            if (!TryInsertBefore(
                ref defaultPlayerSystemLoop,
                typeof(BeforeFixedUpdateType),
                typeof(FixedUpdate),
                BeforeFixedUpdate))
            {
                Debug.Log($"Unable to player loop of type: \"{nameof(BeforeFixedUpdateType)} before: \"{nameof(FixedUpdate)}");
                return false;
            }

            if (!TryInsertAfter(
                ref defaultPlayerSystemLoop,
                typeof(AfterFixedUpdateType),
                typeof(FixedUpdate),
                AfterFixedUpdate))
            {
                Debug.Log($"Unable to player loop of type: \"{nameof(AfterFixedUpdateType)} before: \"{nameof(FixedUpdate)}");
                return false;
            }

            if (!TryInsertBefore(
                ref defaultPlayerSystemLoop,
                typeof(BeforeUpdateType),
                typeof(Update),
                BeforeUpdate))
            {
                Debug.Log($"Unable to player loop of type: \"{nameof(BeforeUpdateType)} before: \"{nameof(Update)}");
                return false;
            }

            if (!TryInsertAfter(
                ref defaultPlayerSystemLoop,
                typeof(AfterUpdateType),
                typeof(Update),
                AfterUpdate))
            {
                Debug.Log($"Unable to player loop of type: \"{nameof(AfterUpdateType)} before: \"{nameof(Update)}");
                return false;
            }

            if (!TryInsertBefore(
                ref defaultPlayerSystemLoop,
                typeof(BeforeLateUpdateType),
                typeof(PreLateUpdate),
                BeforeLateUpdate))
            {
                Debug.Log($"Unable to player loop of type: \"{nameof(BeforeLateUpdate)} before: \"{nameof(PreLateUpdate)}");
                return false;
            }

            if (!TryInsertAfter(
                ref defaultPlayerSystemLoop,
                typeof(AfterLateUpdateType),
                typeof(PostLateUpdate),
                AfterLateUpdate))
            {
                Debug.Log($"Unable to player loop of type: \"{nameof(AfterLateUpdateType)} before: \"{nameof(PostLateUpdate)}");
                return false;
            }

            PlayerLoop.SetPlayerLoop(defaultPlayerSystemLoop);
            return true;
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
            // Debug.Log("BeforeFixedUpdate");
            RPCInterfaceRegistry.BeforeFixedUpdate();
        }

        public static void AfterFixedUpdate ()
        {
            m_CurrentExecutionStage = RPCExecutionStage.AfterFixedUpdate;
            // Debug.Log("AfterFixedUpdate");
            RPCInterfaceRegistry.AfterFixedUpdate();
        }

        public static void BeforeUpdate ()
        {
            m_CurrentExecutionStage = RPCExecutionStage.BeforeUpdate;
            // Debug.Log("BeforeUpdate");
            RPCInterfaceRegistry.BeforeUpdate();
        }

        public static void AfterUpdate ()
        {
            m_CurrentExecutionStage = RPCExecutionStage.AfterUpdate;
            // Debug.Log("AfterUpdate");
            RPCInterfaceRegistry.AfterUpdate();
        }

        public static void BeforeLateUpdate ()
        {
            m_CurrentExecutionStage = RPCExecutionStage.BeforeLateUpdate;
            // Debug.Log("BeforeLateUpdate");
            RPCInterfaceRegistry.BeforeLateUpdate();
        }

        public static void AfterLateUpdate ()
        {
            m_CurrentExecutionStage = RPCExecutionStage.AfterLateUpdate;
            // Debug.Log("AfterLateUpdate");
            RPCInterfaceRegistry.AfterLateUpdate();
        }
    }
}
