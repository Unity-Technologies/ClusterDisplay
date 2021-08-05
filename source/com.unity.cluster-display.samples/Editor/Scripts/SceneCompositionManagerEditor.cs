using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Unity.ClusterDisplay.Editor
{
    [CustomEditor(typeof(SceneCompositionManager))]
    public class SceneCompositionManagerEditor : UnityEditor.Editor
    {
        private IEnumerable<SceneComposition> cachedSceneCompositions;

        [MenuItem("Unity/Cluster Display/Scene Composition Manager")]
        public static void SelectSceneCompositionManager ()
        {
            string guid = AssetDatabase.FindAssets("t:SceneCompositionManager").FirstOrDefault();
            if (string.IsNullOrEmpty(guid))
            {
                Debug.LogError($"There is no: \"{nameof(SceneCompositionManager)}\" in the project, you will need to create one.");
                return;
            }

            Selection.objects = new[] { AssetDatabase.LoadAssetAtPath<SceneCompositionManager>(AssetDatabase.GUIDToAssetPath(guid)) };
        }

        private void Cache ()
        {
            cachedSceneCompositions = AssetDatabase.FindAssets("t:SceneComposition")
                .Select(guid => AssetDatabase.LoadAssetAtPath<SceneComposition>(AssetDatabase.GUIDToAssetPath(guid)));
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            var sceneCompositionManager = target as SceneCompositionManager;

            if (cachedSceneCompositions == null)
                Cache();

            if (GUILayout.Button("Refresh"))
                Cache();

            foreach (var sceneComposition in cachedSceneCompositions)
            {
                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
                EditorGUILayout.LabelField(sceneComposition.CompositionName, EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Select"))
                    Selection.objects = new Object[] { sceneComposition };
                if (GUILayout.Button("Stage"))
                    sceneComposition.Stage();
                if (GUILayout.Button("Open"))
                    sceneComposition.Open();
                EditorGUILayout.EndHorizontal();
            }
        }
    }
}
