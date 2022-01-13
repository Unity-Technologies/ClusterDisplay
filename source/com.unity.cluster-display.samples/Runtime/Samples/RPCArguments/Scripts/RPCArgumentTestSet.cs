using System;
using System.Runtime.InteropServices;
using Unity.ClusterDisplay;
using Unity.ClusterDisplay.RPC;
using Unity.Collections;
using UnityEngine;

public class RPCArgumentTestSet : MonoBehaviour
{
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

    private void Update()
    {
        if (ClusterDisplayState.IsRepeater)
        {
            enabled = false;
            return;
        }
        
        FloatTest(1.4f);
        StringTest("Hello, World!");
        MultiStringTest("Hello", "World");
        
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
        
        StructBTest(new StructB
        {
            booleanValue = true,
            longValue = long.MaxValue
        });
        
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
        
        DoubleArrayTest(new []{3.14159265358979323846, 6.28318530717958647692, 2.71828182845904523536});
        Vector3ArrayTest(new []{Vector3.right, Vector3.up, Vector3.forward});
        Vector3Test(new Vector3(1.5f, 2.25f, 3.125f));
        Vector3ArrayTest(new []{Vector3.right, Vector3.up, Vector3.forward});
        
        StructBTest(new StructB
        {
            booleanValue = true,
            longValue = long.MaxValue
        });
        
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
        
        StructA[] structAArray = new[]
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
        
        StructAArrayTest(structAArray);

        NativeArray<StructA> structANativeArray = new NativeArray<StructA>(structAArray, Allocator.Temp);
        StructANativeArrayTest(structANativeArray);
        structANativeArray.Dispose();
    }

    [ClusterRPC]
    public void FloatTest(float floatValue)
    {
        if (ClusterDisplayState.IsRepeater)
            Debug.Log($"Repeater received float: \"{floatValue}\".");
    }
    
    [ClusterRPC]
    public void StringTest(string stringValue)
    {
        if (ClusterDisplayState.IsRepeater)
            Debug.Log($"Repeater received string: \"{stringValue}\".");
    }
    
    [ClusterRPC]
    public void MultiStringTest(string stringAValue, string stringBValue)
    {
        if (ClusterDisplayState.IsRepeater)
            Debug.Log($"Repeater received stringA: \"{stringAValue}\", stringB: \"{stringBValue}\".");
    }

    [ClusterRPC]
    public void PrimitivesTest(
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
        if (ClusterDisplayState.IsRepeater)
        {
            string msg = "Repeater received primitives:";

            msg = $"{msg}\n\tByte: {byteValue}";
            msg = $"{msg}\n\tSigned Byte: {sbyteValue}";
            msg = $"{msg}\n\tBoolean: {booleanValue}";
            msg = $"{msg}\n\tChar: {charValue}";
            msg = $"{msg}\n\tString: {stringValue}";
            msg = $"{msg}\n\tUnsigned Short: {ushortValue}";
            msg = $"{msg}\n\tShort: {shortValue}";
            msg = $"{msg}\n\tUnsigned Integer: {uintValue}";
            msg = $"{msg}\n\tInteger: {intValue}";
            msg = $"{msg}\n\tUnsigned Long: {ulongValue}";
            msg = $"{msg}\n\tLong: {longValue}";
            msg = $"{msg}\n\tFloat: {floatValue}";
            msg = $"{msg}\n\tDouble: {doubleValue}";

            Debug.Log(msg);
        }
    }

    [ClusterRPC]
    public void Vector3Test(Vector3 vector3Value)
    {
        if (ClusterDisplayState.IsRepeater)
            Debug.Log($"Repeater received Vector3: \"{vector3Value}\".");
    }
    
    [ClusterRPC]
    public void DoubleArrayTest(double[] doubleArray)
    {
        if (ClusterDisplayState.IsRepeater)
        {
            string msg = "Repeater received double[]:";
            for (int i = 0; i < doubleArray.Length; i++)
                msg = $"{msg}\n\t{doubleArray[i]},";
            Debug.Log(msg);
        }
    }
    
    [ClusterRPC]
    public void Vector3ArrayTest(Vector3[] vectorArray)
    {
        if (ClusterDisplayState.IsRepeater)
        {
            string msg = "Repeater received Vector3[]:";
            for (int i = 0; i < vectorArray.Length; i++)
                msg = $"{msg}\n\t{vectorArray[i]},";
            Debug.Log(msg);
        }
    }

    [ClusterRPC]
    public void StructBTest (StructB structB)
    {
        if (ClusterDisplayState.IsRepeater)
            Debug.Log($"Repeater received StructB: (Boolean: \"{structB.booleanValue}\", Long: \"{structB.longValue}\").");
    }

    [ClusterRPC]
    public void StructATest(StructA structA)
    {
        if (ClusterDisplayState.IsRepeater)
            Debug.Log($"Repeater received StructA: (Float: \"{structA.floatValue}\", Integer: \"{structA.intValue}\", StructB: (Boolean: \"{structA.structB.booleanValue}\", Long: \"{structA.structB.longValue}\")).");
    }

    [ClusterRPC]
    public void StructAArrayTest(StructA[] structAArray)
    {
        if (ClusterDisplayState.IsRepeater)
        {
            string msg = "Repeater received StructA[]:";
            for (int i = 0; i < structAArray.Length; i++)
                msg = $"{msg}\n\t{i}: (Float: \"{structAArray[i].floatValue}\", Integer: \"{structAArray[i].intValue}\", StructB: (Boolean: \"{structAArray[i].structB.booleanValue}\", Long: \"{structAArray[i].structB.longValue}\")).";
            Debug.Log(msg);
        }
    }
    
    [ClusterRPC]
    public void StructANativeArrayTest(NativeArray<StructA> structANativeArray)
    {
        if (ClusterDisplayState.IsRepeater)
        {
            string msg = "Repeater received NativeArray<StructA>:";
            for (int i = 0; i < structANativeArray.Length; i++)
                msg = $"{msg}\n\t{i}: (Float: \"{structANativeArray[i].floatValue}\", Integer: \"{structANativeArray[i].intValue}\", StructB: (Boolean: \"{structANativeArray[i].structB.booleanValue}\", Long: \"{structANativeArray[i].structB.longValue}\")).";
            Debug.Log(msg);
        }

        structANativeArray.Dispose();
    }
}
