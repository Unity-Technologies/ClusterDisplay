using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.UI;

namespace Unity.ClusterDisplay.Graphics
{
    public class StandardHDRPPresenter : HDRPPresenter
    {
        public override RTHandle TargetRT
        {
            get => m_RT;
            set
            {
                m_RT = value;
                if (m_Camera != m_RT)
                {
                    if (m_RT == null)
                        m_Camera.targetTexture = null;
                    else m_Camera.targetTexture = m_RT;
                }
            }
        }

        private RTHandle m_PresentRT;
        public override RTHandle PresentRT 
        { 
            get => m_PresentRT;
            set
            {
                if (m_ClusterCanvas == null)
                    return;

                m_PresentRT = value;
                m_ClusterCanvas.RawImageTexture = m_PresentRT;
            } 
        }

        // private ClusterCustomPassVolume m_ClusterCustomPassVolume;
        private ClusterCanvas m_ClusterCanvas;

        protected override void InitializeCamera(Camera camera)
        {
            /*
            if (!ClusterCustomPassVolume.TryGetInstance(out var clusterCustomPassVolume, displayError: false))
                m_ClusterCustomPassVolume = new GameObject("CustomPassVolume").AddComponent<ClusterCustomPassVolume>();
            else m_ClusterCustomPassVolume = clusterCustomPassVolume.GetComponent<ClusterCustomPassVolume>();
            */

            if (!ClusterCanvas.TryGetInstance(out var clusterCanvas, displayError: false))
                m_ClusterCanvas = new GameObject("ClusterCanvas").AddComponent<ClusterCanvas>();
            else m_ClusterCanvas = clusterCanvas.GetComponent<ClusterCanvas>();

            if (Application.isPlaying)
                Object.DontDestroyOnLoad(m_ClusterCanvas.gameObject);
            /*
            if (Application.isPlaying)
                Object.DontDestroyOnLoad(m_ClusterCustomPassVolume.gameObject);
            */
        }

        protected override void DeinitializeCamera(Camera camera)
        {
            /*
            if (m_ClusterCustomPassVolume != null)
            {
                if (Application.isPlaying)
                    Object.Destroy(m_ClusterCustomPassVolume.gameObject);
                else Object.DestroyImmediate(m_ClusterCustomPassVolume.gameObject);
            }
            */

            if (m_ClusterCanvas != null)
            {
                if (Application.isPlaying)
                    Object.Destroy(m_ClusterCanvas.gameObject);
                else Object.DestroyImmediate(m_ClusterCanvas.gameObject);
            }
        }

        public override void Dispose() => DeinitializeCamera(m_Camera);
    }
}
