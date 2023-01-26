using System;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace Unity.LiveEditing.LowLevel
{
    public static class MemoryExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe ref T AsRef<T>(in T source) where T : unmanaged
        {
            fixed (void* p = &source)
            {
                return ref *(T*) p;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ReadOnlySpan<T> CreateReadOnlySpan<T>(in T value, int length = 1) where T : unmanaged =>
            MemoryMarshal.CreateReadOnlySpan(ref AsRef(in value), length);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ReadOnlySpan<byte> AsBytes<T>(this ReadOnlySpan<T> span) where T : unmanaged =>
            MemoryMarshal.AsBytes(span);
    }
}
