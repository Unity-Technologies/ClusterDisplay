using NUnit.Framework;
using System.Runtime.InteropServices;

using UnityEngine;
using Unity.Collections;
using UnityEngine.TestTools;

using Unity.ClusterDisplay.RPC;
using System;

namespace Unity.ClusterDisplay.Tests
{
    public class RPCLateUpdateTest : RPCMonoBehaviourBase, IMonoBehaviourTest
    {
        protected override RPCExecutionStage BeforeStage => RPCExecutionStage.BeforeLateUpdate;
        protected override RPCExecutionStage AfterStage => RPCExecutionStage.AfterLateUpdate;

        void LateUpdate ()
        {
            if (ExecutedAllTests)
            {
                return;
            }

            Execute();
        }

        [ClusterRPC(RPCExecutionStage.BeforeLateUpdate)]
        public override void FloatTestBefore (float floatValue) => Test(() => RPCs.FloatTest(floatValue), BeforeStage);

        [ClusterRPC(RPCExecutionStage.AfterLateUpdate)]
        public override void FloatTestAfter(float floatValue) => Test(() => RPCs.FloatTest(floatValue), AfterStage);

        [ClusterRPC(RPCExecutionStage.BeforeLateUpdate)]
        public override void StringTestBefore(string stringValue) => Test(() => RPCs.StringTest(stringValue), BeforeStage);

        [ClusterRPC(RPCExecutionStage.AfterLateUpdate)]
        public override void StringTestAfter(string stringValue) => Test(() => RPCs.StringTest(stringValue), AfterStage);

        [ClusterRPC(RPCExecutionStage.BeforeLateUpdate)]
        public override void MultiStringTestBefore(string stringAValue, string stringBValue) => Test(() => RPCs.MultiStringTest(stringAValue, stringBValue), BeforeStage);

        [ClusterRPC(RPCExecutionStage.AfterLateUpdate)]
        public override void MultiStringTestAfter(string stringAValue, string stringBValue) => Test(() => RPCs.MultiStringTest(stringAValue, stringBValue), AfterStage);

        [ClusterRPC(RPCExecutionStage.BeforeLateUpdate)]
        public override void PrimitivesTestBefore(
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

        [ClusterRPC(RPCExecutionStage.AfterLateUpdate)]
        public override void PrimitivesTestAfter(
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
                doubleValue), AfterStage);

        [ClusterRPC(RPCExecutionStage.BeforeLateUpdate)]
        public override void Vector3TestBefore(Vector3 vector3Value) => Test(() => RPCs.Vector3Test(vector3Value), BeforeStage);

        [ClusterRPC(RPCExecutionStage.AfterLateUpdate)]
        public override void Vector3TestAfter(Vector3 vector3Value) => Test(() => RPCs.Vector3Test(vector3Value), AfterStage);

        [ClusterRPC(RPCExecutionStage.BeforeLateUpdate)]
        public override void DoubleArrayTestBefore(double[] doubleArray) => Test(() => RPCs.DoubleArrayTest(doubleArray), BeforeStage);

        [ClusterRPC(RPCExecutionStage.AfterLateUpdate)]
        public override void DoubleArrayTestAfter(double[] doubleArray) => Test(() => RPCs.DoubleArrayTest(doubleArray), AfterStage);

        [ClusterRPC(RPCExecutionStage.BeforeLateUpdate)]
        public override void Vector3ArrayTestBefore(Vector3[] vectorArray)  => Test(() => RPCs.Vector3ArrayTest(vectorArray), BeforeStage);

        [ClusterRPC(RPCExecutionStage.AfterLateUpdate)]
        public override void Vector3ArrayTestAfter(Vector3[] vectorArray)  => Test(() => RPCs.Vector3ArrayTest(vectorArray), AfterStage);

        [ClusterRPC(RPCExecutionStage.BeforeLateUpdate)]
        public override void StructATestBefore(StructA structA) => Test(() => RPCs.StructATest(structA), BeforeStage);

        [ClusterRPC(RPCExecutionStage.AfterLateUpdate)]
        public override void StructATestAfter(StructA structA) => Test(() => RPCs.StructATest(structA), AfterStage);

        [ClusterRPC(RPCExecutionStage.BeforeLateUpdate)]
        public override void StructBTestBefore(StructB structB) => Test(() => RPCs.StructBTest(structB), BeforeStage);

        [ClusterRPC(RPCExecutionStage.AfterLateUpdate)]
        public override void StructBTestAfter(StructB structB) => Test(() => RPCs.StructBTest(structB), AfterStage);

        [ClusterRPC(RPCExecutionStage.BeforeLateUpdate)]
        public override void StructAArrayTestBefore(StructA[] structAArray) => Test(() => RPCs.StructAArrayTest(structAArray), BeforeStage);

        [ClusterRPC(RPCExecutionStage.AfterLateUpdate)]
        public override void StructAArrayTestAfter(StructA[] structAArray) => Test(() => RPCs.StructAArrayTest(structAArray), AfterStage);

        [ClusterRPC(RPCExecutionStage.BeforeLateUpdate)]
        public override void StructANativeArrayTestBefore(NativeArray<StructA> structANativeArray) => Test(() => RPCs.StructANativeArrayTest(structANativeArray), BeforeStage);

        [ClusterRPC(RPCExecutionStage.AfterLateUpdate)]
        public override void StructANativeArrayTestAfter(NativeArray<StructA> structANativeArray) => Test(() => RPCs.StructANativeArrayTest(structANativeArray), AfterStage);
    }
}
