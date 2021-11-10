using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.LowLevel;

namespace Unity.ClusterDisplay.Graphics
{
    /// <summary>
    /// A utility class letting us inject subsystems within the player loop.
    /// </summary>
    /// <remarks>
    /// Note that we need to provide a type for the subsystem we want to inject.
    /// </remarks>>
    /// <typeparam name="TParentSubsystem">Type of the parent subsystem.</typeparam>
    /// <typeparam name="TInjectedSubSystem">Type of the injected subsystem.</typeparam>
    static class PlayerLoopInjector<TParentSubsystem, TInjectedSubSystem>
    {
        static readonly string k_TypeName = nameof(PlayerLoopInjector<TParentSubsystem, TInjectedSubSystem>);
        
        /// <summary>
        /// An event invoked on update of the injected subsystem.
        /// </summary>
        public static event Action Update = delegate { };

        static bool s_Initialized;

        /// <summary>
        /// Injects the subsystem in the player loop.
        /// </summary>
        /// <remarks>
        /// Passing a negative insertion index will perform the injection starting at the end of the subsystems list.
        /// a -1 insertion index means the subsystem is added at the end of the list.
        /// </remarks>
        /// <param name="insertionIndex">The index at which we inject our subsystem within the parent subsystem.</param>
        /// <exception cref="InvalidOperationException">Thrown in case the subsystem was already injected.</exception>
        public static void Initialize(int insertionIndex)
        {
            if (s_Initialized)
            {
                throw new InvalidOperationException($"{k_TypeName} is already initialized.");
            }

            s_Initialized = true;

            var loop = PlayerLoop.GetCurrentPlayerLoop();
            var subSystemsList = loop.subSystemList.ToList();

            var parentIndex = subSystemsList.FindIndex(x => x.type == typeof(TParentSubsystem));
            Assert.IsFalse(parentIndex == -1);

            var systemList = loop.subSystemList[parentIndex].subSystemList.ToList();
            
            // Make sure we are not injecting twice.
            var preExistingIndex = systemList.FindIndex(x => x.type == typeof(TInjectedSubSystem));
            if (preExistingIndex != -1)
            {
                throw new InvalidOperationException($"{nameof(TInjectedSubSystem)} is already injected.");
            }
            
            // If insertionData.index is negative, insert from the end.
            var index = insertionIndex >= 0 ? insertionIndex : systemList.Count + 1 + insertionIndex;

            systemList.Insert(index, new PlayerLoopSystem
            {
                type = typeof(TInjectedSubSystem),
                updateDelegate = InvokeExecute
            });

            loop.subSystemList[parentIndex].subSystemList = systemList.ToArray();

            PlayerLoop.SetPlayerLoop(loop);
        }

        /// <summary>
        /// Removes the injected subsystem from the player loop.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown in case the subsystem was not injected.</exception>
        public static void Dispose()
        {
            if (!s_Initialized)
            {
                throw new InvalidOperationException($"Calling {nameof(Dispose)} but {k_TypeName} was not initialized.");
            }
            
            s_Initialized = false;

            var loop = PlayerLoop.GetCurrentPlayerLoop();
            var subSystemsList = loop.subSystemList.ToList();

            var parentIndex = subSystemsList.FindIndex(x => x.type == typeof(TParentSubsystem));
            Assert.IsFalse(parentIndex == -1);

            var systemList = loop.subSystemList[parentIndex].subSystemList.ToList();

            var index = systemList.FindIndex(x => x.type == typeof(TInjectedSubSystem));
            if (index == -1)
            {
                throw new InvalidOperationException($"{nameof(TInjectedSubSystem)} was not injected.");
            }
            
            systemList.RemoveAt(index);

            loop.subSystemList[parentIndex].subSystemList = systemList.ToArray();

            PlayerLoop.SetPlayerLoop(loop);
        }
        
        static void InvokeExecute() => Update.Invoke();
    }
}
