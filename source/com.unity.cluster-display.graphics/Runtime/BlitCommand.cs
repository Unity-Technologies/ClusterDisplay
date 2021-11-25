using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    /// <summary>
    /// A struct holding data required to blit a tile on screen.
    /// </summary>
    readonly struct BlitCommand
    {
        public readonly RenderTexture texture;
        public readonly Vector4 scaleBiasTex;
        public readonly Vector4 scaleBiasRT;

        public BlitCommand(RenderTexture texture, Vector4 scaleBiasTex, Vector4 scaleBiasRT)
        {
            this.texture = texture;
            this.scaleBiasTex = scaleBiasTex;
            this.scaleBiasRT = scaleBiasRT;
        }
    }
}
