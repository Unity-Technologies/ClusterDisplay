﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using GraphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat;

namespace Unity.ClusterDisplay.Graphics
{
    public class XRTileRTManager : TileRTManager
    {
        private RTHandle m_BlitRT;
        private RTHandle m_PresentRT;
        private RTHandle m_BackBufferRT;

        protected override object GetBlitRT(int width, int height, GraphicsFormat format)
        {
            bool resized = 
                m_BlitRT != null && 
                (m_BlitRT.rt.width != (int)width || 
                m_BlitRT.rt.height != (int)height);

            if (m_BlitRT == null || resized)
            {
                if (m_BlitRT != null)
                    RTHandles.Release(m_BlitRT);

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

        protected override object GetPresentRT(int width, int height, GraphicsFormat format)
        {
            bool resized = 
                m_PresentRT != null && 
                (m_PresentRT.rt.width != width || 
                m_PresentRT.rt.height != height);

            if (m_PresentRT == null || resized)
            {
                if (m_PresentRT != null)
                    RTHandles.Release(m_PresentRT);

                m_PresentRT = RTHandles.Alloc(
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

            return m_PresentRT;
        }

        protected override object GetBackBufferRT(int width, int height, GraphicsFormat format)
        {
            bool resized = 
                m_BackBufferRT != null && 
                (m_BackBufferRT.rt.width != width || 
                m_BackBufferRT.rt.height != height);

            if (m_BackBufferRT == null || resized)
            {
                if (m_BackBufferRT != null)
                    RTHandles.Release(m_BackBufferRT);

                m_BackBufferRT = RTHandles.Alloc(
                    width: (int)width, 
                    height: (int)height,
                    slices: 1,
                    useDynamicScale: true,
                    autoGenerateMips: false,
                    enableRandomWrite: true,
                    filterMode: FilterMode.Trilinear,
                    anisoLevel: 8,
                    name: $"BackBuffer-RT-({width}X{height})");
            }

            return m_BackBufferRT;
        }

        public override void Release()
        {
            if (m_BlitRT != null)
                RTHandles.Release(m_BlitRT);

            if (m_PresentRT != null)
                RTHandles.Release(m_PresentRT);

            if (m_BackBufferRT != null)
                RTHandles.Release(m_BackBufferRT);

            m_BlitRT = null;
            m_PresentRT = null;
            m_BackBufferRT = null;
        }
    }
}
