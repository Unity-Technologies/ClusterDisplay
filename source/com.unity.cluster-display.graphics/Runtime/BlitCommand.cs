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

        public readonly Material material;
        public readonly MaterialPropertyBlock materialPropertyBlock;

        public BlitCommand(RenderTexture texture, Vector4 scaleBiasTex, Vector4 scaleBiasRT, Material overridingMaterial = null, MaterialPropertyBlock overridingPropertyBlock = null)
        {
            this.texture = texture;
            this.scaleBiasTex = scaleBiasTex;
            this.scaleBiasRT = scaleBiasRT;

            this.material = overridingMaterial;
            this.materialPropertyBlock = overridingPropertyBlock;
        }
    }
}
