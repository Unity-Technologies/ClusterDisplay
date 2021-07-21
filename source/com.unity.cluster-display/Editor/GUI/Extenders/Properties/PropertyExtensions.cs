using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

using Unity.ClusterDisplay.RPC;

namespace Unity.ClusterDisplay.Editor.Inspectors
{
    [CustomPropertyDrawer(typeof(int))]
    public class IntPropertyDrawerExtension : PropertyDrawerExtension<int> {}

    [CustomPropertyDrawer(typeof(float))]
    public class FloatPropertyDrawerExtension : PropertyDrawerExtension<float> {}

    [CustomPropertyDrawer(typeof(Vector3))]
    public class Vector3PropertyDrawerExtension : PropertyDrawerExtension<Vector3> {}

    [CustomPropertyDrawer(typeof(Vector2))]
    public class VectorfPropertyDrawerExtension : PropertyDrawerExtension<Vector2> {}

    [CustomPropertyDrawer(typeof(int[]))]
    public class ArrayPropertyDrawerExtension : PropertyDrawerExtension<int[]> {}
}
