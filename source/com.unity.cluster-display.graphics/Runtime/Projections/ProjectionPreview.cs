using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    public class ProjectionPreview : MonoBehaviour
    {
#if CLUSTER_DISPLAY_HDRP
        const string k_ShaderName = "HDRP/Unlit";
#elif CLUSTER_DISPLAY_URP
        const string k_ShaderName = "Universal Render Pipeline/Unlit";
#endif
        
        static readonly Quaternion k_BasePlaneRotation = Quaternion.Euler(90, 0, 0);
        static readonly Vector3 k_BaseScale = Vector3.one / 10f;

        Material m_Material;
        MeshRenderer m_Renderer;
        RenderTexture m_Texture;
        
        public static ProjectionPreview Create()
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Plane);
            obj.layer = ClusterRenderer.VirtualObjectLayer;
            var preview = obj.AddComponent<ProjectionPreview>();

            obj.hideFlags = HideFlags.HideAndDontSave;
            preview.m_Material = new(Shader.Find(k_ShaderName));
            preview.m_Renderer = obj.GetComponent<MeshRenderer>();
            preview.m_Renderer.sharedMaterial = preview.m_Material;
            return preview;
        }

        public void Draw(
            Vector3 position, 
            Quaternion rotation, 
            Vector3 scale, 
            RenderTexture source, 
            Vector2Int resolution,
            int overScan,
            GraphicsFormat format)
        {
            var previewTransform = m_Renderer.transform;
            previewTransform.position = position;
            previewTransform.rotation = rotation * k_BasePlaneRotation;
            var previewScale = k_BasePlaneRotation * scale;
            previewScale.y = 1;
            previewScale.Scale(k_BaseScale);
            previewTransform.localScale = previewScale;

            if (GraphicsUtil.AllocateIfNeeded(
                ref m_Texture,
                resolution.x,
                resolution.y,
                format))
            {
                m_Material.mainTexture = m_Texture;
            }

            var overscannedSize = resolution + overScan * 2 * Vector2Int.one;

            UnityEngine.Graphics.Blit(
                source: source, dest: m_Texture,
                scale: (Vector2) resolution / overscannedSize,
                offset: Vector2.one * overScan / overscannedSize);
        }
    }
}