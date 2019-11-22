using System.Diagnostics;

namespace Unity.ClusterRendering
{
    internal class DebugPerf
    {
        private Stopwatch m_Time = new Stopwatch();

        private long[] m_Measurements = new long[20];
        private long m_ReferencePoint;
        private int m_SamplesCursor = 0;

        public DebugPerf()
        {
            m_Time.Start();
        }

        public void SampleNow()
        {
            m_Measurements[m_SamplesCursor] = m_Time.ElapsedTicks - m_ReferencePoint;
            m_SamplesCursor = (m_SamplesCursor + 1) % m_Measurements.Length;
        }

        public void RefPoint()
        {
            m_ReferencePoint = m_Time.ElapsedTicks;
        }

        public float Average
        {
            get
            {
                long total = 0;
                for (int i = 0; i < m_Measurements.Length; i++)
                    total += m_Measurements[i];

                var avg = ((double)total) / Stopwatch.Frequency / m_Measurements.Length;
                return (float)avg;
            }
        }

    }
}
