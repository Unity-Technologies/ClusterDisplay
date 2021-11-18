using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using GraphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat;

namespace Unity.ClusterDisplay.Graphics
{
    public abstract class TileRTManager<T> : RTManager
    {
        protected T m_SourceRT;
        protected T m_PresentRT;

        protected T[] m_QueuedFrameRTs;
        protected long backBufferRTIndex;

        protected void PollQueuedFrameCount (int expectedQueuedFrameCount)
        {
            if (m_QueuedFrameRTs != null && m_QueuedFrameRTs.Length == expectedQueuedFrameCount)
                return;

            if (m_QueuedFrameRTs != null)
            {
                var newQueuedFrameRTs = new T[expectedQueuedFrameCount];
                for (int i = 0; i < expectedQueuedFrameCount && i < m_QueuedFrameRTs.Length; i++)
                    newQueuedFrameRTs[i] = m_QueuedFrameRTs[i];

                m_QueuedFrameRTs = newQueuedFrameRTs;
                return;
            }

            m_QueuedFrameRTs = new T[expectedQueuedFrameCount];
        }

        public abstract T GetSourceRT(int width, int height, GraphicsFormat format = defaultFormat);
        public abstract T GetPresentRT(int width, int height, GraphicsFormat format = defaultFormat);
        public abstract T GetQueuedFrameRT(int width, int height, int currentQueuedFrameCount, GraphicsFormat format = defaultFormat);
        public abstract void Release();
    }
}
