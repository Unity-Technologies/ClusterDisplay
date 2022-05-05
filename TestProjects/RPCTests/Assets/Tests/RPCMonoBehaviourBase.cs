using NUnit.Framework;
using System.Runtime.InteropServices;

using UnityEngine;
using Unity.Collections;
using UnityEngine.TestTools;

using Unity.ClusterDisplay.RPC;
using System;

namespace Unity.ClusterDisplay.Tests
{
    public abstract partial class RPCMonoBehaviourBase : MonoBehaviour
    {
        enum TestStage : int
        {
            Automatic,
            Before,
            After
        }

        protected bool m_ExecutedAllTests = false, m_IsTestFinished = false;

        public bool IsTestFinished => m_IsTestFinished;
        protected bool ExecutedAllTests => m_ExecutedAllTests;

        protected abstract RPCExecutionStage BeforeStage { get; }
        protected abstract RPCExecutionStage AfterStage { get; }

        TestStage stage = TestStage.Automatic;
        void EndTest() => m_IsTestFinished = true;

        protected void Test(Action callback, RPCExecutionStage expectedExecutionStage)
        {
            if (!ExecutedAllTests)
            {
                return;
            }

            try
            {
                Assert.That(expectedExecutionStage, Is.EqualTo(RPCExecutor.CurrentExecutionStage));

                switch (stage)
                {
                    case TestStage.Automatic:
                        callback();
                        break;

                    case TestStage.Before:
                        callback();
                        break;

                    case TestStage.After:
                        callback();
                        EndTest();
                        break;
                }

                stage++;
            }

            catch (Exception exception)
            {
                Debug.LogException(exception);
                EndTest();
            }
        }

        void Setup()
        {
            if (!SceneObjectsRegistry.TryCreateNewInstance(gameObject.scene, out var sceneObjectsRegistry))
            {
                throw new System.Exception($"Unable to create new instance of: \"{nameof(SceneObjectsRegistry)}\".");
            }

            if (!sceneObjectsRegistry.TryRegister(this, this.GetType()))
            {
                throw new System.Exception($"Unable to register instance of: \"{this.GetType().Name}\".");
            }
        }

