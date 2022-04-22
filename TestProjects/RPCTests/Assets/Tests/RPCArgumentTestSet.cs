using NUnit.Framework;
using System.Runtime.InteropServices;

using UnityEngine;
using Unity.Collections;
using UnityEngine.TestTools;

using Unity.ClusterDisplay.RPC;

namespace Unity.ClusterDisplay.Tests
{
    public class ClusterSyncTests
    {
        [SetUp]
        public void SetUp()
        {
            RPCRegistry.Initialize();
            RPCBufferIO.Initialize(overrideCaptureExecution: true);
        }

        private void EmulateFlight ()
        {
            NativeArray<byte> buffer = new NativeArray<byte>(1024, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

            uint endPos = 0;
            RPCBufferIO.Latch(buffer, ref endPos);
            RPCBufferIO.Unlatch(buffer.GetSubArray(0, (int)endPos));

            buffer.Dispose();
        }

        [Test]
        public void StringTest()
        {
            StringTest("Hello, World!");
            EmulateFlight();
        }

        [Test]
        public void FloatTest()
        {
            FloatTest(1.4f);
            EmulateFlight();
        }

        [Test]
        public void MultiStringTest()
        {
            MultiStringTest("Hello", "World");
            EmulateFlight();
        }

        [Test]
        public void PrimitivesTest()
        {
            PrimitivesTest(
                byteValue: 128,
                sbyteValue: -128,
                booleanValue: true,
                charValue: '@',
                stringValue: "Hello, World!",
                ushortValue: 12345,
                shortValue: -12345,
                uintValue: 123456,
                intValue: -123456,
                ulongValue: 12345678912,
                longValue: -12345678912,
                floatValue: -123.456f,
                doubleValue: -123456.78912);

            EmulateFlight();
        }

        [Test]
        public void Vector3Test()
        {
            Vector3Test(new Vector3(1.5f, 2.25f, 3.125f));
            EmulateFlight();
        }

        [Test]
        public void DoubleArrayTest()
        {
            DoubleArrayTest(new[] { 3.14159265358979323846, 6.28318530717958647692, 2.71828182845904523536 });
            EmulateFlight();
        }

        [Test]
        public void Vector3ArrayTest()
        {
            Vector3ArrayTest(new[] { Vector3.right, Vector3.up, Vector3.forward });
            EmulateFlight();
        }

        [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 20)]
        public struct StructA
        {
            [FieldOffset(0)]
            public float floatValue;
            [FieldOffset(4)]
            public int intValue;
            [FieldOffset(8)]
            public StructB structB;
        }

        [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 12)]
        public struct StructB
        {
            [FieldOffset(0)]
            public bool booleanValue;
            [FieldOffset(4)]
            public long longValue;
        }

        [Test]
        public void StructATest()
        {
            StructATest(new StructA
            {
                floatValue = 3.1415926f,
                intValue = 42,
                structB = new StructB
                {
                    booleanValue = true,
                    longValue = 3141592653589793238
                }
            });
            EmulateFlight();
        }

        [Test]
        public void StructBTest()
        {
            StructBTest(new StructB
            {
                booleanValue = true,
                longValue = long.MaxValue
            });
            EmulateFlight();
        }

        private StructA[] GenerateStructArray () =>
            new[]
            {
                new StructA
                {
                    floatValue = 1.1f,
                    intValue = 42,
                    structB = new StructB
                    {
                        booleanValue = true,
                        longValue = 6283185307179586476
                    }
                },

                new StructA
                {
                    floatValue = 1.2f,
                    intValue = 43,
                    structB = new StructB
                    {
                        booleanValue = false,
                        longValue = 2718281828459045235
                    }
                },

                new StructA
                {
                    floatValue = 1.3f,
                    intValue = 44,
                    structB = new StructB
                    {
                        booleanValue = true,
                        longValue = 1
                    }
                },
            };

        [Test]
        public void StructAArrayTest()
        {
            StructAArrayTest(GenerateStructArray());
            EmulateFlight();
        }

        [Test]
        public void StructANativeArrayTest()
        {
            NativeArray<StructA> structANativeArray = new NativeArray<StructA>(GenerateStructArray(), Allocator.Temp);
            StructANativeArrayTest(structANativeArray);
            structANativeArray.Dispose();
            EmulateFlight();
        }

        [ClusterRPC(RPCExecutionStage.ImmediatelyOnArrival)]
        public static void FloatTest(float floatValue)
        {
            Assert.That(floatValue, Is.AtLeast(1.4f));
        }

        [ClusterRPC(RPCExecutionStage.ImmediatelyOnArrival)]
        public static void StringTest(string stringValue)
        {
            Assert.That(stringValue, Is.EqualTo("Hello, World!"));
        }

        [ClusterRPC(RPCExecutionStage.ImmediatelyOnArrival)]
        public static void MultiStringTest(string stringAValue, string stringBValue)
        {
            Assert.That(stringAValue, Is.EqualTo("Hello"));
            Assert.That(stringBValue, Is.EqualTo("World"));
        }

        [ClusterRPC(RPCExecutionStage.ImmediatelyOnArrival)]
        public static void PrimitivesTest(
            byte byteValue,
            sbyte sbyteValue,
            bool booleanValue,
            char charValue,
            string stringValue,
            ushort ushortValue,
            short shortValue,
            uint uintValue,
            int intValue,
            ulong ulongValue,
            long longValue,
            float floatValue,
            double doubleValue)
        {
            Assert.That(byteValue, Is.EqualTo(128));
            Assert.That(sbyteValue, Is.EqualTo(-128));
            Assert.That(booleanValue, Is.EqualTo(true));
            Assert.That(charValue, Is.EqualTo('@'));
            Assert.That(stringValue, Is.EqualTo("Hello, World!"));
            Assert.That(ushortValue, Is.EqualTo(12345));
            Assert.That(shortValue, Is.EqualTo(-12345));
            Assert.That(uintValue, Is.EqualTo(123456));
            Assert.That(intValue, Is.EqualTo(-123456));
            Assert.That(ulongValue, Is.EqualTo(12345678912));
            Assert.That(longValue, Is.EqualTo(-12345678912));
            Assert.That(floatValue, Is.AtLeast(-123.456f));
            Assert.That(doubleValue, Is.AtLeast(-123456.78912));
        }

