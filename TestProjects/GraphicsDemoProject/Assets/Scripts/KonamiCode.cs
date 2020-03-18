using System;
using UnityEngine;

namespace Unity.ClusterRendering.Toolkit
{
    public class KonamiCode
    {
        public event Action OnActivate = delegate { };
        
        string m_Code;
        int m_Index = 0;

        public KonamiCode(string code) { m_Code = code; }

        public void Reset() { m_Index = 0; }

        public void Update()
        {
            foreach (var c in Input.inputString)
            {
                m_Index = c == m_Code[m_Index] ? m_Index + 1 : 0;

                if (m_Index == m_Code.Length - 1)
                {
                    OnActivate.Invoke();
                    m_Index = 0;
                }
            }
        }
    }
}
