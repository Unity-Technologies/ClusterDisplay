using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    /// <summary>
    /// A struct holding data required to blit a tile on screen.
    /// </summary>
    struct BlitCommand
    {
        readonly RenderTexture m_Texture;
        readonly Vector4 m_ScaleBiasTex;
        readonly Vector4 m_ScaleBiasRT;

        public BlitCommand(RenderTexture texture, Vector4 scaleBiasTex, Vector4 scaleBiasRT)
        {
            m_Texture = texture;
            m_ScaleBiasTex = scaleBiasTex;
            m_ScaleBiasRT = scaleBiasRT;
        }

        public void Execute(CommandBuffer cmd, bool flipY)
        {
            GraphicsUtil.Blit(cmd, m_Texture, m_ScaleBiasTex, m_ScaleBiasRT, flipY);
        }
    }
}
