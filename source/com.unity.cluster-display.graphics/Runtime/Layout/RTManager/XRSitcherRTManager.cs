using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class XRSitcherRTManager : StitcherRTManager
{
    public override RTType Type => RTType.Handle;

    private RTHandle m_PresentTarget;
    private RTHandle[] m_Targets;

    private void PollRTs (int tileCount)
    {
        if (m_Targets != null && m_Targets.Length == tileCount)
            return;
        m_Targets = new RTHandle[tileCount];
    }

    protected override object BlitRT(int tileCount, int tileIndex, int width, int height)
    {
        PollRTs(tileCount);

        bool resized = m_Targets[tileIndex] != null && 
            (m_Targets[tileIndex].rt.width != (int)width || 
            m_Targets[tileIndex].rt.height != (int)height);

        if (m_Targets[tileIndex] == null || resized)
        {
            if (m_Targets[tileIndex] != null)
                RTHandles.Release(m_Targets[tileIndex]);

            m_Targets[tileIndex] = RTHandles.Alloc(
                width: (int)width,
                height: (int)height,
                slices: 1,
                dimension: TextureXR.dimension,
                useDynamicScale: false,
                autoGenerateMips: false,
                enableRandomWrite: true,
                filterMode: FilterMode.Trilinear,
                anisoLevel: 8,
                // msaaSamples: MSAASamples.MSAA8x,
                name: $"Tile-{tileIndex}-RT-({width}X{height})");
        }

        return m_Targets[tileIndex];
    }

    protected override object PresentRT(int width, int height)
    {
        bool resized = 
            m_PresentTarget != null && 
            (m_PresentTarget.rt.width != width || 
            m_PresentTarget.rt.height != height);

        if (m_PresentTarget == null || resized)
        {
            if (m_PresentTarget != null)
                RTHandles.Release(m_PresentTarget);

            m_PresentTarget = RTHandles.Alloc(
                width: (int)width, 
                height: (int)height,
                slices: 1,
                useDynamicScale: true,
                autoGenerateMips: false,
                enableRandomWrite: true,
                filterMode: FilterMode.Trilinear,
                anisoLevel: 8,
                name: $"Present-RT-({width}X{height})");
        }

        return m_PresentTarget;
    }

    public override void Release()
    {
        if (m_PresentTarget != null)
            RTHandles.Release(m_PresentTarget);

        if (m_Targets != null)
        {
            for (var i = 0; i != m_Targets.Length; ++i)
            {
                RTHandles.Release(m_Targets[i]);
                m_Targets[i] = null;
            }
        }

        m_PresentTarget = null;
        m_Targets = null;
    }
}
