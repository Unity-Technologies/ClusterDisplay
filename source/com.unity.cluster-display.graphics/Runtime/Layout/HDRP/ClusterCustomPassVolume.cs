using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Unity.ClusterDisplay.Graphics
{
    [RequireComponent(typeof(CustomPassVolume))]
    [ExecuteAlways]
    public class ClusterCustomPassVolume : SingletonMonoBehaviour<ClusterCustomPassVolume> 
    {
        private CustomPassVolume m_CustomPassVolume;
        public CustomPassVolume RenderingCustomPassVolume => m_CustomPassVolume;

        private StandardTileLayoutBuilderPass m_TilePass;

        /*
        public Vector4 ScaleBiasTex
        {
            get => m_TilePass != null ? m_TilePass.m_ScaleBiasTex : Vector4.zero;
            set
            {
                if (m_TilePass == null)
                    return;
                m_TilePass.m_ScaleBiasTex = value;
            }
        }
        */

        private bool TryGetTilePassFromCustomPassVolume (out StandardTileLayoutBuilderPass outTilePass)
        {
            StandardTileLayoutBuilderPass pass = null;
            bool hasPass = false;
            for (int i = 0; i < m_CustomPassVolume.customPasses.Count; i++)
            {
                if (m_CustomPassVolume.customPasses[i].GetType() != typeof(StandardTileLayoutBuilderPass))
                    continue;

                pass = m_CustomPassVolume.customPasses[i] as StandardTileLayoutBuilderPass;
                hasPass = true;
                break;
            }

            outTilePass = pass;
            return hasPass;
        }

        private void Awake()
        {
            if ((m_CustomPassVolume = gameObject.GetComponent<CustomPassVolume>()) == null)
                m_CustomPassVolume = gameObject.AddComponent<CustomPassVolume>();

            m_CustomPassVolume.injectionPoint = CustomPassInjectionPoint.AfterPostProcess;

            if (!TryGetTilePassFromCustomPassVolume(out m_TilePass))
            {
                m_CustomPassVolume.AddPassOfType(typeof(StandardTileLayoutBuilderPass));
                if (!TryGetTilePassFromCustomPassVolume(out m_TilePass))
                    throw new System.Exception($"Unable to add cluster display custom HDRP pass of type: {typeof(StandardTileLayoutBuilderPass).FullName}");
            }
        }
    }
}
