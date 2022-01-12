using System;
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

        public static unsafe int WriteStruct<T>(this byte[] buffer, ref T data, int offset = 0) where T : struct
        {
            var size = Marshal.SizeOf<T>();

            fixed (byte* ptr = &buffer[offset])
            {
                Marshal.StructureToPtr(data, (IntPtr) ptr, false);
            }

            return offset + size;
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
    }
}
