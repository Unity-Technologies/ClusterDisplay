using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    /// <summary>
    /// The purpose of the <see cref="Presenter"/> is to automatically setup where
    /// renders are presented.
    /// </summary>
    class Presenter : IDisposable
    {
        // TODO Storing a ref to a texture managed externally is not ideal.
        RenderTexture m_RenderTexture;
        
        // TODO Temporary bookkeeping
        int m_FrameIndex;
        int m_CallsPerFrame;

        public bool bypass;
        
        public RenderTexture PresentRT
        {
            set => m_RenderTexture = value;
        }

        public void Initialize()
        {
            RenderPipelineManager.endFrameRendering += OnEndFrameRendering;
        }
        
        public void Dispose()
        {
            RenderPipelineManager.endFrameRendering -= OnEndFrameRendering;
        }

        void OnEndFrameRendering(ScriptableRenderContext context, Camera[] cameras)
        {
            if (bypass) return;
            
            var frameIndex = Time.frameCount;
            if (frameIndex != m_FrameIndex)
            {
                m_FrameIndex = frameIndex;
                m_CallsPerFrame = 0;
            }

            ++m_CallsPerFrame;

            if (m_CallsPerFrame > 1)
            {
                Debug.Log($"Present Called {m_CallsPerFrame} times during frame {m_FrameIndex}.");
            }
            
            if (m_RenderTexture != null)
            {
                UnityEngine.Graphics.Blit(m_RenderTexture, (RenderTexture)null);
            }
        }
    }
}
