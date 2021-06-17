using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Unity.ClusterDisplay
{
    public partial class SceneObjectsRegistry : SceneSingletonMonoBehaviour<SceneObjectsRegistry>
    {
#if UNITY_EDITOR
        [CustomEditor(typeof(SceneObjectsRegistry))]
        public class SceneObjectsRegistryEditor : Editor
        {
            public void PresentGUI ()
            {
                var sceneObjectsRegistry = target as SceneObjectsRegistry;
                var scene = sceneObjectsRegistry.gameObject.scene;

                if (!RPCRegistry.TryGetInstance(out var rpcRegistry, throwException: false))
                    return;


                Object objectToRemove = null;
                ushort ? rpcToRemove = null;

                foreach (var obj in sceneObjectsRegistry.sceneObjects)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"Object: \"{obj.GetType().Name}\"", EditorStyles.boldLabel);
                    EditorGUILayout.EndHorizontal();

                    /*
                    EditorGUILayout.LabelField("RPCs:", EditorStyles.boldLabel);
                    foreach (var rpcId in obj.Value)
                    {
                        EditorGUILayout.BeginHorizontal();

                        if (GUILayout.Button("X", GUILayout.Width(20)))
                        {
                            objectToRemove = obj.Key;
                            rpcToRemove = rpcId;
                        }

                        if (!rpcRegistry.TryGetRPC(rpcId, out var rpcMethodInfo))
                            continue;

                        var methodInfo = rpcMethodInfo.methodInfo;
                        EditorGUILayout.LabelField(methodInfo.Name);
                        EditorGUILayout.EndHorizontal();
                    }
                    */
                }

                if (rpcToRemove != null)
                    sceneObjectsRegistry.Unregister(objectToRemove, rpcToRemove.Value);
            }

            public override void OnInspectorGUI()
            {
                base.OnInspectorGUI();
                PresentGUI();
            }
        }
#endif
    }
}
