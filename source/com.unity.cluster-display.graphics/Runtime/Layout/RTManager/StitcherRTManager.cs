using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    public abstract class StitcherRTManager
    {
        public abstract RTType type { get; }
        protected abstract object GetBlitRT(int tileCount, int tileIndex, int width, int height);
        protected abstract object GetPresentRT(int width, int height);
        public abstract void Release();

        public RenderTexture GetBlitRenderTexture (int tileCount, int tileIndex, int width, int height)
        {
            var rt = GetBlitRT(tileCount, tileIndex, width, height);
            if (!(rt is RenderTexture))
                throw new System.Exception("Blit RT is not a RenderTexture.");
            return rt as RenderTexture;
        }

        public RTHandle GetBlitRTHandle (int tileCount, int tileIndex, int width, int height)
        {
            var rt = GetBlitRT(tileCount, tileIndex, width, height);
            if (!(rt is RTHandle))
                throw new System.Exception("Blit RT is not a RenderTexture.");
            return rt as RTHandle;
        }

        public RenderTexture GetPresentRenderTexture (int width, int height)
        {
            var rt = GetPresentRT(width, height);
            if (!(rt is RenderTexture))
                throw new System.Exception("Blit RT is not a RenderTexture.");
            return rt as RenderTexture;
        }

        public RTHandle GetPresentRTHandle (int width, int height)
        {
            var rt = GetPresentRT(width, height);
            if (!(rt is RTHandle))
                throw new System.Exception("Blit RT is not a RenderTexture.");
            return rt as RTHandle;
        }
    }
}
