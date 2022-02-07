using System;
using System.Collections;
using System.Collections.Generic;
using Unity.ClusterDisplay.Graphics;
using UnityEngine;

class CameraOverrideProjection : ProjectionPolicy
{
    [Flags]
    public enum OverrideProperty
    {
        None = 0,
        Position = 1,
        Rotation = 2,
        ProjectionMatrix = 4,
        All = ~0
    }
    
    public OverrideProperty Overrides
    {
        get => m_Overrides;
        set => m_Overrides = value;
    }
    
    public Vector3 Position
    {
        get => m_Position;
        set => m_Position = value;
    }

    public Quaternion Rotation
    {
        get => m_Rotation;
        set => m_Rotation = value;
    }

    public Matrix4x4 ProjectionMatrix
    {
        get => m_ProjectionMatrix;
        set => m_ProjectionMatrix = value;
    }

    public RenderFeature RenderFeature
    {
        get => m_RenderFeature;
        set => m_RenderFeature = value;
    }

    public Vector2Int ScreenResolution
    {
        get => m_ScreenResolution;
        set => m_ScreenResolution = value;
    }

    [SerializeField]
    OverrideProperty m_Overrides;
    
    [SerializeField]
    Vector3 m_Position;
    
    [SerializeField]
    Quaternion m_Rotation;
    
    [SerializeField]
    Matrix4x4 m_ProjectionMatrix;
    
    [SerializeField]
    RenderFeature m_RenderFeature = RenderFeature.AsymmetricProjection;
    
    [SerializeField]
    Vector2Int m_ScreenResolution;

    RenderTexture m_RenderTarget;
    BlitCommand m_BlitCommand;
    
    public override void UpdateCluster(ClusterRendererSettings clusterSettings, Camera activeCamera)
    {
        using (var cameraScope = CameraScopeFactory.Create(activeCamera, RenderFeature))
        {
            cameraScope.Render(m_RenderTarget, 
                projection: m_Overrides.HasFlag(OverrideProperty.ProjectionMatrix) ? m_ProjectionMatrix : null,
                position: m_Overrides.HasFlag(OverrideProperty.Position) ? m_Position : null,
                rotation: m_Overrides.HasFlag(OverrideProperty.Rotation) ? m_Rotation : null);
        }
        
        var overscannedSize = ScreenResolution + clusterSettings.OverScanInPixels * 2 * Vector2Int.one;

        if (!GraphicsUtil.AllocateIfNeeded(
            ref m_RenderTarget,
            overscannedSize.x,
            overscannedSize.y))
        {
            Debug.LogError("Unable to allocate render target");
            return;
        }
        
        m_BlitCommand = new BlitCommand(
            m_RenderTarget,
            new BlitParams(
                    ScreenResolution,
                    clusterSettings.OverScanInPixels, Vector2.zero)
                .ScaleBias,
            GraphicsUtil.k_IdentityScaleBias);
    }

    public override void Present(PresentArgs args)
    {
        if (m_BlitCommand.texture == null)
        {
            return;
        }

        GraphicsUtil.Blit(args.CommandBuffer, m_BlitCommand, args.FlipY);
    }
}
