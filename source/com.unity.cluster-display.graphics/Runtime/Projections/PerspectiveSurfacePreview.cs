using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Pool;

namespace Unity.ClusterDisplay.Graphics
{
    [InitializeOnLoad]
    public class PerspectiveSurfacePreview : MonoBehaviour
    {
#if CLUSTER_DISPLAY_HDRP
        const string k_ShaderName = "HDRP/Unlit";
#elif CLUSTER_DISPLAY_URP
        const string k_ShaderName = "Universal Render Pipeline/Unlit";
#endif
        MeshRenderer m_PreviewRenderer;
        Material m_ScreenPreviewMaterial;
        RenderTexture m_PreviewTexture;

        static readonly Quaternion k_BasePlaneRotation = Quaternion.Euler(90, 0, 0);
        static readonly Vector3 k_BaseScale = Vector3.one / 10f;

        static Dictionary<TrackedPerspectiveSurface, PerspectiveSurfacePreview> s_Previews = new();
        static ObjectPool<PerspectiveSurfacePreview> s_Pool;

        static PerspectiveSurfacePreview()
        {
            s_Pool = new ObjectPool<PerspectiveSurfacePreview>(
                createFunc: () =>
                {
                    var obj = GameObject.CreatePrimitive(PrimitiveType.Plane);
                    obj.layer = ClusterRenderer.VirtualObjectLayer;
                    var surfacePreview = obj.AddComponent<PerspectiveSurfacePreview>();

                    // obj.hideFlags = HideFlags.HideInHierarchy;
                    surfacePreview.m_PreviewRenderer = obj.GetComponent<MeshRenderer>();
                    surfacePreview.m_ScreenPreviewMaterial = new Material(Shader.Find(k_ShaderName));
                    surfacePreview.m_PreviewRenderer.sharedMaterial = surfacePreview.m_ScreenPreviewMaterial;
                    return surfacePreview;
                },
                actionOnGet: preview => preview.gameObject.SetActive(true),
                actionOnRelease: preview => preview.gameObject.SetActive(false), 
                actionOnDestroy: preview => Destroy(preview.gameObject));
        }

        void Awake()
        {
            m_PreviewRenderer.material = m_ScreenPreviewMaterial;
        }

        internal static void UpdateSurfacePreview(TrackedPerspectiveSurface surface, ClusterRendererSettings settings)
        {
            if (!s_Previews.TryGetValue(surface, out var preview))
            {
                s_Pool.Get(out preview);
                s_Previews[surface] = preview;
            }
            
            preview.UpdatePreview(surface, settings);
        }

        internal static void DisableSurfacePreview(TrackedPerspectiveSurface surface)
        {
            if (s_Previews.TryGetValue(surface, out var preview))
            {
                s_Previews.Remove(surface);
                s_Pool.Release(preview);
            }
        }
        
        internal static void DisableAll()
        {
            foreach (var item in s_Previews)
            {
                s_Pool.Release(item.Value);
            }
            
            s_Previews.Clear();
        }

        void UpdatePreview(TrackedPerspectiveSurface surface, ClusterRendererSettings settings)
        {
            var previewTransform = m_PreviewRenderer.transform;
            previewTransform.position = surface.Position;
            previewTransform.rotation = surface.Rotation * k_BasePlaneRotation;
            var previewScale = k_BasePlaneRotation * surface.Scale;
            previewScale.y = 1;
            previewScale.Scale(k_BaseScale);
            previewTransform.localScale = previewScale;

            if (GraphicsUtil.AllocateIfNeeded(
                ref m_PreviewTexture,
                surface.Resolution.x,
                surface.Resolution.y,
                surface.GraphicsFormat))
            {
                m_ScreenPreviewMaterial.mainTexture = m_PreviewTexture;
            }

            var overscannedSize = surface.Resolution + settings.OverScanInPixels * 2 * Vector2Int.one;

            UnityEngine.Graphics.Blit(
                source: surface.RenderTarget, dest: m_PreviewTexture,
                scale: (Vector2) surface.Resolution / overscannedSize,
                offset: Vector2.one * settings.OverScanInPixels / overscannedSize);
        }

        // Start is called before the first frame update
        void Start() { }

        // Update is called once per frame
        void Update() { }
    }
}
