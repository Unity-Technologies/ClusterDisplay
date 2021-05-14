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

                foreach (var objectAndRPCList in sceneObjectsRegistry.objects)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"Object: \"{objectAndRPCList.Key.GetType().Name}\" (RPC Count: {objectAndRPCList.Value.Count})", EditorStyles.boldLabel);
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.LabelField("RPCs:", EditorStyles.boldLabel);
                    foreach (var rpcId in objectAndRPCList.Value)
                    {
                        EditorGUILayout.BeginHorizontal();

                        if (GUILayout.Button("X", GUILayout.Width(20)))
                        {
                            objectToRemove = objectAndRPCList.Key;
                            rpcToRemove = rpcId;
                        }

                        EditorGUILayout.LabelField(rpcRegistry[rpcId].methodInfo.Name);
                        EditorGUILayout.EndHorizontal();
                    }
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
    }
}
