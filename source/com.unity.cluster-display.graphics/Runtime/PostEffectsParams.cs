using UnityEngine;

namespace Unity.ClusterDisplay.Graphics
{
    public readonly struct PostEffectsParams
    {
        readonly Vector2 m_DisplayMatrixSize;
        readonly Vector2Int m_GridSize;

        public PostEffectsParams(Vector2 displayMatrixSize, Vector2Int gridSize)
        {
            m_DisplayMatrixSize = displayMatrixSize;
            m_GridSize = gridSize;
        }

        public Matrix4x4 GetAsMatrix4x4(Rect overscannedViewportSubsection)
        {
            var parms = new Matrix4x4();

            var translationAndScale = new Vector4(overscannedViewportSubsection.x, overscannedViewportSubsection.y, overscannedViewportSubsection.width, overscannedViewportSubsection.height);
            parms.SetRow(0, translationAndScale);

            var screenSize = new Vector4(m_DisplayMatrixSize.x, m_DisplayMatrixSize.y, 1.0f / m_DisplayMatrixSize.x, 1.0f / m_DisplayMatrixSize.y);
            parms.SetRow(1, screenSize);

            var grid = new Vector4(m_GridSize.x, m_GridSize.y, 0, 0);
            parms.SetRow(2, grid);

            return parms;
        }
    }
}
