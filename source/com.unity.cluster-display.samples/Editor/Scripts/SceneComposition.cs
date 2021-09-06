using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.ClusterDisplay
{
    [CreateAssetMenu(fileName = "SceneComposition", menuName = "Cluster Display/Scriptable Objects/Scene Composition")]
    public class SceneComposition : ScriptableObject
    {
        [SerializeField] private string compositionName = "StagedComposition";
        public string CompositionName => compositionName;

        [SerializeField] private SceneAsset baseScene = null;
        [SerializeField] private List<SceneAsset> additions = new List<SceneAsset>();
        [SerializeField] private List<PostSceneCompositionTask> postSceneCompositionTasks = new List<PostSceneCompositionTask>();

        public string StagedScenePath => $"Assets/Cluster Display/Scenes/Staged/{compositionName}.unity";

        public SceneAsset Stage ()
        {
            AssetDatabase.DeleteAsset(StagedScenePath);

            Scene stageScene = EditorSceneManager.OpenScene(AssetDatabase.GetAssetPath(baseScene), OpenSceneMode.Single);
            Scene[] scenesToMerge = new Scene[additions.Count]; 
            for (int i = 0; i < additions.Count; i++)
                scenesToMerge[i] = EditorSceneManager.OpenScene(AssetDatabase.GetAssetPath(additions[i]), OpenSceneMode.Additive);

            for (int i = 0; i < scenesToMerge.Length; i++)
            {
                EditorSceneManager.MergeScenes(scenesToMerge[i], stageScene);
                EditorSceneManager.CloseScene(scenesToMerge[i], false);
            }

            if (!Directory.Exists(Path.GetDirectoryName(StagedScenePath)))
                Directory.CreateDirectory(Path.GetDirectoryName(StagedScenePath));

            EditorSceneManager.SaveScene(stageScene, StagedScenePath);

            AssetDatabase.Refresh();

            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(StagedScenePath);
            for (int i = 0; i < postSceneCompositionTasks.Count; i++)
            {
                if (postSceneCompositionTasks[i] == null)
                    continue;
                postSceneCompositionTasks[i].Execute(sceneAsset, stageScene);
            }

            return sceneAsset;
        }

        public void Open ()
        {
            EditorSceneManager.OpenScene(AssetDatabase.GetAssetPath(baseScene), OpenSceneMode.Single);
            for (int i = 0; i < additions.Count; i++)
                EditorSceneManager.OpenScene(AssetDatabase.GetAssetPath(additions[i]), OpenSceneMode.Additive);
        }
    }
}
