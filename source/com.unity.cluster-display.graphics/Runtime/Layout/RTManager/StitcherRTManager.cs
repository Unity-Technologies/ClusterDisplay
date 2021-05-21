using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    public abstract class StitcherRTManager
    {
        public abstract RTType type { get; }
        protected abstract object BlitRT(int tileCount, int tileIndex, int width, int height);
        protected abstract object PresentRT(int width, int height);
        public abstract void Release();

        public RenderTexture BlitRenderTexture (int tileCount, int tileIndex, int width, int height)
        {
            var rt = BlitRT(tileCount, tileIndex, width, height);
            if (!(rt is RenderTexture))
                throw new System.Exception("Blit RT is not a RenderTexture.");
            return rt as RenderTexture;
        }

        public RTHandle BlitRTHandle (int tileCount, int tileIndex, int width, int height)
        {
            var rt = BlitRT(tileCount, tileIndex, width, height);
            if (!(rt is RTHandle))
                throw new System.Exception("Blit RT is not a RenderTexture.");
            return rt as RTHandle;
        }

        public RenderTexture PresentRenderTexture (int width, int height)
        {
            var rt = PresentRT(width, height);
            if (!(rt is RenderTexture))
                throw new System.Exception("Blit RT is not a RenderTexture.");
            return rt as RenderTexture;
        }

        public RTHandle PresentRTHandle (int width, int height)
        {
            var rt = PresentRT(width, height);
            if (!(rt is RTHandle))
                throw new System.Exception("Blit RT is not a RenderTexture.");
            return rt as RTHandle;
        }
    }
}
