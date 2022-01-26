using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;

namespace Unity.ClusterDisplay.MissionControl
{
    public static class TaskExtensions
    {
        public static async Task WithCancellation(this Task task, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            using (cancellationToken.Register(s => ((TaskCompletionSource<bool>) s).TrySetResult(true), tcs))
            {
                if (task != await Task.WhenAny(task, tcs.Task))
                {
                    throw new OperationCanceledException(cancellationToken);
                }
            }

            await task;
        }

        public static async Task<T> WithCancellation<T>(this Task<T> task, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            using (cancellationToken.Register(s => ((TaskCompletionSource<bool>) s).TrySetResult(true), tcs))
            {
                if (task != await Task.WhenAny(task, tcs.Task))
                {
                    throw new OperationCanceledException(cancellationToken);
                }
            }

            return task.Result;
        }

        public static IEnumerator ToCoroutine(this Task task, Action<Exception> exceptionHandler = null)
        {
            var done = false;
            while (!done)
            {
                yield return null;
                try
                {
                    done = task.Wait(0);
                }
                catch (AggregateException ae)
                {
                    foreach (var e in ae.InnerExceptions)
                    {
                        exceptionHandler?.Invoke(e);
                    }

                    done = true;
                }
            }
        }
    }
}
