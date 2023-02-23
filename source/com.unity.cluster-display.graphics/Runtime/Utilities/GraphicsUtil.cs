using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    /// <summary>
    /// Add this to a class that uses an unreferenced shader.
    /// </summary>
    /// <remarks>
    /// Mark fields/properties that give the name(s) of the required shader(s) with <see cref="AlwaysIncludeShaderAttribute"/>.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
    class RequiresUnreferencedShaderAttribute : Attribute
    {
    }

    /// <summary>
    /// Marks the field or property to hold the value of an unreferenced shader, so it
    /// gets included in the project's "Always included shaders" setting.
    /// </summary>
    /// <remarks>
    /// The field or property must be <see langword="static"/>.
    /// The enclosing type must have the <see cref="RequiresUnreferencedShaderAttribute"/> attribute.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    class AlwaysIncludeShaderAttribute : Attribute
    {
    }

    [RequiresUnreferencedShader]
    internal static class GraphicsUtil
    {
        // Will only be used for Legacy if we end up supporting it.
        // Otherwise see ScreenCoordOverrideUtils in SRP Core.
        const string k_ShaderKeyword = "SCREEN_COORD_OVERRIDE";

        static GraphicsFormat s_GraphicsFormat = GraphicsFormat.None;

        public static readonly Vector4 k_IdentityScaleBias = new Vector4(1, 1, 0, 0);

        internal static class ShaderIDs
        {
            public static readonly int _MainTex = Shader.PropertyToID("_MainTex");
            public static readonly int _BlitTexture = Shader.PropertyToID("_BlitTexture");
            public static readonly int _BlitScaleBias = Shader.PropertyToID("_BlitScaleBias");
            public static readonly int _BlitScaleBiasRt = Shader.PropertyToID("_BlitScaleBiasRt");
            public static readonly int _BlitMipLevel = Shader.PropertyToID("_BlitMipLevel");
            public static readonly int _UVRotation = Shader.PropertyToID("_UVRotation");
            public static readonly int _UVShift = Shader.PropertyToID("_UVShift");
        }

        public enum UVRotation
        {
            Zero,
            CW90,
            CW180,
            CW270
        }

        // Material for performing the cluster present
        [AlwaysIncludeShader]
        public const string k_BlitShaderName = "Hidden/ClusterDisplay/Blit";

        // Material for drawing the warped renders onto the meshes for preview purposes
        const string k_PreviewShaderName = "Hidden/ClusterDisplay/ProjectionPreview";

        static MaterialPropertyBlock s_PropertyBlock;
        static Material s_BlitMaterial;
        static Material s_PreviewMaterial;

        public static GraphicsFormat GetGraphicsFormat()
        {
            if (s_GraphicsFormat == GraphicsFormat.None)
            {
#if CLUSTER_DISPLAY_HDRP
                s_GraphicsFormat = SystemInfo.GetGraphicsFormat(DefaultFormat.HDR);
#else
                s_GraphicsFormat = SystemInfo.GetGraphicsFormat(DefaultFormat.LDR);
#endif
            }

            return s_GraphicsFormat;
        }

        static MaterialPropertyBlock GetPropertyBlock()
        {
            if (s_PropertyBlock == null)
            {
                s_PropertyBlock = new MaterialPropertyBlock();
            }

            return s_PropertyBlock;
        }

        static Material GetBlitMaterial()
        {
            if (s_BlitMaterial == null)
            {
                s_BlitMaterial = CreateHiddenMaterial(k_BlitShaderName);
            }

            return s_BlitMaterial;
        }

        public static Material GetPreviewMaterial()
        {
            if (s_PreviewMaterial == null)
            {
                s_PreviewMaterial = CreateHiddenMaterial(k_PreviewShaderName);
                s_PreviewMaterial.SetVector(ShaderIDs._UVRotation, new Vector4(1, 0, 0, 1));
                s_PreviewMaterial.SetVector(ShaderIDs._UVShift, new Vector2(0, 0));
            }

            return s_PreviewMaterial;
        }

        public static Material CreateHiddenMaterial(string shaderName)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                throw new InvalidOperationException($"Could not find shader \"{shaderName}\", " +
                    "make sure it has been added to the list of Always Included shaders");
            }

            return new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        public static void RotateUVs(this MaterialPropertyBlock propertyBlock, UVRotation rotation)
        {
            var r = rotation switch
            {
                UVRotation.Zero => new Vector4(1, 0, 0, 1),
                UVRotation.CW90 => new Vector4(0, -1, 1, 0),
                UVRotation.CW180 => new Vector4(-1, 0, 0, -1),
                UVRotation.CW270 => new Vector4(0, 1, -1, 0),
                _ => throw new ArgumentOutOfRangeException(nameof(rotation), rotation, null)
            };
            var t = rotation switch {
                UVRotation.Zero => new Vector2(0, 0),
                UVRotation.CW90 => new Vector2(1, 0),
                UVRotation.CW180 => new Vector2(1, 1),
                UVRotation.CW270 => new Vector2(0, 1),
                _ => throw new ArgumentOutOfRangeException(nameof(rotation), rotation, null)
            };

            propertyBlock.SetVector(ShaderIDs._UVRotation, r);
            propertyBlock.SetVector(ShaderIDs._UVShift, t);
        }

        public static void Blit(CommandBuffer commandBuffer, in BlitCommand blitCommand, bool flipY)
        {
            Blit(commandBuffer, blitCommand.texture, blitCommand.scaleBiasTex, blitCommand.scaleBiasRT, flipY, blitCommand.customMaterial, blitCommand.customMaterialPropertyBlock);
        }

        public static void Blit(CommandBuffer cmd, Texture source, bool flipY)
        {
            Blit(cmd, source, k_IdentityScaleBias, k_IdentityScaleBias, flipY);
        }

        public static void Blit(CommandBuffer cmd, Texture source, Vector4 texBias, Vector4 rtBias, bool flipY, Material material = null, MaterialPropertyBlock materialPropertyBlock = null)
        {
            var shaderPass = flipY ? 1 : 0;

            var propertyBlock = materialPropertyBlock == null ? GetPropertyBlock() : materialPropertyBlock;
            var blitMaterial = material == null ? GetBlitMaterial() : material;

            propertyBlock.SetTexture(ShaderIDs._BlitTexture, source);
            propertyBlock.SetVector(ShaderIDs._BlitScaleBias, texBias);
            propertyBlock.SetVector(ShaderIDs._BlitScaleBiasRt, rtBias);
            propertyBlock.SetFloat(ShaderIDs._BlitMipLevel, 0);

            cmd.DrawProcedural(Matrix4x4.identity, blitMaterial, shaderPass, MeshTopology.Quads, 4, 1, propertyBlock);
        }

        public static void AllocateIfNeeded(ref RenderTexture[] rts, int count, int width, int height, string name)
        {
            AllocateIfNeeded(ref rts, count, width, height, GetGraphicsFormat(), name);
        }

        public static void AllocateIfNeeded(ref RenderTexture[] rts, int count, int width, int height, GraphicsFormat format, string name)
        {
            var nameNeedsUpdate = false;

            if (rts == null || count != rts.Length)
            {
                nameNeedsUpdate = true;
                DeallocateIfNeeded(ref rts);
                rts = new RenderTexture[count];
            }

            for (var i = 0; i != count; ++i)
            {
                nameNeedsUpdate |= AllocateIfNeeded(ref rts[i], width, height, format);
            }

            if (nameNeedsUpdate)
            {
                for (var i = 0; i != count; ++i)
                {
                    rts[i].name = $"{name}-{width}X{height}-{i}";
                }
            }
        }

        public static bool AllocateIfNeeded(ref RenderTexture rt, int width, int height)
        {
            return AllocateIfNeeded(ref rt, width, height, GetGraphicsFormat());
        }

        public static bool AllocateIfNeeded(ref RenderTexture rt, int width, int height, GraphicsFormat format)
        {
            if (rt == null ||
                rt.width != width ||
                rt.height != height ||
                rt.graphicsFormat != format)
            {
                if (rt != null)
                {
                    rt.Release();
                }

                rt = new RenderTexture(width, height, 1, format, 0);
                return true;
            }

            return false;
        }

        public static void DeallocateIfNeeded(ref RenderTexture[] rts)
        {
            if (rts == null)
            {
                return;
            }

            for (var i = 0; i != rts.Length; ++i)
            {
                DeallocateIfNeeded(ref rts[i]);
            }

            rts = null;
        }

        public static void DeallocateIfNeeded(ref RenderTexture rt)
        {
            if (rt != null)
            {
                rt.Release();
            }

            rt = null;
        }

        // Convention, consistent with blit scale-bias for example.
        internal static Vector4 AsScaleBias(Rect rect) => new(rect.width, rect.height, rect.x, rect.y);

        public static void SaveCubemapToFile(RenderTexture rt, string path)
        {
            var equirect = new RenderTexture(rt.width * 4, rt.height * 3, rt.depth);
            rt.ConvertToEquirect(equirect, Camera.MonoOrStereoscopicEye.Mono);

            RenderTexture.active = equirect;
            Texture2D tex = new Texture2D(equirect.width, equirect.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, equirect.width, equirect.height), 0, 0);
            RenderTexture.active = null;

            byte[] bytes;
            bytes = tex.EncodeToPNG();

            System.IO.File.WriteAllBytes(path, bytes);
            Debug.Log("Saved to " + path);
        }
    }
}
