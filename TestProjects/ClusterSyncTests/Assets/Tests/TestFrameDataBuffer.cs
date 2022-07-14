using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using RangeAttribute = NUnit.Framework.RangeAttribute;

namespace Unity.ClusterDisplay.Tests
{
    public class TestFrameDataBuffer
    {
        [Test]
        public void TestStoreWithDelegate()
        {
            if (!BitConverter.IsLittleEndian)
            {
                Assert.Ignore();
            }
            using var buffer = new FrameDataBuffer();
            buffer.Store(13, writeableBuffer =>
            {
                new byte[] {1, 2, 3, 4, 5}.CopyTo(writeableBuffer.AsSpan());
                return 5;
            });
            var byteBuffer13 = new byte[13];
            buffer.CopyTo(byteBuffer13);
            Assert.That(byteBuffer13, Is.EqualTo(new byte[] {13, 0, 0, 0, 5, 0, 0, 0, 1, 2, 3, 4, 5}));

            buffer.Store(14, writeableBuffer =>
            {
                new byte[] {6, 7, 8}.CopyTo(writeableBuffer.AsSpan());
                return 3;
            });
            var byteBuffer24 = new byte[24];
            buffer.CopyTo(byteBuffer24);
            Assert.That(byteBuffer24, Is.EqualTo(new byte[]
            {
                13, 0, 0, 0, 5, 0, 0, 0, 1, 2, 3, 4, 5,
                14, 0, 0, 0, 3, 0, 0, 0, 6, 7, 8
            }));

            // Case where callback does not write anything (returns 0)
            buffer.Store(15, _ => 0);
            Assert.That(buffer.Data(), Is.EqualTo(new byte[]
            {
                13, 0, 0, 0, 5, 0, 0, 0, 1, 2, 3, 4, 5,
                14, 0, 0, 0, 3, 0, 0, 0, 6, 7, 8,
                15, 0, 0, 0, 0, 0, 0, 0
            }));
        }

        [Test]
        public void TestStoreValue()
        {
            if (!BitConverter.IsLittleEndian)
            {
                Assert.Ignore();
            }
            using var buffer = new FrameDataBuffer();
            var value1 = 0x1ffe43a2;
            buffer.Store(12, ref value1);
            var byteBuffer = new byte[12];
            buffer.CopyTo(byteBuffer);
            Assert.That(byteBuffer, Is.EqualTo(new byte[] {12, 0, 0, 0, 4, 0, 0, 0, 0xa2, 0x43, 0xfe, 0x1f}));

            ushort value2 = 0xf5a4;
            buffer.Store(23, ref value2);
            Assert.That(buffer.Data(), Is.EquivalentTo(new byte[]
            {
                12, 0, 0, 0, 4, 0, 0, 0, 0xa2, 0x43, 0xfe, 0x1f,
                23, 0, 0, 0, 2, 0, 0, 0, 0xa4, 0xf5
            }));
        }

        [Test]
        public void TestStoreEmpty()
        {
            using var buffer = new FrameDataBuffer();
            var value = 0x1ffe43a2;
            buffer.Store(12, ref value);
            Assert.That(buffer.Length, Is.EqualTo(12));
            buffer.Store(6);
            Assert.That(buffer.Length, Is.EqualTo(20));
            Assert.That(buffer.Data().Skip(12), Is.EqualTo(new byte[]
            {
                6, 0, 0, 0, 0, 0, 0, 0
            }));
        }

        [Test]
        public void TestClear()
        {
            using var buffer = new FrameDataBuffer();
            var value = 0x1ffe43a2;
            buffer.Store(12, ref value);
            Assert.That(buffer.Length, Is.EqualTo(12));
            buffer.Clear();
            Assert.That(buffer.Length, Is.EqualTo(0));
        }

