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
            await using (cancellationToken.Register(s => ((TaskCompletionSource<bool>) s).TrySetResult(true), tcs))
            {
                if (task != await Task.WhenAny(task, tcs.Task))
                {
                    throw new OperationCanceledException(cancellationToken);
                }
            }

            return task.Result;
        }

        /// <summary>
        /// Explicitly handle exceptions from a task.
        /// </summary>
        /// <param name="task"></param>
        /// <param name="exceptionHandler"></param>
        /// <returns></returns>
        /// <remarks>
        /// This is useful when you have a long-running task and the synchronization context is
        /// swallowing exceptions (e.g. Unity).
        /// </remarks>
        public static async Task<bool> WithErrorHandling(this Task task, Action<Exception> exceptionHandler)
        {
            try
            {
                await task;
                return task.IsCompletedSuccessfully;
            }
            catch (AggregateException ae)
            {
                foreach (var exception in ae.InnerExceptions)
                {
                    exceptionHandler?.Invoke(exception);
                }
            }
            catch (Exception e)
            {
                exceptionHandler?.Invoke(e);
            }

            return false;
        }
    }
}
