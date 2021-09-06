using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Unity.FPS.UI
{
    public class ToggleGameObjectButton : MonoBehaviour
    {
        public GameObject ObjectToToggle;
        public bool ResetSelectionAfterClick;

        void Update()
        {
            if (ObjectToToggle.activeSelf && Input.GetKeyDown(KeyCode.Backspace))
                SetGameObjectActive(false);
        }

        public void SetGameObjectActive(bool active)
        {
            ObjectToToggle.SetActive(active);

            if (ResetSelectionAfterClick)
                EventSystem.current.SetSelectedGameObject(null);
        }
    }
}