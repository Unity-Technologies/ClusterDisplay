using System;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using UnityEngine;

namespace Unity.ClusterDisplay.Utils
{
    /// <summary>
    /// A struct containing a 128-bit GUID that is compatible with Unity serialization.
    /// </summary>
    [Serializable]
    public class SerializableGuid : ISerializationCallbackReceiver
    {
        [SerializeField, HideInInspector]
        bool m_Serialized;

        [SerializeField]
        long m_Low;

        [SerializeField]
        long m_High;

        public SerializableGuid()
        {
            FromGuid(Guid.NewGuid());
        }

        public static implicit operator Guid(SerializableGuid guid)
        {
            Span<byte> bytes = stackalloc byte[16];
            BitConverter.TryWriteBytes(bytes[..8], guid.m_Low);
            BitConverter.TryWriteBytes(bytes[8..], guid.m_High);
            return new Guid(bytes);
        }

        public override string ToString() => ((Guid)this).ToString();

        public void FromGuid(Guid guid)
        {
            ReadOnlySpan<byte> bytes = guid.ToByteArray();
            m_Low = BitConverter.ToInt64(bytes[..8]);
            m_High = BitConverter.ToInt64(bytes[8..]);
        }

        public void OnBeforeSerialize()
        {
            // Hack: Generate a new GUID (instead of setting members to 0)
            // when a new instance is created in the Editor.

            if (m_Serialized) return;

            FromGuid(Guid.NewGuid());
            m_Serialized = true;
        }

        public void OnAfterDeserialize() { }
    }

    public static class GuidUtils
    {
        /// <summary>
        /// Get a UUID from a namespace and name.
        /// </summary>
        /// <param name="namespaceGuid">The namespace ID.</param>
        /// <param name="name">The name.</param>
        /// <returns></returns>
        /// <remarks>
        /// As per RFC-4122 (https://datatracker.ietf.org/doc/html/rfc4122#section-4.3),
        /// requirements for these types of UUIDs are as follows:
        /// <ul>
        /// <li>The UUIDs generated at different times from the same name in the
        ///    same namespace MUST be equal.</li>
        ///
        /// <li>The UUIDs generated from two different names in the same namespace
        ///    should be different (with very high probability).</li>
        ///
        /// <li>The UUIDs generated from the same name in two different namespaces
        ///    should be different with (very high probability).</li>
        ///
        /// <li>If two UUIDs that were generated from names are equal, then they
        ///    were generated from the same name in the same namespace (with very
        ///    high probability).</li>
        /// </ul>
        /// </remarks>
        public static Guid GetNameBasedGuid(Guid namespaceGuid, string name)
        {
            // .NET's byte ordering is really funky.
            // It's represented as int-short-short-[8 bytes]
            // so we need to reverse the bytes accordingly
            // see https://docs.microsoft.com/en-us/dotnet/api/system.guid.tobytearray
            static void ToNetworkByteOrder(Span<byte> bytes)
            {
                var intVal = IPAddress.HostToNetworkOrder(MemoryMarshal.Read<int>(bytes[..4]));
                MemoryMarshal.Write(bytes[..4], ref intVal);
                var shortVal = IPAddress.HostToNetworkOrder(MemoryMarshal.Read<short>(bytes[4..6]));
                MemoryMarshal.Write(bytes[4..6], ref shortVal);
                shortVal = IPAddress.HostToNetworkOrder(MemoryMarshal.Read<short>(bytes[6..8]));
                MemoryMarshal.Write(bytes[6..8], ref shortVal);
            }

            static void ToCrazyEndianOrder(Span<byte> bytes)
            {
                var intVal = IPAddress.NetworkToHostOrder(MemoryMarshal.Read<int>(bytes[..4]));
                MemoryMarshal.Write(bytes[..4], ref intVal);
                var shortVal = IPAddress.NetworkToHostOrder(MemoryMarshal.Read<short>(bytes[4..6]));
                MemoryMarshal.Write(bytes[4..6], ref shortVal);
                shortVal = IPAddress.NetworkToHostOrder(MemoryMarshal.Read<short>(bytes[6..8]));
                MemoryMarshal.Write(bytes[6..8], ref shortVal);
            }

            // Convert the name to a canonical sequence of octets (as defined by
            // the standards or conventions of its name space);
            var nameBytes = MemoryMarshal.Cast<char, byte>(name);

            // Allocate a UUID to use as a "name space ID" for all UUIDs
            // generated from names in that name space;
            Span<byte> allBytes = stackalloc byte[16 + nameBytes.Length];
            namespaceGuid.TryWriteBytes(allBytes);

            // put the name space ID in network byte order.
            ToNetworkByteOrder(allBytes[..16]);

            // concatenate the namespace bytes to the name bytes
            nameBytes.CopyTo(allBytes[16..]);

            // Choose either MD5 [4] or SHA-1 [8] as the hash algorithm; If
            // backward compatibility is not an issue, SHA-1 is preferred.
            using var sha1 = SHA1.Create();

            // Compute the hash of the name space ID concatenated with the name.
            Span<byte> hashBytes = stackalloc byte[sha1.HashSize / 8];
            sha1.TryComputeHash(allBytes, hashBytes, out _);

            // Final GUID result in network byte order
            Span<byte> guidBytes = stackalloc byte[16];

            // Merge the following steps into a single copy operation:
            // - Set octets zero through 3 of the time_low field (0-3) to octets zero through 3 of the hash.
            // - Set octets zero and one of the time_mid field (4-5) to octets 4 and 5 of the hash.
            // - Set octets zero and one of the time_hi_and_version (6-7) field to octets 6 and 7 of the hash.
            // - Set the clock_seq_hi_and_reserved field to octet 8 of the hash.
            // - Set the clock_seq_low field to octet 9 of the hash.
            // - Set octets zero through five of the node field to octets 10 through 15 of the hash.
            hashBytes[..16].CopyTo(guidBytes);

            // Set the four most significant bits (bits 12 through 15) of the
            // time_hi_and_version (octets 6-7) field to the appropriate 4-bit version number
            // from Section 4.1.3: version 5 [0101] (name-based version that uses SHA-1).
            guidBytes[6] = (byte)((guidBytes[6] & 0b0000_1111) | 0b0101_0000);

            // Set the two most significant bits (bits 6 and 7) of the clock_seq_hi_and_reserved (octet 8)
            // to zero and one, respectively.
            guidBytes[8] = (byte)((guidBytes[8] & 0b0011_1111) | 0b1000_0000);

            // Convert the resulting UUID to local byte order.
            ToCrazyEndianOrder(guidBytes);

            return new Guid(guidBytes);
        }
    }
}
