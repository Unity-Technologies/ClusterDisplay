using UnityEngine;

// TODO Really needed?
namespace Unity.ClusterDisplay.Graphics.Tests
{
    public struct ScopedShaderKeyword
    {
        readonly string m_Keyword;

        public ScopedShaderKeyword(string keyword)
        {
            m_Keyword = keyword;
            Shader.EnableKeyword(m_Keyword);
        }

        public void Dispose()
        {
            Shader.DisableKeyword(m_Keyword);
        }
    }
}
