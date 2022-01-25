using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Unity.ClusterDisplay.MissionControl
{
    public static class BufferExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe T ReadStruct<T>(this byte[] buffer, int offset = 0) where T : struct
        {
            fixed (byte* ptr = &buffer[offset])
            {
                return Marshal.PtrToStructure<T>((IntPtr) ptr);
            }
        }

        public static unsafe int WriteStruct<T>(this byte[] buffer, in T data, int offset = 0) where T : struct
        {
            var size = Marshal.SizeOf<T>();

            fixed (byte* ptr = &buffer[offset])
            {
                Marshal.StructureToPtr(data, (IntPtr) ptr, false);
            }

            return offset + size;
        }

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

        public static async Task<T?> TakeAsync<T>(this BlockingCollection<T> collection, int timeoutMilliseconds, CancellationToken cancellationToken) where T : struct
        {
            return await Task.Run<T?>(() =>
            {
                if (collection.TryTake(out var item, timeoutMilliseconds, cancellationToken))
                {
                    return item;
                }
                return null;
            }, cancellationToken);
        }
        
        public static async Task<bool> AddAsync<T>(this BlockingCollection<T> collection, T item, int timeoutMilliseconds, CancellationToken cancellationToken) where T : struct
        {
            return await Task.Run(() => collection.TryAdd(item, timeoutMilliseconds, cancellationToken), cancellationToken);
        }
    }
}