        void ExecuteRPCs()
        {
            var floatValue = 1.4f;
            FloatTestAutomatic(floatValue);
            FloatTestBefore(floatValue);
            FloatTestAfter(floatValue);

            var str = "Hello, World!";
            StringTestAutomatic(str);
            StringTestBefore(str);
            StringTestAfter(str);

            string strA = "Hello", strB = "World";
            MultiStringTestAutomatic(strA, strB);
            MultiStringTestBefore(strA, strB);
            MultiStringTestAfter(strA, strB);

            PrimitivesTestAutomatic(
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

            PrimitivesTestBefore(
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

            PrimitivesTestAfter(
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

            var vector = new Vector3(1.5f, 2.25f, 3.125f);
            Vector3TestAutomatic(vector);
            Vector3TestBefore(vector);
            Vector3TestAfter(vector);

            var doubleArray = new[] { 3.14159265358979323846, 6.28318530717958647692, 2.71828182845904523536 };
            DoubleArrayTestAutomatic(doubleArray);
            DoubleArrayTestBefore(doubleArray);
            DoubleArrayTestAfter(doubleArray);

            var vectorArray = new[] { Vector3.right, Vector3.up, Vector3.forward };
            Vector3ArrayTestAutomatic(vectorArray);
            Vector3ArrayTestBefore(vectorArray);
            Vector3ArrayTestAfter(vectorArray);

            var structA = new StructA
            {
                floatValue = 3.1415926f,
                intValue = 42,
                structB = new StructB
                {
                    booleanValue = true,
                    longValue = 3141592653589793238
                }
            };

            StructATestAutomatic(structA);
            StructATestBefore(structA);
            StructATestAfter(structA);

            var structB = new StructB
            {
                booleanValue = true,
                longValue = long.MaxValue
            };

            StructBTestAutomatic(structB);
            StructBTestBefore(structB);
            StructBTestAfter(structB);

            var structArray = RPCs.GenerateStructArray();
            StructAArrayTestAutomatic(structArray);
            StructAArrayTestBefore(structArray);
            StructAArrayTestAfter(structArray);

            NativeArray<StructA> structANativeArray = new NativeArray<StructA>(RPCs.GenerateStructArray(), Allocator.Temp);
            StructANativeArrayTestAutomatic(structANativeArray);
            StructANativeArrayTestBefore(structANativeArray);
            StructANativeArrayTestAfter(structANativeArray);
            structANativeArray.Dispose();
        }
        protected void Execute ()
        {
            Setup();

            RPCs.FlagSending();
            ExecuteRPCs();
            RPCs.FlagReceiving();

            m_ExecutedAllTests = true;
            RPCs.EmulateFlight();
        }

        [ClusterRPC] public void FloatTestAutomatic(float floatValue) => Test(() => RPCs.FloatTest(floatValue), BeforeStage);
        [ClusterRPC] public void StringTestAutomatic(string stringValue) => Test(() => RPCs.StringTest(stringValue), BeforeStage);
        [ClusterRPC] public void MultiStringTestAutomatic(string stringAValue, string stringBValue) => Test(() => RPCs.MultiStringTest(stringAValue, stringBValue), BeforeStage);
        [ClusterRPC] public void PrimitivesTestAutomatic(
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
            double doubleValue) => Test(() => RPCs.PrimitivesTest(
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
                doubleValue), BeforeStage);
        [ClusterRPC] public void Vector3TestAutomatic(Vector3 vector3Value) => Test(() => RPCs.Vector3Test(vector3Value), BeforeStage);
        [ClusterRPC] public void DoubleArrayTestAutomatic(double[] doubleArray) => Test(() => RPCs.DoubleArrayTest(doubleArray), BeforeStage);
        [ClusterRPC] public void Vector3ArrayTestAutomatic(Vector3[] vectorArray)  => Test(() => RPCs.Vector3ArrayTest(vectorArray), BeforeStage);
        [ClusterRPC] public void StructATestAutomatic(StructA structA) => Test(() => RPCs.StructATest(structA), BeforeStage);
        [ClusterRPC] public void StructBTestAutomatic(StructB structB) => Test(() => RPCs.StructBTest(structB), BeforeStage);
        [ClusterRPC] public void StructAArrayTestAutomatic(StructA[] structAArray) => Test(() => RPCs.StructAArrayTest(structAArray), BeforeStage);
        [ClusterRPC] public void StructANativeArrayTestAutomatic(NativeArray<StructA> structANativeArray) => Test(() => RPCs.StructANativeArrayTest(structANativeArray), BeforeStage);

        public abstract void FloatTestBefore(float floatValue);
        public abstract void StringTestBefore(string stringValue);
        public abstract void MultiStringTestBefore(string stringAValue, string stringBValue);
        public abstract void PrimitivesTestBefore(
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
            double doubleValue);
        public abstract void Vector3TestBefore(Vector3 vector3Value);
        public abstract void DoubleArrayTestBefore(double[] doubleArray);
        public abstract void Vector3ArrayTestBefore(Vector3[] vectorArray);
        public abstract void StructATestBefore(StructA structA);
        public abstract void StructBTestBefore(StructB structB);
        public abstract void StructAArrayTestBefore(StructA[] structAArray);
        public abstract void StructANativeArrayTestBefore(NativeArray<StructA> structANativeArray);

        public abstract void FloatTestAfter(float floatValue);
        public abstract void StringTestAfter(string stringValue);
        public abstract void MultiStringTestAfter(string stringAValue, string stringBValue);

        public abstract void PrimitivesTestAfter(
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
            double doubleValue);

        public abstract void Vector3TestAfter(Vector3 vector3Value);
        public abstract void DoubleArrayTestAfter(double[] doubleArray);
        public abstract void Vector3ArrayTestAfter(Vector3[] vectorArray);
        public abstract void StructATestAfter(StructA structA);
        public abstract void StructBTestAfter(StructB structB);
        public abstract void StructAArrayTestAfter(StructA[] structAArray);
        public abstract void StructANativeArrayTestAfter(NativeArray<StructA> structANativeArray);
    }
}
