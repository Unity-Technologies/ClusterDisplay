using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.ClusterDisplay.Tests
{
    [System.Serializable]
    public struct ValueStruct
    {
        public int intTest;
        public float floatTest;
        // public string stringTest;
        public Vector3 vectorTest;
        public NestedStruct nestedStructTest;
    }

    [System.Serializable]
    public struct NestedStruct
    {
        public double doubleTest;
        public long longTest;
        public Vector2 vector;
    }

    public class TypeMemberRPCTest : MonoBehaviour
    {
        [SerializeField] private ValueStruct valueTypeStruct;
        public ValueStruct ValueTypeStructProperty { get => valueTypeStruct; set => valueTypeStruct = value; }

        [SerializeField] private float floatingPoint;
        public float FloatingPoint { get => floatingPoint; set => floatingPoint = value; }

        [SerializeField] private Color colorTest;
        public Color ColorTest 
        { 
            get => colorTest; 
            set => colorTest = value; 
        }

        [SerializeField] private Vector2 vector2Test;
        public Vector2 Vector2Test { get => vector2Test; set => vector2Test = value; }

        [SerializeField] private Vector3 vector3Test;
        public Vector3 Vector3Test { get => vector3Test; set => vector3Test = value; }
    }
}
