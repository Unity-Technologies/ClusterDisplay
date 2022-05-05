using NUnit.Framework;
using System.Runtime.InteropServices;

using UnityEngine;
using Unity.Collections;
using UnityEngine.TestTools;

using Unity.ClusterDisplay.RPC;
using System.Collections;

namespace Unity.ClusterDisplay.Tests
{
    public class RPCTests
    {
        [SetUp]
        public void SetUp() => RPCs.Initialize();

        [Test]
        public void StringTest()
        {
            RPCs.FlagSending();
            RPCs.StringTestImmediatelyOnArrival("Hello, World!");
            RPCs.FlagReceiving();
            RPCs.EmulateFlight();
        }

        [Test]
        public void FloatTest()
        {
            RPCs.FlagSending();
            RPCs.FloatTestImmediatelyOnArrival(1.4f);
            RPCs.FlagReceiving();
            RPCs.EmulateFlight();
        }

        [Test]
        public void MultiStringTest()
        {
            RPCs.FlagSending();
            RPCs.MultiStringTestImmediatelyOnArrival("Hello", "World");
            RPCs.FlagReceiving();
            RPCs.EmulateFlight();
        }

        [Test]
        public void PrimitivesTest()
        {
            RPCs.FlagSending();
            RPCs.PrimitivesTestImmediatelyOnArrival(
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

            RPCs.FlagReceiving();
            RPCs.EmulateFlight();
        }

        [Test]
        public void Vector3Test()
        {
            RPCs.FlagSending();
            RPCs.Vector3TestImmediatelyOnArrival(new Vector3(1.5f, 2.25f, 3.125f));
            RPCs.FlagReceiving();
            RPCs.EmulateFlight();
        }

        [Test]
        public void DoubleArrayTest()
        {
            RPCs.FlagSending();
            RPCs.DoubleArrayTestImmediatelyOnArrival(new[] { 3.14159265358979323846, 6.28318530717958647692, 2.71828182845904523536 });
            RPCs.FlagReceiving();
            RPCs.EmulateFlight();
        }

        [Test]
        public void Vector3ArrayTest()
        {
            RPCs.FlagSending();
            RPCs.Vector3ArrayTestImmediatelyOnArrival(new[] { Vector3.right, Vector3.up, Vector3.forward });
            RPCs.FlagReceiving();
            RPCs.EmulateFlight();
        }

        [Test]
        public void StructATest()
        {
            RPCs.FlagSending();
            RPCs.StructATestImmediatelyOnArrival(new StructA
            {
                floatValue = 3.1415926f,
                intValue = 42,
                structB = new StructB
                {
                    booleanValue = true,
                    longValue = 3141592653589793238
                }
            });
            RPCs.FlagReceiving();
            RPCs.EmulateFlight();
        }

        [Test]
        public void StructBTest()
        {
            RPCs.FlagSending();
            RPCs.StructBTestImmediatelyOnArrival(new StructB
            {
                booleanValue = true,
                longValue = long.MaxValue
            });
            RPCs.FlagReceiving();
            RPCs.EmulateFlight();
        }

        [Test]
        public void StructAArrayTest()
        {
            RPCs.FlagSending();
            RPCs.StructAArrayTestImmediatelyOnArrival(RPCs.GenerateStructArray());
            RPCs.FlagReceiving();
            RPCs.EmulateFlight();
        }

        [Test]
        public void StructANativeArrayTest()
        {
            RPCs.FlagSending();

            NativeArray<StructA> structANativeArray = new NativeArray<StructA>(RPCs.GenerateStructArray(), Allocator.Temp);
            RPCs.StructANativeArrayTestImmediatelyOnArrival(structANativeArray);
            structANativeArray.Dispose();

            RPCs.FlagReceiving();
            RPCs.EmulateFlight();
        }

        [UnityTest]
        public IEnumerator FixedUpdateTest ()
        {
            yield return new MonoBehaviourTest<RPCFixedUpdateTest>();
        }

        [UnityTest]
        public IEnumerator UpdateTest ()
        {
            yield return new MonoBehaviourTest<RPCUpdateTest>();
        }

        [UnityTest]
        public IEnumerator LateUpdate ()
        {
            yield return new MonoBehaviourTest<RPCLateUpdateTest>();
        }

        [TearDown]
        public void TearDown() => RPCs.Dispose();
    }
}
