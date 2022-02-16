using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools.Graphics;

namespace Unity.ClusterDisplay.Graphics.Tests
{
    public class BaseDelayPresentationTest : MonoBehaviour
    {
        struct MoveAround
        {
            readonly Vector3 m_Up;
            readonly Vector3 m_Forward;
            readonly Vector3 m_Position;
            readonly float m_Amount;

            public MoveAround(Transform transform, float amount)
            {
                m_Up = transform.up;
                m_Forward = transform.forward;
                m_Position = transform.position;
                m_Amount = amount;
            }

            public Vector3 Update(float angle)
            {
                return m_Position + Quaternion.AngleAxis(angle, m_Forward) * m_Up * m_Amount;
            }
        }

        const int k_NumFrames = 12;

        protected Camera m_Camera;
        protected ClusterRenderer m_ClusterRenderer;
        protected RenderTexture[] m_ClusterCaptureNoDelay;
        protected RenderTexture[] m_ClusterCaptureDelayed;
        protected Texture2D m_ClusterCaptureNoDelayTex2D;
        protected Texture2D m_ClusterCaptureDelayedTex2D;
        protected GraphicsTestSettings m_GraphicsTestSettings;

        protected virtual void InitializeTest()
        {
            m_Camera = GraphicsTestUtil.FindUniqueMainCamera();
            Assert.IsNotNull(m_Camera, "Could not find main camera.");
            m_ClusterRenderer = FindObjectOfType<ClusterRenderer>(true);
            Assert.IsNotNull(m_ClusterRenderer, $"Could not find {nameof(ClusterRenderer)}");
            m_GraphicsTestSettings = FindObjectOfType<GraphicsTestSettings>();
            Assert.IsNotNull(m_GraphicsTestSettings, "Missing test settings for graphic tests.");

            var success = EditorBridge.SetGameViewSize(
                m_GraphicsTestSettings.ImageComparisonSettings.TargetWidth,
                m_GraphicsTestSettings.ImageComparisonSettings.TargetHeight);
            Assert.IsTrue(success);
        }

        void DisposeTest()
        {
            CoreUtils.Destroy(m_ClusterCaptureNoDelayTex2D);
            CoreUtils.Destroy(m_ClusterCaptureDelayedTex2D);
            GraphicsUtil.DeallocateIfNeeded(ref m_ClusterCaptureNoDelay);
            GraphicsUtil.DeallocateIfNeeded(ref m_ClusterCaptureDelayed);
        }

        protected IEnumerator RenderAndCompareSequence()
        {
            InitializeTest();

            yield return GraphicsTestUtil.PreWarm();

            Assert.IsNotNull(m_Camera, $"{nameof(m_Camera)} not assigned.");
            Assert.IsNotNull(m_ClusterRenderer, $"{nameof(m_ClusterRenderer)} not assigned.");
            Assert.IsTrue(m_ClusterRenderer.isActiveAndEnabled);

            var moveAround = new MoveAround(m_Camera.transform, 4);
            var width = m_GraphicsTestSettings.ImageComparisonSettings.TargetWidth;
            var height = m_GraphicsTestSettings.ImageComparisonSettings.TargetHeight;

            GraphicsUtil.AllocateIfNeeded(ref m_ClusterCaptureNoDelay, k_NumFrames, width, height, "vanilla-capture");
            GraphicsUtil.AllocateIfNeeded(ref m_ClusterCaptureDelayed, k_NumFrames, width, height, "cluster-capture");

            m_ClusterCaptureNoDelayTex2D = new Texture2D(width, height);
            m_ClusterCaptureDelayedTex2D = new Texture2D(width, height);

            {
                m_ClusterRenderer.DelayPresentByOneFrame = false;

                for (var i = 0; i != k_NumFrames; ++i)
                {
                    m_Camera.transform.position = moveAround.Update(i * 397);

                    yield return GraphicsTestUtil.DoScreenCapture(m_ClusterCaptureNoDelay[i]);
                }
            }

            {
                m_ClusterRenderer.DelayPresentByOneFrame = true;

                for (var i = 0; i != k_NumFrames; ++i)
                {
                    m_Camera.transform.position = moveAround.Update(i * 397);

                    yield return GraphicsTestUtil.DoScreenCapture(m_ClusterCaptureDelayed[i]);
                }
            }

            /*for (var i = 0; i != k_NumFrames; ++i)
            {
                GraphicsTestUtil.CopyToTexture2D(m_ClusterCaptureNoDelay[i], m_ClusterCaptureNoDelayTex2D);
                GraphicsTestUtil.CopyToTexture2D(m_ClusterCaptureDelayed[i], m_ClusterCaptureDelayedTex2D);

                GraphicsTestUtil.SaveAsPNG(m_ClusterCaptureNoDelayTex2D, $"no-delay-{i}");
                GraphicsTestUtil.SaveAsPNG(m_ClusterCaptureDelayedTex2D, $"delayed-{i}");
            }*/

            // Make sure the cluster output is delayed by one frame.
            for (var i = 0; i != k_NumFrames - 1; ++i)
            {
                GraphicsTestUtil.CopyToTexture2D(m_ClusterCaptureNoDelay[i], m_ClusterCaptureNoDelayTex2D);
                GraphicsTestUtil.CopyToTexture2D(m_ClusterCaptureDelayed[i + 1], m_ClusterCaptureDelayedTex2D);
                
                ImageAssert.AreEqual(m_ClusterCaptureNoDelayTex2D, m_ClusterCaptureDelayedTex2D, m_GraphicsTestSettings.ImageComparisonSettings);
            }
            
            // Make sure the test did not pass by accident. (Aka, the frames did change over time.)
            for (var i = 0; i != k_NumFrames; ++i)
            {
                GraphicsTestUtil.CopyToTexture2D(m_ClusterCaptureNoDelay[i], m_ClusterCaptureNoDelayTex2D);
                GraphicsTestUtil.CopyToTexture2D(m_ClusterCaptureDelayed[i], m_ClusterCaptureDelayedTex2D);

                GraphicsTestUtil.AssertImagesAreNotEqual(m_ClusterCaptureNoDelayTex2D, m_ClusterCaptureDelayedTex2D, m_GraphicsTestSettings.ImageComparisonSettings);
            }
            
            DisposeTest();
        }
    }
}
