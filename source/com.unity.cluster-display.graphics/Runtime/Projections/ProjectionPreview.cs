using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    /// <summary>
    /// Script that draws an unlit textured plane hidden in the hierarchy
    /// and not rendered by the game camera.
    /// </summary>
    /// <remarks>
    /// Use this script to preview projection surfaces (screens).
    /// </remarks>
    class ProjectionPreview : MonoBehaviour
    {
#if CLUSTER_DISPLAY_HDRP
        const string k_ShaderName = "HDRP/Unlit";
#elif CLUSTER_DISPLAY_URP
        const string k_ShaderName = "Universal Render Pipeline/Unlit";
#endif

        static readonly Quaternion k_BasePlaneRotation = Quaternion.Euler(90, 0, 0);
        static readonly Vector3 k_BaseScale = Vector3.one / 10f;

        Material m_Material;
        RenderTexture m_Texture;

        public static ProjectionPreview Create()
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Plane);
            obj.layer = ClusterRenderer.VirtualObjectLayer;
            var preview = obj.AddComponent<ProjectionPreview>();

            obj.hideFlags = HideFlags.HideAndDontSave;
            preview.m_Material = new Material(Shader.Find(k_ShaderName));
            var renderer = obj.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.sharedMaterial = preview.m_Material;
            return preview;
        }

        public void Draw(
            Vector3 position,
            Quaternion rotation,
            Vector3 scale,
            RenderTexture source,
            Vector2Int resolution,
            int overScan)
        {
            var previewTransform = transform;
            previewTransform.position = position;
            previewTransform.rotation = rotation * k_BasePlaneRotation;

            var previewScale = k_BasePlaneRotation * scale;
            previewScale.y = 1;
            previewScale.Scale(k_BaseScale);

            previewTransform.localScale = previewScale;

            if (GraphicsUtil.AllocateIfNeeded(
                ref m_Texture,
                resolution.x,
                resolution.y))
            {
                m_Material.mainTexture = m_Texture;
            }

            var overscannedSize = resolution + overScan * 2 * Vector2Int.one;

            UnityEngine.Graphics.Blit(
                source: source, dest: m_Texture,
                scale: (Vector2) resolution / overscannedSize,
                offset: Vector2.one * overScan / overscannedSize
            );
        }
    }
}
