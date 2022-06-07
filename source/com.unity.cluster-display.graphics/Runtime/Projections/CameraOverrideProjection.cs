using System;
using UnityEngine;

namespace Unity.ClusterDisplay.Graphics
{
    [PopupItem("Camera Override")]
    [CreateAssetMenu(fileName = "CameraOverride", menuName = "Cluster Display/Camera Override Projection")]
    public class CameraOverrideProjection : ProjectionPolicy
    {
        [Flags]
        public enum OverrideProperty
        {
            None = 0,
            Position = 1,
            Rotation = 2,
            ProjectionMatrix = 4,
            NodeID = 8,
            Resolution = 16,
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

        public int NodeID
        {
            get => m_NodeID;
            set => m_NodeID = value;
        }

        public Vector2Int Resolution
        {
            get => m_Resolution;
            set => m_Resolution = value;
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
        int m_NodeID;

        [SerializeField]
        Vector2Int m_Resolution;

        RenderTexture m_RenderTarget;
        BlitCommand m_BlitCommand;
    
        public override void UpdateCluster(ClusterRendererSettings clusterSettings, Camera activeCamera)
        {
            var displaySize = m_Overrides.HasFlag(OverrideProperty.Resolution) ? m_Resolution : new Vector2Int(Screen.width, Screen.height);
            var overscannedSize = displaySize + clusterSettings.OverScanInPixels * 2 * Vector2Int.one;

            GraphicsUtil.AllocateIfNeeded(ref m_RenderTarget, overscannedSize.x, overscannedSize.y);

            var postEffectsParams = new PostEffectsParams(displaySize);
            var viewport = new Viewport(new Vector2Int(1, 1), displaySize, Vector2.zero, clusterSettings.OverScanInPixels);
            var subsection = viewport.GetSubsectionWithOverscan(0, displaySize);

            using (var cameraScope = CameraScopeFactory.Create(activeCamera, RenderFeature.AsymmetricProjectionAndScreenCoordOverride))
            {
                cameraScope.Render(m_RenderTarget,
                    screenSizeOverride: postEffectsParams.GetScreenSizeOverride(),
                    screenCoordScaleBias: PostEffectsParams.GetScreenCoordScaleBias(subsection),
                    projection: m_Overrides.HasFlag(OverrideProperty.ProjectionMatrix) ? m_ProjectionMatrix.GetFrustumSlice(subsection) : null,
                    position: m_Overrides.HasFlag(OverrideProperty.Position) ? m_Position : null,
                    rotation: m_Overrides.HasFlag(OverrideProperty.Rotation) ? m_Rotation : null);
            }

            ClusterDisplayState.TryGetRuntimeNodeId(out var runtimeNodeId); // I don't check if cluster sync is active since this is rendering code.
            int nodeId = m_Overrides.HasFlag(OverrideProperty.NodeID) ? m_NodeID : runtimeNodeId;

            var blitParams = new BlitParams(
                displaySize,
                clusterSettings.OverScanInPixels, Vector2.zero);

            m_BlitCommand = new BlitCommand(
                m_RenderTarget,
                blitParams.ScaleBias,
                GraphicsUtil.k_IdentityScaleBias,
                customBlitMaterial,
                GetCustomBlitMaterialPropertyBlocks(nodeId));
        }

        public override void Present(PresentArgs args)
        {
            if (m_BlitCommand.texture != null)
            {
                GraphicsUtil.Blit(args.CommandBuffer, m_BlitCommand, args.FlipY);
            }
        }

        void OnDisable()
        {
            GraphicsUtil.DeallocateIfNeeded(ref m_RenderTarget);
        }
    }
}