        [Test]
        public void TestEnumeration()
        {
            using var buffer = new FrameDataBuffer();
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

            // Test that we don't overflow if we omit the StateID.End entry
            using (var testCopy = new NativeArray<byte>(buffer.Length, Allocator.Temp))
            {
                buffer.CopyTo(testCopy.AsSpan());
                Assert.DoesNotThrow(() =>
                {
                    foreach (var item in new FrameDataReader(testCopy))
                    {
                        Debug.Log(item);
                    }
                });
            }

            buffer.Store((int) StateID.End);

            // Data added after StateID.End will not get enumerated
            buffer.Store(15, _ => 0);

            using (var testCopy = new NativeArray<byte>(buffer.Length, Allocator.Temp))
            {
                buffer.CopyTo(testCopy.AsSpan());

                // Although FrameDataReader supports foreach, it is not
                // an Enumerable. Read out values into a list we can examine them.
                var readData = new List<(int id, NativeArray<byte> data)>();
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

        [Test]
        public void TestFreezing()
        {
            using var buffer = new FrameDataBuffer();
            Assert.That(buffer.IsFrozen, Is.False);

            buffer.Store(28);
            Assert.That(buffer.Length, Is.EqualTo(8));

            ulong ulongValue = 0x6543210990123456;
            buffer.Store(42, ref ulongValue);
            Assert.That(buffer.Length, Is.EqualTo(24));

            buffer.Store(84, nativeArray =>
            {
                var newGuid = Guid.NewGuid();
                return newGuid.StoreInBuffer(nativeArray);
            });
            Assert.That(buffer.Length, Is.EqualTo(48));

            // Ask for the data (which will cause the FrameDataBuffer to freeze)
            buffer.Data();
            Assert.That(buffer.IsFrozen, Is.True);

            // Every store should now throw
            Assert.Throws<InvalidOperationException>(() => buffer.Store(28));
            Assert.Throws<InvalidOperationException>(() => buffer.Store(42, ref ulongValue));
            Assert.Throws<InvalidOperationException>(() => buffer.Store(84, nativeArray => 0));

            // Until the clear method is called
            buffer.Clear();
            Assert.That(buffer.IsFrozen, Is.False);
            Assert.That(buffer.Length, Is.Zero);

            buffer.Store(28);
            Assert.That(buffer.Length, Is.EqualTo(8));
        }

        [Test]
        public void TestGrowWithStoreId([Range(0,9)] int availableBytes)
        {
            using var buffer = new FrameDataBuffer();
            int initialCapacity = buffer.Capacity;

            var startFiller = Utilities.AllocRandomByteArray(buffer.Capacity - availableBytes - k_SmallestDataBlockSize);
            buffer.Store(28, nativeArray =>
            {
                NativeArray<byte>.Copy(startFiller, nativeArray, startFiller.Length);
                return startFiller.Length;
            });
            Assert.That(buffer.Capacity, Is.EqualTo(initialCapacity));

            buffer.Store(42);

            var dataBlocks = GetFrameDataBufferContent(buffer);
            Assert.That(dataBlocks.Count, Is.EqualTo(2));
            var (blockId0, blockData0) = dataBlocks[0];
            Assert.That(blockId0, Is.EqualTo(28));
            Assert.That(blockData0.ToArray(), Is.EqualTo(startFiller));
            var (blockId1, blockData1) = dataBlocks[1];
            Assert.That(blockId1, Is.EqualTo(42));
            Assert.That(blockData1, Is.Empty);

            if (availableBytes < k_SmallestDataBlockSize)
            {
                Assert.That(buffer.Capacity, Is.GreaterThan(initialCapacity));
            }
            else
            {
                Assert.That(buffer.Capacity, Is.EqualTo(initialCapacity));
            }
        }

        [Test]
        public void TestGrowWithStoreFixed([Range(0,20)] int availableBytes)
        {
            using var buffer = new FrameDataBuffer();
            int initialCapacity = buffer.Capacity;

            var startFiller = Utilities.AllocRandomByteArray(buffer.Capacity - availableBytes - k_SmallestDataBlockSize);
            buffer.Store(28, nativeArray =>
            {
                NativeArray<byte>.Copy(startFiller, nativeArray, startFiller.Length);
                return startFiller.Length;
            });
            Assert.That(buffer.Capacity, Is.EqualTo(initialCapacity));

            ulong valueToStore = 0x1234567887654321;
            buffer.Store(42, ref valueToStore);

            var dataBlocks = GetFrameDataBufferContent(buffer);
            Assert.That(dataBlocks.Count, Is.EqualTo(2));
            var (blockId0, blockData0) = dataBlocks[0];
            Assert.That(blockId0, Is.EqualTo(28));
            Assert.That(blockData0.ToArray(), Is.EqualTo(startFiller));
            var (blockId1, blockData1) = dataBlocks[1];
            Assert.That(blockId1, Is.EqualTo(42));
            Assert.That(blockData1.Length, Is.EqualTo(sizeof(ulong)));
            Assert.That(blockData1, Is.EqualTo(BitConverter.GetBytes(valueToStore)));

            if (availableBytes < k_SmallestDataBlockSize + sizeof(ulong))
            {
                Assert.That(buffer.Capacity, Is.GreaterThan(initialCapacity));
            }
            else
            {
                Assert.That(buffer.Capacity, Is.EqualTo(initialCapacity));
            }
        }

        [Test]
        public void TestGrowWithStoreCallback([Range(0,30)] int availableBytes)
        {
            using var buffer = new FrameDataBuffer();
            int initialCapacity = buffer.Capacity;

            var startFiller = Utilities.AllocRandomByteArray(buffer.Capacity - availableBytes - k_SmallestDataBlockSize);
            buffer.Store(28, nativeArray =>
            {
                NativeArray<byte>.Copy(startFiller, nativeArray, startFiller.Length);
                return startFiller.Length;
            });
            Assert.That(buffer.Capacity, Is.EqualTo(initialCapacity));

            var toStore = Guid.NewGuid();
            var arrayToStore = toStore.ToByteArray();
            buffer.Store(42, nativeArray =>
            {
                if (nativeArray.Length >= arrayToStore.Length)
                {
                    NativeArray<byte>.Copy(arrayToStore, nativeArray, arrayToStore.Length);
                    return arrayToStore.Length;
                }
                else
                {
                    return -1;
                }
            });

            var dataBlocks = GetFrameDataBufferContent(buffer);
            Assert.That(dataBlocks.Count, Is.EqualTo(2));
            var (blockId0, blockData0) = dataBlocks[0];
            Assert.That(blockId0, Is.EqualTo(28));
            Assert.That(blockData0.ToArray(), Is.EqualTo(startFiller));
            var (blockId1, blockData1) = dataBlocks[1];
            Assert.That(blockId1, Is.EqualTo(42));
            Assert.That(blockData1.Length, Is.EqualTo(arrayToStore.Length));
            Assert.That(blockData1, Is.EqualTo(arrayToStore));

            if (availableBytes < k_SmallestDataBlockSize + arrayToStore.Length)
            {
                Assert.That(buffer.Capacity, Is.GreaterThan(initialCapacity));
            }
            else
            {
                Assert.That(buffer.Capacity, Is.EqualTo(initialCapacity));
            }
        }

        List<(int, byte[])> GetFrameDataBufferContent(FrameDataBuffer frameDataBuffer)
        {
            using var storageArray = new NativeArray<byte>(frameDataBuffer.Length, Allocator.Temp);
            NativeArray<byte>.Copy(frameDataBuffer.Data(), storageArray);
            var ret = new List<(int, byte[])>();
            foreach (var (blockId, blockData) in new FrameDataReader(storageArray))
            {
                ret.Add((blockId, blockData.ToArray()));
            }
            return ret;
        }

        const int k_SmallestDataBlockSize = 8; // Two int, one for the block id and the other one for its length
    }
}
