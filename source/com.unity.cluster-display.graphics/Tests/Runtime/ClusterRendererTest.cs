using System;
using System.Collections;
using NUnit.Framework;
using Unity.ClusterDisplay.Graphics.EditorTests;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools.Graphics;

namespace Unity.ClusterDisplay.Graphics.Tests
{
    public class ClusterRendererTest : MonoBehaviour
    {
        static string k_GameViewSizeName = "Graphic Test";
        const string k_MainCameraTag = "MainCamera";

        protected Camera m_Camera;
        protected ClusterRenderer m_ClusterRenderer;
        protected RenderTexture m_VanillaCapture;
        protected RenderTexture m_ClusterCapture;
        protected Texture2D m_VanillaCaptureTex2D;
        protected Texture2D m_VanillaCapture2Tex2D; // Used when making sure camera is restored properly.
        protected Texture2D m_ClusterCaptureTex2D;
        protected GraphicsTestSettings m_GraphicsTestSettings;
        
        protected virtual void InitializeTest()
        {
            m_Camera = FindUniqueMainCamera();
            Assert.IsNotNull(m_Camera, "Could not find main camera.");
            m_ClusterRenderer = FindObjectOfType<ClusterRenderer>(true);
            Assert.IsNotNull(m_ClusterRenderer, $"Could not find {nameof(ClusterRenderer)}");
            m_GraphicsTestSettings = FindObjectOfType<GraphicsTestSettings>();
            Assert.IsNotNull(m_GraphicsTestSettings, "Missing test settings for graphic tests.");
            
            SetGameViewSize(
                m_GraphicsTestSettings.ImageComparisonSettings.TargetWidth, 
                m_GraphicsTestSettings.ImageComparisonSettings.TargetHeight);
        }

        protected virtual void DisposeTest()
        {
            CoreUtils.Destroy(m_VanillaCaptureTex2D);
            CoreUtils.Destroy(m_VanillaCapture2Tex2D);
            CoreUtils.Destroy(m_ClusterCaptureTex2D);
            GraphicsUtil.DeallocateIfNeeded(ref m_VanillaCapture);
            GraphicsUtil.DeallocateIfNeeded(ref m_ClusterCapture);
        }
        
        protected IEnumerator RenderVanillaAndOverscan()
        {
            Assert.IsNotNull(m_Camera, $"{nameof(m_Camera)} not assigned.");
            Assert.IsNotNull(m_ClusterRenderer, $"{nameof(m_ClusterRenderer)} not assigned.");

            GraphicsUtil.AllocateIfNeeded(ref m_VanillaCapture, m_Camera.pixelWidth, m_Camera.pixelHeight);
            GraphicsUtil.AllocateIfNeeded(ref m_ClusterCapture, m_Camera.pixelWidth, m_Camera.pixelHeight);

            m_VanillaCaptureTex2D = new Texture2D(m_Camera.pixelWidth, m_Camera.pixelHeight);
            m_ClusterCaptureTex2D = new Texture2D(m_Camera.pixelWidth, m_Camera.pixelHeight);

            // First we render "vanilla", that is, without Cluster Display.
            m_ClusterRenderer.gameObject.SetActive(false);

            m_Camera.gameObject.SetActive(true);
            m_Camera.enabled = true;

            using (new CameraCapture(m_Camera, m_VanillaCapture))
            {
                // Let a capture of the vanilla output happen.
                yield return new WaitForEndOfFrame();
            }

            // Then we activate Cluster Display.
            m_ClusterRenderer.gameObject.SetActive(true);

            Assert.IsNotNull(m_ClusterRenderer.PresentCamera);

            using (new CameraCapture(m_ClusterRenderer.PresentCamera, m_ClusterCapture))
            {
                // Let a capture of the stitched output happen.
                yield return new WaitForEndOfFrame();
            }
            
            m_ClusterRenderer.gameObject.SetActive(false);
        }

        protected IEnumerator CameraIsRestoredProperly(
            Action preRender = null, 
            Action postRender = null)
        {
            InitializeTest();
            
            if (preRender != null)
            {
                preRender.Invoke();
            }
            
            Assert.IsNotNull(m_Camera, $"{nameof(m_Camera)} not assigned.");
            Assert.IsNotNull(m_ClusterRenderer, $"{nameof(m_ClusterRenderer)} not assigned.");

            GraphicsUtil.AllocateIfNeeded(ref m_VanillaCapture, m_Camera.pixelWidth, m_Camera.pixelHeight);
            GraphicsUtil.AllocateIfNeeded(ref m_ClusterCapture, m_Camera.pixelWidth, m_Camera.pixelHeight);

            m_VanillaCaptureTex2D = new Texture2D(m_Camera.pixelWidth, m_Camera.pixelHeight);
            m_VanillaCapture2Tex2D = new Texture2D(m_Camera.pixelWidth, m_Camera.pixelHeight);
            m_ClusterCaptureTex2D = new Texture2D(m_Camera.pixelWidth, m_Camera.pixelHeight);
            
            // First we render "vanilla", that is, without Cluster Display.
            m_ClusterRenderer.gameObject.SetActive(false);

            m_Camera.gameObject.SetActive(true);
            m_Camera.enabled = true;

            using (new CameraCapture(m_Camera, m_VanillaCapture))
            {
                // Let a capture of the vanilla output happen.
                yield return new WaitForEndOfFrame();
            }
            
            // Then we activate Cluster Display.
            m_ClusterRenderer.gameObject.SetActive(true);

            Assert.IsNotNull(m_ClusterRenderer.PresentCamera);

            using (new CameraCapture(m_ClusterRenderer.PresentCamera, m_ClusterCapture))
            {
                // Let a capture of the stitched output happen.
                yield return new WaitForEndOfFrame();
            }
            
            // We expect vanilla and cluster output to be different.
            CopyToTexture2D(m_VanillaCapture, m_VanillaCaptureTex2D);
            CopyToTexture2D(m_ClusterCapture, m_ClusterCaptureTex2D);

            AssertImagesAreNotEqual(m_VanillaCaptureTex2D, m_ClusterCaptureTex2D, m_GraphicsTestSettings.ImageComparisonSettings);
            
            // Then we deactivate Cluster Display.
            // We expect the output to be restored to what it was before Cluster Display was activated.
            m_ClusterRenderer.gameObject.SetActive(false);
            
            using (new CameraCapture(m_Camera, m_VanillaCapture))
            {
                // Let a capture of the vanilla output happen.
                yield return new WaitForEndOfFrame();
            }
            
            CopyToTexture2D(m_VanillaCapture, m_VanillaCapture2Tex2D);
            
            ImageAssert.AreEqual(m_VanillaCaptureTex2D, m_VanillaCapture2Tex2D, m_GraphicsTestSettings.ImageComparisonSettings);

            if (postRender != null)
            {
                postRender.Invoke();
            }
            
            DisposeTest();
        }

