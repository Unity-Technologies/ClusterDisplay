using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace Unity.ClusterDisplay.Tests
{
    static class TestUtils
    {
        public static IEnumerator ToCoroutine(this Task task, float timeoutSeconds)
        {
            var elapsed = 0f;
            while (!task.IsCompleted && elapsed < timeoutSeconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            Assert.True(task.IsCompleted);
            Assert.DoesNotThrow(task.Wait);
        }

        public static CoroutineTask<T> ToCoroutine<T>(this ValueTask<T> task) => new(task);
        
        public static bool LoopUntil(Func<bool> pred, int maxRetries)
        {
            for (int i = 0; i < maxRetries; i++)
            {
                if (pred())
                {
                    return true;
                }
                Thread.Sleep(100);
            }

            return false;
        }
    }

    /// <summary>
    /// Class for running an async Task<T> as a coroutine and keeping track of the
    /// return value. 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    class CoroutineTask<T>
    {
        public T Result =>
            m_Task.IsCompletedSuccessfully
                ? m_Task.Result
                : throw new InvalidOperationException("Task is was not complete, or it was faulted");
        
        ValueTask<T> m_Task;

        public bool IsSuccessful => m_Task.IsCompletedSuccessfully;

        public CoroutineTask(ValueTask<T> task) => m_Task = task;

        public IEnumerator WaitForCompletion(float timeoutSeconds)
        {
            var elapsed = 0f;
            while (!m_Task.IsCompleted && elapsed < timeoutSeconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
        }
    }
}
