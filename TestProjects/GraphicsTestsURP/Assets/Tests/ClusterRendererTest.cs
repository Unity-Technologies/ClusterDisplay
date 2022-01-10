using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics.Tests.Universal
{
    [ExecuteAlways]
    public class ClusterRendererTest : MonoBehaviour
    {
        [SerializeField]
        Camera m_Camera;

        [SerializeField]
        ClusterRenderer m_ClusterRenderer;

        RTHandle m_VanillaCaptureHandle;
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
            Blitter_.Dispose();
        }

        [ContextMenu("Run Test")]
        void RunTest()
        {
            StartCoroutine(CompareVanillaAndStitchedCluster());
        }

        // [UnityTest]
        IEnumerator CompareVanillaAndStitchedCluster()
        {
            Blitter_.InitializeifNeeded();

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
                yield return new WaitForEndOfFrame();
            }

            m_ClusterRenderer.enabled = true;

            using (new StitcherCapture(m_ClusterRenderer, m_StitcherCapture))
            {
                // Let a capture of the stitcher output happen.
                // TODO Figure out why 2 frames are needed.
                yield return new WaitForEndOfFrame();
            }

            // Compare the two.

            yield return null;
        }

        IEnumerator Wait()
        {
            yield return null;
            yield return null;
        }
    }
}
