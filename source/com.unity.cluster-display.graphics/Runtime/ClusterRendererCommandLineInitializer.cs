using UnityEngine;

namespace Unity.ClusterDisplay.Graphics
{
    // TODO naming way too vague
    [RequireComponent(typeof(ClusterRenderer))]
    class ClusterRendererCommandLineInitializer : MonoBehaviour
    {
        static class CommandLineArgs
        {
            internal static readonly string k_Debug = "--debug"; // very common name, collision risk
            internal static readonly string k_GridSize = "--gridsize";
            internal static readonly string k_Overscan = "--overscan";
            internal static readonly string k_Bezel = "--bezel";
            internal static readonly string k_PhysicalScreenSize = "--physicalscreensize";
        }

        ClusterRenderer m_ClusterRenderer;
        
        public void OnEnable()
        {
            m_ClusterRenderer = GetComponent<ClusterRenderer>();
                
            if (ApplicationUtil.CommandLineArgExists(CommandLineArgs.k_Debug))
                m_ClusterRenderer.Context.Debug = true;

            Vector2Int gridSize;
            if (ApplicationUtil.ParseCommandLineArgs(CommandLineArgs.k_GridSize, out gridSize))
                m_ClusterRenderer.Settings.GridSize = gridSize;
           
            Vector2 bezel;
            if (ApplicationUtil.ParseCommandLineArgs(CommandLineArgs.k_Bezel, out bezel))
                m_ClusterRenderer.Settings.Bezel = bezel;
            
            Vector2 physicalScreenSize;
            if (ApplicationUtil.ParseCommandLineArgs(CommandLineArgs.k_PhysicalScreenSize, out physicalScreenSize))
                m_ClusterRenderer.Settings.PhysicalScreenSize = physicalScreenSize;

            int overscanInPixels;
            if (ApplicationUtil.ParseCommandLineArgs(CommandLineArgs.k_Overscan, out overscanInPixels))
                m_ClusterRenderer.Settings.OverscanInPixels = overscanInPixels;
        }
    }
}
