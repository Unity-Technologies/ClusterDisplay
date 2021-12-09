using System;
using Unity.ClusterDisplay;
using Unity.ClusterDisplay.Graphics;
using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(fileName = "ClusterRenderer", menuName = "Cluster Display/Projection Policies/Tracked Perspective Projection", order = 1)]
[ExecuteAlways, DisallowMultipleComponent]
[PopupItem("Tracked Perspective")]
public sealed class TrackedPerspectiveProjection : ProjectionPolicy
{
    [SerializeField]
    bool m_IsDebug;

    [SerializeField]
    public TrackedPerspectiveSurface[] m_ProjectionSurfaces = Array.Empty<TrackedPerspectiveSurface>();

    [SerializeField]
    int m_NodeIndexOverride;

    Camera m_Camera;
    BlitCommand m_BlitCommand;

    public override void UpdateCluster(ClusterRendererSettings clusterSettings, Camera activeCamera)
    {
        var nodeIndex = m_IsDebug || !ClusterDisplayState.IsActive ? m_NodeIndexOverride : ClusterDisplayState.NodeID;

        if (nodeIndex >= m_ProjectionSurfaces.Length)
        {
            return;
        }

        if (m_ProjectionSurfaces[nodeIndex] is not {isActiveAndEnabled: true} targetSurface)
        {
            return;
        }

        if (m_IsDebug)
        {
            foreach (var surface in m_ProjectionSurfaces)
            {
                surface.Render(clusterSettings, activeCamera);
            }
        }
        else
        {
            targetSurface.Render(clusterSettings, activeCamera);
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

    public override void OnEnable() {}
    public override void OnDisable() {}
}