        [ClusterRPC(RPCExecutionStage.ImmediatelyOnArrival)]
        public static void Vector3Test(Vector3 vector3Value)
        {
            Assert.That(vector3Value.x, Is.AtLeast(vector3Value.x));
            Assert.That(vector3Value.y, Is.AtLeast(vector3Value.y));
            Assert.That(vector3Value.z, Is.AtLeast(vector3Value.z));
        }

        [ClusterRPC(RPCExecutionStage.ImmediatelyOnArrival)]
        public static void DoubleArrayTest(double[] doubleArray)
        {
            Assert.That(doubleArray[0], Is.AtLeast(3.14159265358979323846));
            Assert.That(doubleArray[1], Is.AtLeast(6.28318530717958647692));
            Assert.That(doubleArray[2], Is.AtLeast(2.71828182845904523536));
        }

        [ClusterRPC(RPCExecutionStage.ImmediatelyOnArrival)]
        public static void Vector3ArrayTest(Vector3[] vectorArray)
        {
            Assert.That(vectorArray[0].x, Is.AtLeast(Vector3.right.x));
            Assert.That(vectorArray[0].y, Is.AtLeast(Vector3.right.y));
            Assert.That(vectorArray[0].z, Is.AtLeast(Vector3.right.z));

            Assert.That(vectorArray[1].x, Is.AtLeast(Vector3.up.x));
            Assert.That(vectorArray[1].y, Is.AtLeast(Vector3.up.y));
            Assert.That(vectorArray[1].z, Is.AtLeast(Vector3.up.z));

            Assert.That(vectorArray[2].x, Is.AtLeast(Vector3.forward.x));
            Assert.That(vectorArray[2].y, Is.AtLeast(Vector3.forward.y));
            Assert.That(vectorArray[2].z, Is.AtLeast(Vector3.forward.z));
        }

        [ClusterRPC(RPCExecutionStage.ImmediatelyOnArrival)]
        public static void StructATest(StructA structA)
        {
            Assert.That(structA.floatValue, Is.AtLeast(3.1415926f));
            Assert.That(structA.intValue, Is.AtLeast(42));
            Assert.That(structA.structB.booleanValue, Is.AtLeast(true));
            Assert.That(structA.structB.longValue, Is.AtLeast(3141592653589793238));
        }

        [ClusterRPC(RPCExecutionStage.ImmediatelyOnArrival)]
        public static void StructBTest(StructB structB)
        {
            Assert.That(structB.booleanValue, Is.AtLeast(true));
            Assert.That(structB.longValue, Is.AtLeast(3141592653589793238));
        }

        [ClusterRPC(RPCExecutionStage.ImmediatelyOnArrival)]
        public static void StructAArrayTest(StructA[] structAArray)
        {
            Assert.That(structAArray[0].floatValue, Is.AtLeast(1.1f));
            Assert.That(structAArray[0].intValue, Is.AtLeast(42));
            Assert.That(structAArray[0].structB.booleanValue, Is.AtLeast(true));
            Assert.That(structAArray[0].structB.longValue, Is.AtLeast(6283185307179586476));

            Assert.That(structAArray[1].floatValue, Is.AtLeast(1.2f));
            Assert.That(structAArray[1].intValue, Is.AtLeast(43));
            Assert.That(structAArray[1].structB.booleanValue, Is.AtLeast(false));
            Assert.That(structAArray[1].structB.longValue, Is.AtLeast(2718281828459045235));

            Assert.That(structAArray[2].floatValue, Is.AtLeast(1.3f));
            Assert.That(structAArray[2].intValue, Is.AtLeast(44));
            Assert.That(structAArray[2].structB.booleanValue, Is.AtLeast(true));
            Assert.That(structAArray[2].structB.longValue, Is.AtLeast(1));
        }

        [ClusterRPC(RPCExecutionStage.ImmediatelyOnArrival)]
        public static void StructANativeArrayTest(NativeArray<StructA> structANativeArray)
        {
            Assert.That(structANativeArray[0].floatValue, Is.AtLeast(1.1f));
            Assert.That(structANativeArray[0].intValue, Is.AtLeast(42));
            Assert.That(structANativeArray[0].structB.booleanValue, Is.AtLeast(true));
            Assert.That(structANativeArray[0].structB.longValue, Is.AtLeast(6283185307179586476));

            Assert.That(structANativeArray[1].floatValue, Is.AtLeast(1.2f));
            Assert.That(structANativeArray[1].intValue, Is.AtLeast(43));
            Assert.That(structANativeArray[1].structB.booleanValue, Is.AtLeast(false));
            Assert.That(structANativeArray[1].structB.longValue, Is.AtLeast(2718281828459045235));

            Assert.That(structANativeArray[2].floatValue, Is.AtLeast(1.3f));
            Assert.That(structANativeArray[2].intValue, Is.AtLeast(44));
            Assert.That(structANativeArray[2].structB.booleanValue, Is.AtLeast(true));
            Assert.That(structANativeArray[2].structB.longValue, Is.AtLeast(1));
        }

        [TearDown]
        public void TearDown()
        {
            RPCBufferIO.Dispose();
        }
    }
}
