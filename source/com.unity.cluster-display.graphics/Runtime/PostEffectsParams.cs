using UnityEngine;

namespace Unity.ClusterDisplay.Graphics
{
    readonly struct PostEffectsParams
    {
        readonly Vector2 m_DisplayMatrixSize;

        public PostEffectsParams(Vector2 displayMatrixSize)
        {
            m_DisplayMatrixSize = displayMatrixSize;
        }

        public Vector4 GetScreenSizeOverride()
        {
            return new Vector4(m_DisplayMatrixSize.x, m_DisplayMatrixSize.y, 1.0f / m_DisplayMatrixSize.x, 1.0f / m_DisplayMatrixSize.y);
        }
        
        public static Vector4 GetScreenCoordTransform(Rect overscannedViewportSubsection)
        {
            return new Vector4(overscannedViewportSubsection.x, overscannedViewportSubsection.y, overscannedViewportSubsection.width, overscannedViewportSubsection.height);
        }
    }
}
