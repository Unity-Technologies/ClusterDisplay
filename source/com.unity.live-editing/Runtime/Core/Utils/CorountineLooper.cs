using System;
using System.Collections;
using UnityEngine;

namespace Unity.LiveEditing.LowLevel
{
    class CoroutineRunner : MonoBehaviour
    {
        static CoroutineRunner s_Instance;

        public static CoroutineRunner Instance
        {
            get
            {
                if (s_Instance == null)
                {
                    s_Instance = FindObjectsOfType<CoroutineRunner>() is { Length: > 0 } existing
                        ? existing[0]
                        : new GameObject($"{nameof(CoroutineRunner)}_Singleton").AddComponent<CoroutineRunner>();
                }

                return s_Instance;
            }
        }
    }

    /// <summary>
    /// Looper that raises Update events from the main loop using a coroutine.
    /// </summary>
    sealed class CoroutineLooper : ILooper, IDisposable
    {
        public Action Update { get; set; }
        IEnumerator m_Waiter;
        Coroutine m_Coroutine;

        /// <summary>
        /// Creates a new <see cref="CoroutineLooper"/>.
        /// </summary>
        /// <param name="waiter">Something to await between each Update.</param>
        public CoroutineLooper(IEnumerator waiter)
        {
            m_Waiter = waiter;
            m_Coroutine = CoroutineRunner.Instance.StartCoroutine(DoLoop());
        }

        /// <summary>
        /// Creates a new <see cref="CoroutineLooper"/>.
        /// </summary>
        /// <param name="yieldInstruction">
        /// A yield instruction recognized by Unity, which is awaited
        /// between each update.
        /// </param>
        public CoroutineLooper(object yieldInstruction = null)
        {
            IEnumerator Wait()
            {
                yield return yieldInstruction;
            }

            m_Waiter = Wait();
            m_Coroutine = CoroutineRunner.Instance.StartCoroutine(DoLoop());
        }

        IEnumerator DoLoop()
        {
            while (true)
            {
                Update?.Invoke();
                yield return m_Waiter;
            }

            // ReSharper disable once IteratorNeverReturns
        }

        public void Dispose()
        {
            CoroutineRunner.Instance.StopCoroutine(m_Coroutine);
        }
    }
}
