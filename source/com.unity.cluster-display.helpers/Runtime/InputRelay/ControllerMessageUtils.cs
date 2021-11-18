using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Unity.ClusterDisplay.Helpers
{
    internal static class ControllerMessageUtils
    {
        public static unsafe T BytesToValueType<T> (byte[] bytes, int offset) where T : struct
        {
            fixed (void* ptr = bytes)
                return Marshal.PtrToStructure<T>(new System.IntPtr(ptr) + offset);
        }

        public static unsafe byte[] ValueTypeToBytes<T> (T instance) where T : struct
        {
            var size = Marshal.SizeOf<T>();

            byte[] bytes = new byte[size];
            fixed (void * ptr = bytes)
                UnsafeUtility.MemCpy(
                    (byte*)ptr, 
                    UnsafeUtility.AddressOf(ref instance), 
                    size);

            return bytes;
        }

    }
}
