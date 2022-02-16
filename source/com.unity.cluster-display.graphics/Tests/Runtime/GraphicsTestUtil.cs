using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.TestTools.Graphics;
using Object = UnityEngine.Object;

namespace Unity.ClusterDisplay.Graphics.Tests
{
    public static class GraphicsTestUtil
    {
        const string k_MainCameraTag = "MainCamera";

        public static IEnumerator DoScreenCapture(RenderTexture target)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshotIntoRenderTexture(target);
        }

        public static Camera FindUniqueMainCamera()
        {
            var cameras = Object.FindObjectsOfType<Camera>(true);
            foreach (var camera in cameras)
            {
                if (camera.CompareTag(k_MainCameraTag))
                {
                    return camera;
                }
            }

            return null;
        }

        public static void CopyToTexture2D(RenderTexture source, Texture2D dest)
        {
            Assert.IsTrue(source.width == dest.width);
            Assert.IsTrue(source.height == dest.height);

            var restore = RenderTexture.active;
            RenderTexture.active = source;
            dest.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            dest.Apply();
            RenderTexture.active = restore;
        }

        public static void AssertImagesAreNotEqual(Texture2D texA, Texture2D TexB, ImageComparisonSettings imageComparisonSettings)
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

        public static ProjectionSurface AlignSurfaceWithCameraFrustum(ProjectionSurface surface, Camera camera, float nearPlaneOffset, Transform rendererTransform)
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
    }
}
