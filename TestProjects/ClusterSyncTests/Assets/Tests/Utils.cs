using System.Collections;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace Unity.ClusterDisplay.Tests
{
    static class Utils
    {
        public static IEnumerator TestAsyncTask(Task task, int timeoutSeconds)
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
    }
}
