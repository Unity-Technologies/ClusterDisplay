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
        private MonoBehaviourReflector cachedReflector;
        protected override void OnExtendInspectorGUI(MonoBehaviour instance)
        {
            TryGetReflectorInstance(instance, ref cachedReflector);

            if (GUILayout.Button(cachedReflector == null ? "Create Reflect" : "Remove Reflector"))
            {
                if (cachedReflector == null)
                {
                    cachedReflector = instance.gameObject.AddComponent<MonoBehaviourReflector>();
                    cachedReflector.Setup(instance);
                }

                else DestroyImmediate(cachedReflector);
            }
        }
    }

    [CustomEditor(typeof(Transform))]
    public class TransformExtension : UnityInspectorExtension<Transform>
    {
        private TransformReflector cachedReflector;
        protected override void OnExtendInspectorGUI(Transform instance)
        {
            TryGetReflectorInstance(instance, ref cachedReflector);

            if (GUILayout.Button(cachedReflector == null ? "Create Reflect" : "Remove Reflector"))
            {
                if (cachedReflector == null)
                {
                    cachedReflector = instance.gameObject.AddComponent<TransformReflector>();
                    cachedReflector.Setup(instance);
                }

                else DestroyImmediate(cachedReflector);
            }
        }
    }
}
