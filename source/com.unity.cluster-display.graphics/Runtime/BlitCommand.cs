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

        public readonly Material customMaterial;
        public readonly MaterialPropertyBlock customMaterialPropertyBlock;

        public BlitCommand(RenderTexture texture, Vector4 scaleBiasTex, Vector4 scaleBiasRT, Material customMaterial = null, MaterialPropertyBlock customMaterialPropertyBlock = null)
        {
            this.texture = texture;
            this.scaleBiasTex = scaleBiasTex;
            this.scaleBiasRT = scaleBiasRT;

            this.customMaterial = customMaterial;
            this.customMaterialPropertyBlock = customMaterialPropertyBlock;
        }
    }
}
