using System;
using Unity.ClusterDisplay;
using Unity.ClusterDisplay.Graphics;
using UnityEngine;
using UnityEngine.Rendering;

[PopupItem("Tracked Perspective")]
public sealed class TrackedPerspectiveProjection : ProjectionPolicy
{
    [SerializeField]
    bool m_IsDebug;

    [SerializeReference]
    TrackedPerspectiveSurface[] m_ProjectionSurfaces = Array.Empty<TrackedPerspectiveSurface>();

    [SerializeField]
    int m_NodeIndexOverride;

    Camera m_Camera;
    BlitCommand m_BlitCommand;

    Matrix4x4 m_RootTransform = Matrix4x4.identity;

    public override void UpdateCluster(ClusterRendererSettings clusterSettings, Camera activeCamera)
    {
        var nodeIndex = m_IsDebug || !ClusterSync.Active ? m_NodeIndexOverride : ClusterSync.Instance.DynamicLocalNodeId;

        if (nodeIndex >= m_ProjectionSurfaces.Length)
        {
            return;
        }

        if (m_ProjectionSurfaces[nodeIndex] is not { } targetSurface)
        {
            return;
        }

        if (m_IsDebug)
        {
            foreach (var surface in m_ProjectionSurfaces)
            {
                surface.Render(clusterSettings, activeCamera, m_RootTransform);
            }
        }
        else
        {
            targetSurface.Render(clusterSettings, activeCamera, m_RootTransform);
        }

        m_BlitCommand = new BlitCommand(
            targetSurface.RenderTarget,
            new BlitParams(
                    m_ProjectionSurfaces[nodeIndex].Resolution,
                    clusterSettings.OverScanInPixels, Vector2.zero)
                .ScaleBias,
            GraphicsUtil.k_IdentityScaleBias);
    }

    public override void Present(CommandBuffer commandBuffer)
    {
        if (m_ProjectionSurfaces.Length == 0 || m_BlitCommand.texture == null)
        {
            return;
        }

        GraphicsUtil.Blit(commandBuffer, m_BlitCommand);
    }

    void OnDisable()
    {
        foreach (var surface in m_ProjectionSurfaces)
        {
            surface.Dispose();
        }
    }
}
