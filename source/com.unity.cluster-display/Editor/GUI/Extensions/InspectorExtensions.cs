using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

using Unity.ClusterDisplay.Networking;

namespace Unity.ClusterDisplay.Editor.Extensions
{
    [CustomEditor(typeof(MonoBehaviour), editorForChildClasses: true)]
    public class MonoBehaviourExtension : UserInspectorExtension<MonoBehaviour>
    {
        public MonoBehaviourExtension() : base(useDefaultInspector: true) {}

        private MonoBehaviourReflector cachedReflector;
        protected override void OnExtendInspectorGUI(MonoBehaviour instance)
        {
            var type = instance.GetType();
            bool isPostProcessable = ReflectionUtils.TypeIsInPostProcessableAssembly(type);
            if (isPostProcessable)
            {
                if (ListFields(instance, out var selectedField, out var selectedState))
                {
                }

                if (ListProperties(instance, out var selectedProperty, out selectedState))
                {
                }

                if (ListMethods(instance, out var selectedMethodInfo, out selectedState))
                {
                }
            }

            else
            {
                TryGetReflectorInstance(instance, ref cachedReflector);
                ReflectorButton(instance, ref cachedReflector);
                if (cachedReflector == null)
                    return;
            }
        }
    }

    [CustomEditor(typeof(Transform))]
    public class TransformExtension : UnityInspectorExtension<Transform>
    {
        public TransformExtension() : base(useDefaultInspector: true) {}

        private TransformReflector cachedReflector;
        protected override void OnExtendInspectorGUI(Transform instance)
        {
            TryGetReflectorInstance(instance, ref cachedReflector);
            ReflectorButton(instance, ref cachedReflector);

            if (cachedReflector == null)
                return;

            var reflectorSerializedObject = new SerializedObject(cachedReflector);
            reflectorSerializedObject.Update();

            var modeProperty = reflectorSerializedObject.FindProperty("m_Mode");
            EditorGUILayout.PropertyField(modeProperty);

            reflectorSerializedObject.ApplyModifiedProperties();
        }
    }
}
