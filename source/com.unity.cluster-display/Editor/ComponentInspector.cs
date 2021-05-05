using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;

namespace Unity.ClusterDisplay
{
    public class ComponentInspector : EditorWindow
    {
        private ClusterDisplayNetworkManager objectReferenceContainer;
        private Vector2 selectedObjectsScrollPosition;

        [MenuItem("Window/Cluster Display/Component Inspector")]
        public static void Open ()
        {
            var window = EditorWindow.CreateWindow<ComponentInspector>();
            window.Show();
        }

        private void OnGUI()
        {
            if (objectReferenceContainer == null && !ClusterDisplayNetworkManager.TryGetInstance(out objectReferenceContainer))
            {
                EditorGUILayout.LabelField($"There is no instance of \"{typeof(ClusterDisplayNetworkManager).FullName}\" in the scene.");
                return;
            }

            var components = Selection.objects
                .Where(obj => obj is GameObject)
                .SelectMany(obj => (obj as GameObject).GetComponents<Component>());

            EditorGUILayout.LabelField("Components", EditorStyles.boldLabel);
            if (components.Count() > 0)
            {
                selectedObjectsScrollPosition = EditorGUILayout.BeginScrollView(selectedObjectsScrollPosition, GUILayout.Height(150));

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.Space(10);

                EditorGUILayout.BeginVertical();
                foreach (var component in components)
                {
                    EditorGUILayout.BeginHorizontal();

                    if (GUILayout.Button("Select", GUILayout.Width(60)))
                    {
                        var componentScene = component.gameObject.scene;
                        if (!SceneObjectsRegistry.TryGetSceneInstance(componentScene.path, out var sceneObjectRegistry, throwException: false))
                            sceneObjectRegistry = SceneObjectsRegistry.CreateNewInstance(componentScene);

                        sceneObjectRegistry.RegisterObject(component);
                    }

                    EditorGUILayout.LabelField(component.GetType().Name);
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndVertical();

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndScrollView();
            }

            else
            {
                EditorGUILayout.LabelField("Select a GameObject");
            }
        }
    }
}
