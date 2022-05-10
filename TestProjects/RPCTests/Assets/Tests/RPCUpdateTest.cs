using NUnit.Framework;
using System.Runtime.InteropServices;

using UnityEngine;
using Unity.Collections;
using UnityEngine.TestTools;

using Unity.ClusterDisplay.RPC;
using System;

namespace Unity.ClusterDisplay.Tests
{
    public class RPCUpdateTest : RPCMonoBehaviourBase
    {
        const RPCExecutionStage k_BeforeStage = RPCExecutionStage.BeforeUpdate;
        const RPCExecutionStage k_AfterStage = RPCExecutionStage.AfterUpdate;

        protected override RPCExecutionStage BeforeStage => k_BeforeStage;
        protected override RPCExecutionStage AfterStage => k_AfterStage;

        void Update ()
        {
            if (FininishedFirstFrame)
            {
                PollTestState();
                return;
            }

            Propagate();
            PollTestState();
        }

        [ClusterRPC(k_BeforeStage)] public override void FloatTestBefore (float floatValue) => Test(() => RPCs.FloatTest(floatValue), BeforeStage);
        [ClusterRPC(k_BeforeStage)] public override void StringTestBefore(string stringValue) => Test(() => RPCs.StringTest(stringValue), BeforeStage);
        [ClusterRPC(k_BeforeStage)] public override void MultiStringTestBefore(string stringAValue, string stringBValue) => Test(() => RPCs.MultiStringTest(stringAValue, stringBValue), BeforeStage);
        [ClusterRPC(k_BeforeStage)] public override void Vector3TestBefore(Vector3 vector3Value) => Test(() => RPCs.Vector3Test(vector3Value), BeforeStage);
        [ClusterRPC(k_BeforeStage)] public override void DoubleArrayTestBefore(double[] doubleArray) => Test(() => RPCs.DoubleArrayTest(doubleArray), BeforeStage);
        [ClusterRPC(k_BeforeStage)] public override void Vector3ArrayTestBefore(Vector3[] vectorArray)  => Test(() => RPCs.Vector3ArrayTest(vectorArray), BeforeStage);
        [ClusterRPC(k_BeforeStage)] public override void StructATestBefore(StructA structA) => Test(() => RPCs.StructATest(structA), BeforeStage);
        [ClusterRPC(k_BeforeStage)] public override void StructBTestBefore(StructB structB) => Test(() => RPCs.StructBTest(structB), BeforeStage);
        [ClusterRPC(k_BeforeStage)] public override void StructAArrayTestBefore(StructA[] structAArray) => Test(() => RPCs.StructAArrayTest(structAArray), BeforeStage);
        [ClusterRPC(k_BeforeStage)] public override void StructANativeArrayTestBefore(NativeArray<StructA> structANativeArray) => Test(() => RPCs.StructANativeArrayTest(structANativeArray), BeforeStage);
        [ClusterRPC(k_BeforeStage)] public override void PrimitivesTestBefore(
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

        // ----------

        [ClusterRPC(k_AfterStage)] public override void FloatTestAfter(float floatValue) => Test(() => RPCs.FloatTest(floatValue), AfterStage);
        [ClusterRPC(k_AfterStage)] public override void StringTestAfter(string stringValue) => Test(() => RPCs.StringTest(stringValue), AfterStage);
        [ClusterRPC(k_AfterStage)] public override void MultiStringTestAfter(string stringAValue, string stringBValue) => Test(() => RPCs.MultiStringTest(stringAValue, stringBValue), AfterStage);
        [ClusterRPC(k_AfterStage)] public override void PrimitivesTestAfter(
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
        [ClusterRPC(k_AfterStage)] public override void Vector3TestAfter(Vector3 vector3Value) => Test(() => RPCs.Vector3Test(vector3Value), AfterStage);
        [ClusterRPC(k_AfterStage)] public override void DoubleArrayTestAfter(double[] doubleArray) => Test(() => RPCs.DoubleArrayTest(doubleArray), AfterStage);
        [ClusterRPC(k_AfterStage)] public override void Vector3ArrayTestAfter(Vector3[] vectorArray)  => Test(() => RPCs.Vector3ArrayTest(vectorArray), AfterStage);
        [ClusterRPC(k_AfterStage)] public override void StructATestAfter(StructA structA) => Test(() => RPCs.StructATest(structA), AfterStage);
        [ClusterRPC(k_AfterStage)] public override void StructBTestAfter(StructB structB) => Test(() => RPCs.StructBTest(structB), AfterStage);
        [ClusterRPC(k_AfterStage)] public override void StructAArrayTestAfter(StructA[] structAArray) => Test(() => RPCs.StructAArrayTest(structAArray), AfterStage);
        [ClusterRPC(k_AfterStage)] public override void StructANativeArrayTestAfter(NativeArray<StructA> structANativeArray) => Test(() => RPCs.StructANativeArrayTest(structANativeArray), AfterStage);
    }
}
