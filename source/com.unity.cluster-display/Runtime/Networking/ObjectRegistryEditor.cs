using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public partial class ObjectRegistry : ScriptableObject, ISerializationCallbackReceiver
{
#if UNITY_EDITOR
    [CustomEditor(typeof(ObjectRegistry))]
    public class ObjectRegistryEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
        }
    }
#endif
}
