using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class ReloadScene : MonoBehaviour
{
    #if UNITY_EDITOR
    [CustomEditor(typeof(ReloadScene))]
    private class ReloadSceneEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            var instance = target as ReloadScene;

            EditorGUILayout.TextArea(
                "TO TEST OR BUILD:\n\t1. Stage this scene using the \"Scene Composition Manager\"\n\t2. Set the \"This Scene\" field with the staged scene.\n\t3. CLick the button below.\n\t4. Test or make a build.");
            
            if (GUILayout.Button("Add Scenes to Build List"))
            {
                instance.CacheScenePath();
                
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(instance.thisScenePath) != null &&
                    AssetDatabase.LoadAssetAtPath<SceneAsset>(instance.additionScenePath) != null)
                {
                    EditorBuildSettings.scenes = new EditorBuildSettingsScene[]
                    {
                        new EditorBuildSettingsScene(instance.thisScenePath, true),
                        new EditorBuildSettingsScene(instance.additionScenePath, true),
                    };
                }
            }
        }
    }
    
    [SerializeField] private SceneAsset thisScene;
    [SerializeField] private SceneAsset additionScene;

    private void CacheScenePath()
    {
        if (thisScene != null)
            thisScenePath = AssetDatabase.GetAssetPath(thisScene);
        
        if (additionScene != null)
            additionScenePath = AssetDatabase.GetAssetPath(additionScene);
    }

    private void OnValidate() => CacheScenePath();
    #endif
    
    [SerializeField][HideInInspector] private string thisScenePath;
    [SerializeField][HideInInspector] private string additionScenePath;

    private void Awake() => StartCoroutine(TestSceneLoading());

    private IEnumerator TestSceneLoading ()
    {
        var waitToReloadThisScene = new WaitForSeconds(1);
        var waitToLoadAdditionScene = new WaitForSeconds(2);
        var waitToUnloadAdditionScene = new WaitForSeconds(5);

        string thisSceneName = Path.GetFileNameWithoutExtension(thisScenePath);
        string additionSceneName = Path.GetFileNameWithoutExtension(additionScenePath);
        
        while (true)
        {
            yield return waitToLoadAdditionScene;
            SceneManager.LoadSceneAsync(additionSceneName, LoadSceneMode.Additive);
            
            yield return waitToUnloadAdditionScene;
            SceneManager.UnloadSceneAsync(additionSceneName);
            
            yield return waitToReloadThisScene;
            SceneManager.LoadScene(thisSceneName, LoadSceneMode.Single);
        }
    }
}
