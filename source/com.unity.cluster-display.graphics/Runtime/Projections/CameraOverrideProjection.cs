using System;
using Unity.ClusterDisplay.Graphics;
using UnityEngine;
using UnityEngine.Assertions;

namespace Unity.ClusterDisplay.Graphics
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

    public static class CameraDataOverridePipeline
    {
        internal static OverridingCameraData ? m_CameraOverrideData;
        internal static OverridingCameraData? cameraOverrideData => m_CameraOverrideData;

        public static void SetCameraOverrideData (OverridingCameraData cameraOverrideData) => m_CameraOverrideData = cameraOverrideData;
    }
}
    
[PopupItem("Camera Override")]
[CreateAssetMenu(fileName = "CameraOverride", menuName = "Cluster Display/Camera Override Projection")]
class CameraOverrideProjection : ProjectionPolicy
{
    public OverrideProperty Overrides
    {
        get => m_CameraOverrideData.m_Overrides;
        set => m_CameraOverrideData.m_Overrides = value;
    }
    
    public Vector3 Position
    {
        get => m_CameraOverrideData.m_Position;
        set => m_CameraOverrideData.m_Position = value;
    }

    public Quaternion Rotation
    {
        get => m_CameraOverrideData.m_Rotation;
        set => m_CameraOverrideData.m_Rotation = value;
    }

    public Matrix4x4 ProjectionMatrix
    {
        get => m_CameraOverrideData.m_ProjectionMatrix;
        set => m_CameraOverrideData.m_ProjectionMatrix = value;
    }

    [SerializeField]
    OverridingCameraData m_CameraOverrideData;
    
    RenderTexture m_RenderTarget;
    BlitCommand m_BlitCommand;
    
    public override void UpdateCluster(ClusterRendererSettings clusterSettings, Camera activeCamera)
    {
        // Attempt to retrieve user overriding camera data.
        var userCameraOverrideData = CameraDataOverridePipeline.cameraOverrideData;
        var camerOverrideData = m_CameraOverrideData;

        if (userCameraOverrideData != null) // Determine if it's null, if it's NOT null, then it's been set.
        {
            camerOverrideData = userCameraOverrideData.Value;
        }

        var displaySize = new Vector2Int(Screen.width, Screen.height);
        var overscannedSize = displaySize + clusterSettings.OverScanInPixels * 2 * Vector2Int.one;

        GraphicsUtil.AllocateIfNeeded(ref m_RenderTarget, overscannedSize.x, overscannedSize.y);

        var postEffectsParams = new PostEffectsParams(displaySize);
        var viewport = new Viewport(new Vector2Int(1, 1), displaySize, Vector2.zero, clusterSettings.OverScanInPixels);
        var subsection = viewport.GetSubsectionWithOverscan(0);

        using (var cameraScope = CameraScopeFactory.Create(activeCamera, RenderFeature.AsymmetricProjectionAndScreenCoordOverride))
        {
            cameraScope.Render(m_RenderTarget,
                screenSizeOverride: postEffectsParams.GetScreenSizeOverride(),
                screenCoordScaleBias: PostEffectsParams.GetScreenCoordScaleBias(subsection),
                projection: camerOverrideData.m_Overrides.HasFlag(OverrideProperty.ProjectionMatrix) ? camerOverrideData.m_ProjectionMatrix.GetFrustumSlice(subsection) : null,
                position: camerOverrideData.m_Overrides.HasFlag(OverrideProperty.Position) ? camerOverrideData.m_Position : null,
                rotation: camerOverrideData.m_Overrides.HasFlag(OverrideProperty.Rotation) ? camerOverrideData.m_Rotation : null);
        }
        
        m_BlitCommand = new BlitCommand(
            m_RenderTarget,
            new BlitParams(
                    displaySize,
                    clusterSettings.OverScanInPixels, Vector2.zero)
                .ScaleBias,
            GraphicsUtil.k_IdentityScaleBias);

        CameraDataOverridePipeline.m_CameraOverrideData = null; // Reset this to null, the user has to override this every frame, otherwise it will reset.
    }

    public override void Present(PresentArgs args)
    {
        Assert.IsNotNull(m_BlitCommand.texture);
        GraphicsUtil.Blit(args.CommandBuffer, m_BlitCommand, args.FlipY);
    }

    void OnDisable()
    {
        GraphicsUtil.DeallocateIfNeeded(ref m_RenderTarget);
    }
}
