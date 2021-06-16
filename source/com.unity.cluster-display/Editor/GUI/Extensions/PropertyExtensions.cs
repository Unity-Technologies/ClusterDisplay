using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

using Unity.ClusterDisplay.Networking;

namespace Unity.ClusterDisplay.Editor.Extensions
{
    [CustomPropertyDrawer(typeof(int))]
    public class IntPropertyDrawerExtension : PropertyDrawerExtension<int> {}

    [CustomPropertyDrawer(typeof(float))]
    public class FloatPropertyDrawerExtension : PropertyDrawerExtension<float> {}

    [CustomPropertyDrawer(typeof(int[]))]
    public class ArrayPropertyDrawerExtension : PropertyDrawerExtension<int[]> {}
}
