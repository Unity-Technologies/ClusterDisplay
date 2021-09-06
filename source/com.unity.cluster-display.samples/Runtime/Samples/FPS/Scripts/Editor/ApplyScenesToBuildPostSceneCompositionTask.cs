using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Unity.ClusterDisplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

[CreateAssetMenu(fileName = "ApplyScenesToBuild", menuName = "Cluster Display/Scriptable Objects/Apply Scenes To Build")]
public class ApplyScenesToBuildPostSceneCompositionTask : PostSceneCompositionTask
{
    [SerializeField] private List<SceneAsset> additionalScenesToAddToBuild = new List<SceneAsset>();

    public override void Execute(SceneAsset sceneAsset, Scene openedScene)
    {
        List<SceneAsset> listCopy = new List<SceneAsset>() { sceneAsset };
        listCopy.AddRange(additionalScenesToAddToBuild.Where(sa => sa != null));

        var settings = listCopy.Select(sa => new EditorBuildSettingsScene(AssetDatabase.GetAssetPath(sa), true));
        EditorBuildSettings.scenes = settings.ToArray();
    }
}
