using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics.Tests.Universal
{
    public class ClusterRendererTest : MonoBehaviour
    {
        static readonly string k_ShaderName ="Hidden/ClusterDisplay/Tests/Blit";
        static Material s_BlitMaterial;

        static Material GetBlitMaterial()
        {
            if (s_BlitMaterial == null)
            {
                s_BlitMaterial = CoreUtils.CreateEngineMaterial(k_ShaderName);
            }

            return s_BlitMaterial;
        }

        class StitcherCapture : ICapturePresent, IDisposable
        {
            CommandBuffer m_CommandBuffer;
            RenderTexture m_Target;
            ClusterRenderer m_ClusterRenderer;

            public StitcherCapture(ClusterRenderer clusterRenderer, RenderTexture target)
            {
                m_ClusterRenderer = clusterRenderer;
                m_Target = target;
                m_CommandBuffer = CommandBufferPool.Get("Capture Stitcher.");
                m_ClusterRenderer.AddCapturePresent(this);
            }

            public void Dispose()
            {
                m_ClusterRenderer.RemoveCapturePresent(this);
                CommandBufferPool.Release(m_CommandBuffer);
            }
            
            public void OnBeginCapture()
            {
                m_CommandBuffer.SetRenderTarget(m_Target);
            }

            public void OnEndCapture()
            {
                UnityEngine.Graphics.ExecuteCommandBuffer(m_CommandBuffer);
                m_CommandBuffer.Clear();
            }

            public CommandBuffer GetCommandBuffer() => m_CommandBuffer;
        }

        class CameraCapture : IDisposable
        {
            Camera m_Camera;
            RenderTexture m_Target;
            
            public CameraCapture(Camera camera, RenderTexture target)
            {
                m_Camera = camera;
                m_Target = target;
                CameraCaptureBridge.enabled = true;
                CameraCaptureBridge.AddCaptureAction(m_Camera, CaptureAction);
            }

            public void Dispose()
            {
                CameraCaptureBridge.RemoveCaptureAction(m_Camera, CaptureAction);
                CameraCaptureBridge.enabled = false;
            }
            
            void CaptureAction(RenderTargetIdentifier source, CommandBuffer cb)
            {
                /*if (source == BuiltinRenderTextureType.CurrentActive)
                {
                    var tid = Shader.PropertyToID("_MainTex");
                    cb.GetTemporaryRT(tid, m_Capture.width, m_Capture.height, 0, FilterMode.Bilinear);
                    cb.Blit(source, tid);
                    cb.Blit(tid, m_Capture, GetBlitMaterial());
                    cb.ReleaseTemporaryRT(tid);
                }*/
            
                Assert.IsFalse(source == BuiltinRenderTextureType.CurrentActive);
                cb.Blit(source, m_Target, GetBlitMaterial());
            }
        }
        
        [SerializeField]
        Camera m_Camera;

        [SerializeField]
        ClusterRenderer m_ClusterRenderer;
        
        RenderTexture m_VanillaCapture;
        RenderTexture m_StitcherCapture;
        
        void OnGUI()
        {
            if (m_VanillaCapture != null)
            {
                GUI.DrawTexture(new Rect(0, 0, 256, 256), m_VanillaCapture);
            }
            
            if (m_StitcherCapture != null)
            {
                GUI.DrawTexture(new Rect(256, 0, 256, 256), m_StitcherCapture);
            }
        }

        void OnDisable()
        {
            GraphicsUtil.DeallocateIfNeeded(ref m_VanillaCapture);
            GraphicsUtil.DeallocateIfNeeded(ref m_StitcherCapture);
            StopAllCoroutines();
        }

        [ContextMenu("Run Test")]
        void RunTest()
        {
            StartCoroutine(CompareVanillaAndStitchedCluster());
        }
    
        // [UnityTest]
        IEnumerator CompareVanillaAndStitchedCluster()
        {
            Assert.IsNotNull(m_Camera, $"{nameof(m_Camera)} not assigned.");
            Assert.IsNotNull(m_ClusterRenderer, $"{nameof(m_ClusterRenderer)} not assigned.");
            
            // We assume there's no resize during the execution.
            // TODO Maybe force GameView display and size.
            
            var format = SystemInfo.GetGraphicsFormat(DefaultFormat.LDR);
            GraphicsUtil.AllocateIfNeeded(ref m_VanillaCapture, m_Camera.pixelWidth, m_Camera.pixelHeight, format);
            GraphicsUtil.AllocateIfNeeded(ref m_StitcherCapture, m_Camera.pixelWidth, m_Camera.pixelHeight, format);

            m_ClusterRenderer.gameObject.SetActive(true);
            m_ClusterRenderer.enabled = false;

            // TODO Could add tests of the ClusterRenderer control of the Camera state. Separately.
            m_Camera.gameObject.SetActive(true);
            m_Camera.enabled = true;
            
            // Perform a capture of vanilla output
            using (new CameraCapture(m_Camera, m_VanillaCapture))
            {
                // Let a capture of the vanilla output happen.
                yield return null;
            }
            
            m_ClusterRenderer.enabled = true;

            using (new StitcherCapture(m_ClusterRenderer, m_StitcherCapture))
            {
                // Let a capture of the stitcher output happen.
                // TODO Figure out why 2 frames are needed.
                yield return null;
                yield return null;
            }

            // Compare the two.

            yield return null;
        }
    }
}
