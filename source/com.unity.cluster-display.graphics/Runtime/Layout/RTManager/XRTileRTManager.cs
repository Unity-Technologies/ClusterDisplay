using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class XRTileRTManager : TileRTManager
{
    public override RTType Type => RTType.Handle;
    private RTHandle m_BlitRT;

    protected override object BlitRT(int width, int height)
    {
        bool resized = 
            m_BlitRT != null && 
            (m_BlitRT.rt.width != (int)width || 
            m_BlitRT.rt.height != (int)height);

        if (m_BlitRT == null || resized)
        {
            if (m_BlitRT != null)
            {
                // if (camera.targetTexture != null && camera.targetTexture == m_BlitRT)
                //     camera.targetTexture = null;
                RTHandles.Release(m_BlitRT);
            }

            m_BlitRT = RTHandles.Alloc(
                width: (int)width, 
                height: (int)height, 
                slices: 1, 
                useDynamicScale: true, 
                autoGenerateMips: false, 
                enableRandomWrite: true,
                filterMode: FilterMode.Trilinear,
                anisoLevel: 8,
                name: "Overscanned Target");
        }

        return m_BlitRT;
    }

    protected override object PresentRT(int width, int height)
    {
        return null;
    }

    public override void Release()
    {
    }
}
