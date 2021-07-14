using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.ClusterDisplay.RPC
{
    public class ComponentInspector : EditorWindow
    {
        private Vector2 selectedObjectsScrollPosition;
        private Vector2 selectedObjectMethodsScrollPosition;

        private MethodInfo[] cachedMethods;

        private Object targetObject;
        private System.Type targetObjectType;

        private string methodSearchStr;

        [MenuItem("Window/Cluster Display/Component Inspector")]
        public static void Open ()
        {
            var window = EditorWindow.CreateWindow<ComponentInspector>();
            window.Show();
        }
        
        private void OnChangeSearch (string newMethodSearchStr)
        {
            cachedMethods = ReflectionUtils.GetMethodsWithRPCCompatibleParamters(
                targetObjectType,
                newMethodSearchStr);

            methodSearchStr = newMethodSearchStr;
        }

        private void OnSelectMethod (MethodInfo selectedMethodInfo)
        {
            var existsInScene = false;
            Scene ? scene = null;

            if (targetObject is GameObject)
            {
                scene = (targetObject as GameObject).scene;
                existsInScene = scene.HasValue ? scene.Value.IsValid() : false;
            }
            else if (targetObject is Component)
            {
                scene = (targetObject as Component).gameObject.scene;
                existsInScene = scene.HasValue ? scene.Value.IsValid() : false;
            }

            if (existsInScene)
            {
                if (!SceneObjectsRegistry.TryGetSceneInstance(scene.Value.path, out var sceneObjectsRegistry, throwException: false))
                    SceneObjectsRegistry.TryCreateNewInstance(scene.Value, out sceneObjectsRegistry);

                if (sceneObjectsRegistry != null)
                    RPCRegistry.TryAddNewRPC(selectedMethodInfo);
            }
        }

        private void ListObjects (IEnumerable<Object> objects)
        {
            selectedObjectsScrollPosition = EditorGUILayout.BeginScrollView(selectedObjectsScrollPosition, GUILayout.Height(250));

            foreach (var obj in objects)
            {
                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("Select", GUILayout.Width(45)))
                {
                    targetObject = obj;
                    targetObjectType = targetObject.GetType();
                    OnChangeSearch(methodSearchStr);
                }

                var objType = obj.GetType();
                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField($"Object Name: \"{obj.name}\"");
                EditorGUILayout.LabelField($"Object Type: \"{objType.Name}\"", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Namespace:   \"{objType.Namespace}\"");
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();
                RPCEditorGUICommon.HorizontalLine();
            }

            EditorGUILayout.EndScrollView();
        }

        private void PresentSelectedComponent ()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("X", GUILayout.Width(25)))
            {
                targetObject = null;
                targetObjectType = null;
                cachedMethods = null;
            }

            else
            {
                EditorGUILayout.LabelField($"Selected Object: \"{targetObjectType.FullName}\"", EditorStyles.boldLabel);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void ListMethods ()
        {
            RPCEditorGUICommon.ListMethods(
                $"Object: \"{targetObject.GetType().FullName}\" Methods:",
                cachedMethods,
                methodSearchStr,
                ref selectedObjectMethodsScrollPosition,
                OnChangeSearch,
                OnSelectMethod);
        }

        private void OnGUI()
        {
            /*
            if (objectReferenceContainer == null && !ClusterDisplayNetworkManager.TryGetInstance(out objectReferenceContainer, throwException: false))
            {
                EditorGUILayout.LabelField($"Create instance of \"{nameof(ClusterDisplayNetworkManager)}\"");
                return;
            }
            */

            var selectedObjects =  Selection.objects;
            var objects = selectedObjects
                .Where(obj => obj.GetType().Assembly.GetName().Name == ReflectionUtils.DefaultUserAssemblyName)
                .Concat(selectedObjects
                    .Where(obj => obj is GameObject)
                    .SelectMany(obj => (obj as GameObject).GetComponents<Component>() as Object[]))
                    .Where(obj => obj.GetType().Assembly.GetName().Name == ReflectionUtils.DefaultUserAssemblyName);

            EditorGUILayout.LabelField("Selected Objects", EditorStyles.boldLabel);

            if (objects.Count() > 0)
            {
                RPCEditorGUICommon.HorizontalLine();
                ListObjects(objects);
                RPCEditorGUICommon.HorizontalLine();

                if (targetObject != null && targetObjectType != null)
                {
                    PresentSelectedComponent();
                    ListMethods();
                }

                else
                {
                    EditorGUILayout.LabelField("Select an Object from the list above.");
                    EditorGUILayout.Space(150);
                }
            }

            else
            {
                EditorGUILayout.LabelField("Select an Object");
                EditorGUILayout.Space(150);

                targetObject = null;
                targetObjectType = null;
            }

            var sceneObjectRegistry = FindObjectsOfType<SceneObjectsRegistry>();
            for (int i = 0; i < sceneObjectRegistry.Length; i++)
            {
                RPCEditorGUICommon.HorizontalLine();
                var editor = UnityEditor.Editor.CreateEditor(sceneObjectRegistry[i], typeof(SceneObjectsRegistry.SceneObjectsRegistryEditor)) as SceneObjectsRegistry.SceneObjectsRegistryEditor;
                editor.PresentGUI();
            }

            RPCEditorGUICommon.HorizontalLine();
        }
    }
}
