using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.ClusterDisplay.Tests
{
    public class TestFrameDataBuffer
    {
        [Test]
        public void TestStoreWithDelegate()
        {
            using var buffer = new FrameDataBuffer(1024);
            buffer.Store(13, writeableBuffer =>
            {
                new byte[] {1, 2, 3, 4, 5}.CopyTo(writeableBuffer.AsSpan());
                return 5;
            });
            Assert.That(buffer.Data, Is.EqualTo(new byte[] {13, 5, 0, 0, 0, 1, 2, 3, 4, 5}));
            buffer.Store(14, writeableBuffer =>
            {
                new byte[] {6, 7, 8}.CopyTo(writeableBuffer.AsSpan());
                return 3;
            });

            Assert.That(buffer.Data, Is.EqualTo(new byte[]
            {
                13, 5, 0, 0, 0, 1, 2, 3, 4, 5,
                14, 3, 0, 0, 0, 6, 7, 8
            }));
            buffer.Store((byte) StateID.End);

            using var copy = new NativeArray<byte>(buffer.Length, Allocator.Temp);
            buffer.CopyTo(copy.AsSpan());
            foreach (var (id, data) in new FrameDataReader(copy))
            {
                Debug.Log($"{id}: {data.Length}");
            }
        }

        [Test]
        public void TestStoreValue()
        {
            if (!BitConverter.IsLittleEndian)
            {
                Assert.Ignore();
            }
            using var buffer = new FrameDataBuffer(1024);
            var value1 = 0x1ffe43a2;
            buffer.Store(12, ref value1);
            Assert.That(buffer.Data, Is.EqualTo(new byte[] {12, 4, 0, 0, 0, 0xa2, 0x43, 0xfe, 0x1f}));

            ushort value2 = 0xf5a4;
            buffer.Store(23, ref value2);
            Assert.That(buffer.Data, Is.EquivalentTo(new byte[]
            {
                12, 4, 0, 0, 0, 0xa2, 0x43, 0xfe, 0x1f,
                23, 2, 0, 0, 0, 0xa4, 0xf5
            }));
        }

        [Test]
        public void TestStoreEmpty()
        {
            using var buffer = new FrameDataBuffer(1024);
            var value = 0x1ffe43a2;
            buffer.Store(12, ref value);
            Assert.That(buffer.Length, Is.EqualTo(9));
            buffer.Store(6);
            Assert.That(buffer.Length, Is.EqualTo(14));
            Assert.That(buffer.Data.Skip(9), Is.EqualTo(new byte[]
            {
                6, 0, 0, 0, 0
            }));
        }

        [Test]
        public void TestClear()
        {
            using var buffer = new FrameDataBuffer(1024);
            var value = 0x1ffe43a2;
            buffer.Store(12, ref value);
            Assert.That(buffer.Length, Is.EqualTo(9));
            buffer.Clear();
            Assert.That(buffer.Length, Is.EqualTo(0));
        }

        [Test]
        public void TestEnumeration()
        {
            using var buffer = new FrameDataBuffer(1024);
            buffer.Store(13, writeableBuffer =>
            {
                new byte[] {1, 2, 3, 4, 5}.CopyTo(writeableBuffer.AsSpan());
                return 5;
            });
            buffer.Store(14, writeableBuffer =>
            {
                new byte[] {6, 7, 8}.CopyTo(writeableBuffer.AsSpan());
                return 3;
            });

            buffer.Store((byte) StateID.End);

            using var testCopy = new NativeArray<byte>(buffer.Length, Allocator.Temp);
            buffer.CopyTo(testCopy.AsSpan());

            var readData = new List<(byte id, NativeArray<byte> data)>();
            foreach (var item in new FrameDataReader(testCopy))
            {
                readData.Add(item);
            }

            Assert.That(readData, Has.Count.EqualTo(2));
            Assert.That(readData[0].id, Is.EqualTo(13));
            Assert.That(readData[0].data, Is.EqualTo(new byte[] {1, 2, 3, 4, 5}));
            Assert.That(readData[1].id, Is.EqualTo(14));
            Assert.That(readData[1].data, Is.EqualTo(new byte[] {6, 7, 8}));
        }
    }
}
