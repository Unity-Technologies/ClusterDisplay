using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.LowLevel;

namespace Unity.ClusterDisplay.RPC
{
    static class PlayerLoopExtensions
    {
        static int GetIndexOf (
            PlayerLoopSystem playerLoopSystem, 
            System.Type target)
        {
            for (int i = 0; i < playerLoopSystem.subSystemList.Length; i++)
            {
                if (target != playerLoopSystem.subSystemList[i].type)
                {
                    continue;
                }

                return i;
            }

            throw new System.IndexOutOfRangeException($"There is no: \"{target.FullName}\" in the current player loop.");
        }

        public static void InsertBefore<TargetLoop, BeforeLoop> (
            PlayerLoopSystem.UpdateFunction updateDelegate)
        {
            var defaultPlayerLoop = PlayerLoop.GetCurrentPlayerLoop();
            var targetLoop = typeof(TargetLoop);
            var beforeLoop = typeof(BeforeLoop);

            InsertAt(
                ref defaultPlayerLoop,
                new PlayerLoopSystem
                {
                    type = targetLoop,
                    updateDelegate = updateDelegate
                },
                GetIndexOf(
                    defaultPlayerLoop,
                    beforeLoop));
        }

        public static void InsertAfter<TargetLoop, AfterLoop> (
            PlayerLoopSystem.UpdateFunction updateDelegate)
        {
            var defaultPlayerLoop = PlayerLoop.GetCurrentPlayerLoop();
            var targetLoop = typeof(TargetLoop);
            var afterLoop = typeof(AfterLoop);

            InsertAt(
                ref defaultPlayerLoop,
                new PlayerLoopSystem
                {
                    type = targetLoop,
                    updateDelegate = updateDelegate
                },
                GetIndexOf(
                    defaultPlayerLoop,
                    afterLoop) + 1);
        }

        static void InsertAt (
            ref PlayerLoopSystem intoSystem,
            PlayerLoopSystem targetLoop, 
            int insertIndex)
        {
            var list = intoSystem.subSystemList.ToList();
            if (list.Any(playerLoop => playerLoop.type == targetLoop.type))
            {
                throw new System.InvalidOperationException($"The loop: \"{targetLoop.type.FullName}\" already exists in the sub system.");
            }

            var after = list[insertIndex - 1];
            if (insertIndex + 1 < list.Count)
            {
                var before = list[insertIndex];
                ClusterDebug.Log($"Inserting: \"{targetLoop.type.FullName} into current player loop after: \"{after.type.FullName}\" and before: \"{before.type.FullName}\".");
            }

            else
            {
                ClusterDebug.Log($"Inserting: \"{targetLoop.type.FullName} into current player loop after: \"{after.type.FullName}\" and as the very last method.");
            }

            list.Insert(insertIndex, targetLoop);
            intoSystem.subSystemList = list.ToArray();
            PlayerLoop.SetPlayerLoop(intoSystem);
        }

        public static void RemovePlayerLoops<T> ()
        {
            var defaultPlayerSystemLoop = PlayerLoop.GetCurrentPlayerLoop();

            string msg = $"Removed the following {nameof(PlayerLoopSystem)}s from the current player loop:";
            defaultPlayerSystemLoop.subSystemList = defaultPlayerSystemLoop.subSystemList
                .Where(loop =>
                {
                    if (loop.type == typeof(T) || loop.type.IsSubclassOf(typeof(T)))
                    {
                        msg += $"\n\t\"{loop.type.FullName}\"";
                        return false;
                    }

                    return true;
                })
                .ToArray();

            PlayerLoop.SetPlayerLoop(defaultPlayerSystemLoop);
            ClusterDebug.Log(msg);
        }
    }
}
