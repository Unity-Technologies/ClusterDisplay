using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace Unity.FPS.UI
{
    public class LoadSceneButton : MonoBehaviour
    {
        public string SceneName = "";

        void Update()
        {
            if (EventSystem.current.currentSelectedGameObject == gameObject
                && Input.GetKeyDown(KeyCode.Return))
            {
                LoadTargetScene();
            }
        }

        public void LoadTargetScene()
        {
            SceneManager.LoadScene(SceneName);
        }
    }
}