        protected IEnumerator RenderAndCompare(
            Action preRender = null, 
            Action postRender = null,
            Action exceptionHandler = null)
        {
            InitializeTest();

            if (preRender != null)
            {
                preRender.Invoke();
            }
            
            yield return RenderVanillaAndOverscan();

            if (postRender != null)
            {
                postRender.Invoke();
            }

            CopyToTexture2D(m_VanillaCapture, m_VanillaCaptureTex2D);
            CopyToTexture2D(m_ClusterCapture, m_ClusterCaptureTex2D);

            if (exceptionHandler == null)
            {
                ImageAssert.AreEqual(m_VanillaCaptureTex2D, m_ClusterCaptureTex2D, m_GraphicsTestSettings.ImageComparisonSettings);
            }
            else
            {
                try
                {
                    ImageAssert.AreEqual(m_VanillaCaptureTex2D, m_ClusterCaptureTex2D, m_GraphicsTestSettings.ImageComparisonSettings);
                }
                catch (Exception)
                {
                    exceptionHandler.Invoke();
                    throw;
                }
            }

            DisposeTest();
        }
        
        protected void AlignSurfaceWithCameraFrustum(float nearPlaneOffset)
        {
            var projection = m_ClusterRenderer.ProjectionPolicy as TrackedPerspectiveProjection;
            Assert.IsNotNull(projection);
            Assert.IsTrue(projection.Surfaces.Count == 1);

            var alignedSurface = AlignSurfaceWithCameraFrustum(projection.Surfaces[0], m_Camera, nearPlaneOffset, m_ClusterRenderer.transform);
            projection.SetSurface(0, alignedSurface);
        }
        
        static Camera FindUniqueMainCamera()
        {
            var cameras = FindObjectsOfType<Camera>(true);
            foreach (var camera in cameras)
            {
                if (camera.CompareTag(k_MainCameraTag))
                {
                    return camera;
                }
            }

            return null;
        }
        
        protected static ProjectionSurface AlignSurfaceWithCameraFrustum(ProjectionSurface surface, Camera camera, float nearPlaneOffset, Transform rendererTransform)
        {
            // Evaluate surface in local camera space.
            var distance = nearPlaneOffset + camera.nearClipPlane;
            var height = 2f * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * .5f) * distance;
            var width = height * camera.aspect;

            var cameraLocalSurfaceTransform = Matrix4x4.TRS(
                camera.transform.forward * distance, 
                Quaternion.AngleAxis(180, Vector3.up), 
                new Vector3(width, height, 0));

            // Convert to local renderer space.
            var rendererLocalSurfaceTransform = 
                rendererTransform.worldToLocalMatrix * 
                camera.transform.localToWorldMatrix * 
                cameraLocalSurfaceTransform;
        
            surface.LocalPosition = rendererLocalSurfaceTransform.GetPosition();
            surface.LocalRotation = rendererLocalSurfaceTransform.rotation;
            var lossyScale = rendererLocalSurfaceTransform.lossyScale;
            surface.PhysicalSize = new Vector2(lossyScale.x, lossyScale.y);
            return surface;
        }

        static void AssertImagesAreNotEqual(Texture2D texA, Texture2D TexB, ImageComparisonSettings imageComparisonSettings)
        {
            try
            {
                ImageAssert.AreEqual(texA, TexB, imageComparisonSettings);
            }
            catch (Exception)
            {
                // Deliberately swallow the exception.
                return;
            }

            throw new InvalidOperationException($"{nameof(AssertImagesAreNotEqual)} failed, Images were equal.");
        }

        static void CopyToTexture2D(RenderTexture source, Texture2D dest)
        {
            Assert.IsTrue(source.width == dest.width);
            Assert.IsTrue(source.height == dest.height);

            var restore = RenderTexture.active;
            RenderTexture.active = source;
            dest.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            dest.Apply();
            RenderTexture.active = restore;
        }
        
        static void SetGameViewSize(int width, int height)
        {
            if (GameViewUtils.SizeExists(GameViewSizeGroupType.Standalone, k_GameViewSizeName))
            {
                GameViewUtils.RemoveCustomSize(GameViewSizeGroupType.Standalone, GameViewUtils.FindSize(GameViewSizeGroupType.Standalone, k_GameViewSizeName));
            }

            GameViewUtils.AddCustomSize(
                GameViewUtils.GameViewSizeType.FixedResolution, 
                GameViewSizeGroupType.Standalone, 
                width, height, k_GameViewSizeName);

            GameViewUtils.SetSize(GameViewUtils.FindSize(GameViewSizeGroupType.Standalone, k_GameViewSizeName));
        }
    }
}
