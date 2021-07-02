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
                /*
                var sceneObjectsRegistry = target as SceneObjectsRegistry;
                var scene = sceneObjectsRegistry.gameObject.scene;

                if (!RPCRegistry.TryGetInstance(out var rpcRegistry, throwException: false))
                    return;

                Component instanceToRemove = null;
                ushort ? rpcToRemove = null;

                foreach (var instance in sceneObjectsRegistry.m_SceneInstances)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"Object: \"{instance.GetType().Name}\"", EditorStyles.boldLabel);
                    EditorGUILayout.EndHorizontal();

                }

                if (rpcToRemove != null)
                    sceneObjectsRegistry.Unregister(instanceToRemove);
                */
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
