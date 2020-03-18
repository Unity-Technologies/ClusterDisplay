using System.Collections;
using System.Collections.Generic;
using UnityEngine;
/*
namespace Unity.ClusterRendering.Toolkit
{
    /// <summary>
    /// provides a set of utilities for runtime edits
    /// </summary>
    public class WorkflowUtils : MonoBehaviour, IDebugGUI
    {
        HashSet<Canvas> m_hiddenCanvases = new HashSet<Canvas>();
        private bool m_UIHidden = false;

        void Synchronize()
        {
            if (m_UIHidden)
                HideAllCanvases();
            else
                ShowHiddenCanvases();
        }

        void OnLevelWasLoaded(int level) 
        {
            Synchronize();
        }

        void OnEnable()
        {
            Synchronize();
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.U))
            {
                m_UIHidden = !m_UIHidden;
                Synchronize();
            }
        }

        public void AppendGUI()
        {
            if (GUILayout.Button("hide canvases"))
                HideAllCanvases();
            if (GUILayout.Button("show hidden canvases"))
                ShowHiddenCanvases();
        }

        void HideAllCanvases()
        {
            foreach (var canvas in FindObjectsOfType<Canvas>())
            {
                // HACK: we have introduced a component whose sole purpose is tagging canvases we don't want to hide
                if (canvas.GetComponent<StaticUITag>() != null)
                    continue;
                
                canvas.enabled = false;
                m_hiddenCanvases.Add(canvas);
            }
        }

        void ShowHiddenCanvases()
        {
            foreach (var canvas in m_hiddenCanvases)
                canvas.enabled = true;
            m_hiddenCanvases.Clear();
        }
    }
}
*/