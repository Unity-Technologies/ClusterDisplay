using System;
using Unity.ClusterDisplay;
using Unity.ClusterDisplay.Graphics;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

[ExecuteAlways]
public class TrackedPerspectiveProjection : MonoBehaviour, IProjectionPolicy
{
    [SerializeField]
    bool m_IsDebug;

    [SerializeField]
    public TrackedPerspectiveSurface[] m_ProjectionSurfaces;

    [SerializeField]
    int m_NodeIndexOverride;

    Camera m_Camera;
    BlitCommand m_BlitCommand;

    public void UpdateCluster(ClusterRendererSettings clusterSettings, Camera activeCamera)
    {
        if (m_IsDebug)
        {
            foreach (var surface in m_ProjectionSurfaces)
            {
                surface.Render(clusterSettings, activeCamera);
            }

            return;
        }

        var nodeIndex = m_IsDebug || !ClusterSync.Active ? m_NodeIndexOverride : ClusterSync.Instance.DynamicLocalNodeId;
        if (nodeIndex >= m_ProjectionSurfaces.Length) return;
        
        var targetSurface = m_ProjectionSurfaces[nodeIndex];
        targetSurface.Render(clusterSettings, activeCamera);

        m_BlitCommand = new BlitCommand(
            targetSurface.RenderTarget,
            new BlitParams(
                    m_ProjectionSurfaces[nodeIndex].Resolution,
                    clusterSettings.OverScanInPixels, Vector2.zero)
                .ScaleBias,
            GraphicsUtil.ToVector4(new Rect(0, 0, 1, 1)));
    }

    public void Present(CommandBuffer commandBuffer)
    {
        if (m_IsDebug)
        {
            return;
        }

        GraphicsUtil.Blit(commandBuffer, m_BlitCommand);
    }
}
