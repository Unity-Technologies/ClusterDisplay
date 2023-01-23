using System;
using System.Collections;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;

namespace Unity.ClusterDisplay.MissionControl
{
    public class EnumeratorTimeout
    {
        public EnumeratorTimeout(TimeSpan timeout)
        {
            Timeout = timeout;
        }

        public TimeSpan Timeout { get; }
        public bool TimedOut { get; set; }
    }

    public static class Helpers
    {
        /// <summary>
        /// Port used for services opening listen sockets.
        /// </summary>
        public const int ListenPort = 8010;

        public static IEnumerator AsIEnumerator<T>(this Task<T> task)
        {
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsFaulted)
            {
                ExceptionDispatchInfo.Capture(task.Exception!).Throw();
            }

            yield return null;
        }

        public static IEnumerator AsIEnumerator(this Task task)
        {
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsFaulted)
            {
                ExceptionDispatchInfo.Capture(task.Exception!).Throw();
            }

            yield return null;
        }

        public static IEnumerator AsIEnumeratorNoThrow(this Task task)
        {
            while (!task.IsCompleted)
            {
                yield return null;
            }

            yield return null;
        }

        public static IEnumerator AsIEnumerator(this Task task, EnumeratorTimeout timeout)
        {
            var stopwatch = Stopwatch.StartNew();
            while (!task.IsCompleted)
            {
                if (stopwatch.Elapsed > timeout.Timeout)
                {
                    timeout.TimedOut = true;
                    break;
                }
                yield return null;
            }

            if (task.IsFaulted)
            {
                ExceptionDispatchInfo.Capture(task.Exception!).Throw();
            }

            yield return null;
        }

        public static IEnumerator AsIEnumerator<T>(this ValueTask<T> task)
        {
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsFaulted)
            {
                ExceptionDispatchInfo.Capture(task.AsTask().Exception!).Throw();
            }

            yield return null;
        }

        public static IEnumerator AsIEnumerator(this ValueTask task)
        {
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsFaulted)
            {
                ExceptionDispatchInfo.Capture(task.AsTask().Exception!).Throw();
            }

            yield return null;
        }

        public static IEnumerator AsIEnumerator(this ValueTask task, EnumeratorTimeout timeout)
        {
            var stopwatch = Stopwatch.StartNew();
            while (!task.IsCompleted)
            {
                if (stopwatch.Elapsed > timeout.Timeout)
                {
                    timeout.TimedOut = true;
                    break;
                }
                yield return null;
            }

            if (task.IsFaulted)
            {
                ExceptionDispatchInfo.Capture(task.AsTask().Exception!).Throw();
            }

            yield return null;
        }
    }
}
