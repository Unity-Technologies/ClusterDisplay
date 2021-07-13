using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

using Unity.ClusterDisplay.RPC;

namespace Unity.ClusterDisplay.Editor.Extensions
{
    public abstract class UserReflectorInspectorExtension<InstanceType, ReflectorType> : UserInspectorExtension<InstanceType>
        where InstanceType : MonoBehaviour
        where ReflectorType : ComponentReflector<InstanceType>
    {
        protected ReflectorType cachedReflector;

        protected override void OnPollReflectorGUI (InstanceType instance, bool hasRegistered)
        {
            if (!ReflectionUtils.TypeIsInPostProcessableAssembly(instance.GetType()))
            {
                TryGetReflectorInstance(instance, ref cachedReflector);
                if (hasRegistered && cachedReflector == null)
                    ReflectorButton(instance, ref cachedReflector);
            }
        }
    }

    public abstract class UnityReflectorInspectorExtension<InstanceType, ReflectorType> : UnityInspectorExtension<InstanceType>
        where InstanceType : Component
        where ReflectorType : ComponentReflector<InstanceType>
    {
        protected ReflectorType cachedReflector;

        protected override void OnPollReflectorGUI (InstanceType instance, bool anyStreamablesRegistered)
        {
            TryGetReflectorInstance(instance, ref cachedReflector);
            if (anyStreamablesRegistered && cachedReflector == null)
                ReflectorButton(instance, ref cachedReflector);
        }
    }

    [CustomEditor(typeof(MonoBehaviour), editorForChildClasses: true)]
    public class MonoBehaviourExtension : UserReflectorInspectorExtension<MonoBehaviour, MonoBehaviourReflector>
    {
        protected override void OnExtendInspectorGUI(MonoBehaviour instance) {}
    }

    [CustomEditor(typeof(Transform))]
    public class TransformExtension : UnityReflectorInspectorExtension<Transform, TransformReflector>
    {
        protected override void OnExtendInspectorGUI(Transform instance)
        {
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
