using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using Unity.TestProtocol;
using Unity.TestProtocol.Messages;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unity.ClusterDisplay.Graphics.Tests
{
    public static class GraphicsTestUtil
    {
        const string k_MainCameraTag = "MainCamera";
        static readonly string k_DirectoryPath = Path.Combine(Application.dataPath, "RenderOutput");

        public static IEnumerator PreWarm()
        {
            yield return new WaitForEndOfFrame();
        }

        public static IEnumerator DoScreenCapture(RenderTexture target)
        {
            // IMPORTANT: ScreenCapture may grab whatever editor panel is being rendered if this yield instruction is changed!
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

        public static void ReportArtifact(string artifactPath)
        {
            var fullPath = Path.GetFullPath(artifactPath);
            var message = ArtifactPublishMessage.Create(fullPath);
            Debug.Log(UnityTestProtocolMessageBuilder.Serialize(message));
        }

        public static void SaveAsPNG(Texture2D texture, string fileName)
        {
            SaveAsPNG(texture, k_DirectoryPath, fileName);
        }

        public static void SaveAsPNG(Texture2D texture, string directory, string fileName)
        {
            var bytes = texture.EncodeToPNG();
            var filePath = Path.Combine(directory, fileName + ".png");
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllBytes(filePath, bytes);
        }
    }
}
