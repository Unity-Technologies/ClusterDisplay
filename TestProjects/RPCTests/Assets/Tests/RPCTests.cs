using NUnit.Framework;
using System.Runtime.InteropServices;

using UnityEngine;
using Unity.Collections;
using UnityEngine.TestTools;

using Unity.ClusterDisplay.RPC;
using System.Collections;

namespace Unity.ClusterDisplay.Tests
{
    public class RPCTests : IRPCTestRecorder
    {
        static RPCTestRecorder m_RPCTestRecorder;
        public void RecordPropagation() => m_RPCTestRecorder.RecordPropagation();
        public void RecordExecution() => m_RPCTestRecorder.RecordExecution();

        [SetUp]
        public void SetUp()
        {
            m_RPCTestRecorder = new RPCTestRecorder(this);
            RPCs.PushRPCTestRecorder(this);
            RPCs.Initialize();
        }

        [Test]
        public void StringTest()
        {
            RPCs.FlagSending();
            StringTestImmediatelyOnArrival("Hello, World!");
            RPCs.FlagReceiving();
            RPCs.EmulateFlight();
        }

        [Test]
        public void FloatTest()
        {
            RPCs.FlagSending();
            FloatTestImmediatelyOnArrival(1.4f);
            RPCs.FlagReceiving();
            RPCs.EmulateFlight();
        }

        [Test]
        public void MultiStringTest()
        {
            RPCs.FlagSending();
            MultiStringTestImmediatelyOnArrival("Hello", "World");
            RPCs.FlagReceiving();
            RPCs.EmulateFlight();
        }

        [Test]
        public void PrimitivesTest()
        {
            RPCs.FlagSending();
            PrimitivesTestImmediatelyOnArrival(
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
            Vector3TestImmediatelyOnArrival(new Vector3(1.5f, 2.25f, 3.125f));
            RPCs.FlagReceiving();
            RPCs.EmulateFlight();
        }

        [Test]
        public void DoubleArrayTest()
        {
            RPCs.FlagSending();
            DoubleArrayTestImmediatelyOnArrival(new[] { 3.14159265358979323846, 6.28318530717958647692, 2.71828182845904523536 });
            RPCs.FlagReceiving();
            RPCs.EmulateFlight();
        }

        [Test]
        public void Vector3ArrayTest()
        {
            RPCs.FlagSending();
            Vector3ArrayTestImmediatelyOnArrival(new[] { Vector3.right, Vector3.up, Vector3.forward });
            RPCs.FlagReceiving();
            RPCs.EmulateFlight();
        }

        [Test]
        public void StructATest()
        {
            RPCs.FlagSending();
            StructATestImmediatelyOnArrival(new StructA
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
            StructBTestImmediatelyOnArrival(new StructB
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
            StructAArrayTestImmediatelyOnArrival(RPCs.GenerateStructArray());
            RPCs.FlagReceiving();
            RPCs.EmulateFlight();
        }

        [Test]
        public void StructANativeArrayTest()
        {
            RPCs.FlagSending();

            NativeArray<StructA> structANativeArray = new NativeArray<StructA>(RPCs.GenerateStructArray(), Allocator.Temp);
            StructANativeArrayTestImmediatelyOnArrival(structANativeArray);
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

        [ClusterRPC(RPCExecutionStage.ImmediatelyOnArrival)]
        public static void FloatTestImmediatelyOnArrival(float floatValue) => RPCs.FloatTest(floatValue);

        [ClusterRPC(RPCExecutionStage.ImmediatelyOnArrival)]
        public static void StringTestImmediatelyOnArrival(string stringValue) => RPCs.StringTest(stringValue);

        [ClusterRPC(RPCExecutionStage.ImmediatelyOnArrival)]
        public static void MultiStringTestImmediatelyOnArrival(string stringAValue, string stringBValue) => RPCs.MultiStringTest(stringAValue, stringBValue);

        [ClusterRPC(RPCExecutionStage.ImmediatelyOnArrival)]
        public static void PrimitivesTestImmediatelyOnArrival(
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
            double doubleValue) => RPCs.PrimitivesTest(
                byteValue,
                sbyteValue,
                booleanValue,
                charValue,
                stringValue,
                ushortValue,
                shortValue,
                uintValue,
                intValue,
                ulongValue,
                longValue,
                floatValue,
                doubleValue);

        [ClusterRPC(RPCExecutionStage.ImmediatelyOnArrival)]
        public static void Vector3TestImmediatelyOnArrival(Vector3 vector3Value) => RPCs.Vector3Test(vector3Value);

        [ClusterRPC(RPCExecutionStage.ImmediatelyOnArrival)]
        public static void DoubleArrayTestImmediatelyOnArrival(double[] doubleArray) => RPCs.DoubleArrayTest(doubleArray);

        [ClusterRPC(RPCExecutionStage.ImmediatelyOnArrival)]
        public static void Vector3ArrayTestImmediatelyOnArrival(Vector3[] vectorArray) => RPCs.Vector3ArrayTest(vectorArray);

        [ClusterRPC(RPCExecutionStage.ImmediatelyOnArrival)]
        public static void StructATestImmediatelyOnArrival(StructA structA) => RPCs.StructATest(structA);

        [ClusterRPC(RPCExecutionStage.ImmediatelyOnArrival)]
        public static void StructBTestImmediatelyOnArrival(StructB structB) => RPCs.StructBTest(structB);

        [ClusterRPC(RPCExecutionStage.ImmediatelyOnArrival)]
        public static void StructAArrayTestImmediatelyOnArrival(StructA[] structAArray) => RPCs.StructAArrayTest(structAArray);

        [ClusterRPC(RPCExecutionStage.ImmediatelyOnArrival)]
        public static void StructANativeArrayTestImmediatelyOnArrival(NativeArray<StructA> structANativeArray) => RPCs.StructANativeArrayTest(structANativeArray);
    }
}
