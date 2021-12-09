using UnityEngine;

namespace Unity.ClusterDisplay.Graphics
{
    public readonly struct BlitParams
    {
        readonly Vector2 m_DisplaySize;
        readonly Vector2 m_SrcOffset;
        readonly float m_OverscanInPixels;

        public BlitParams(Vector2 displaySize, float overscanInPixels, Vector2 srcOffset)
        {
            m_DisplaySize = displaySize;
            m_OverscanInPixels = overscanInPixels;
            m_SrcOffset = srcOffset;
        }

        public Vector4 ScaleBias
        {
            get
            {
                {
                    var overscannedSize = m_DisplaySize + Vector2.one * m_OverscanInPixels * 2;

                    return new Vector4(
                        m_DisplaySize.x / overscannedSize.x, m_DisplaySize.y / overscannedSize.y,
                        m_OverscanInPixels / overscannedSize.x + m_SrcOffset.x,
                        m_OverscanInPixels / overscannedSize.y + m_SrcOffset.y);
                }
            }
        }
    }
}
