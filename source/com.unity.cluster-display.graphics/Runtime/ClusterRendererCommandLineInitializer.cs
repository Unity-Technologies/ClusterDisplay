using UnityEngine;

namespace Unity.ClusterDisplay.Graphics
{
    // TODO naming way too vague
    [CreateAssetMenu(fileName = "ClusterRendererCommandLineInitializer", menuName = "Cluster Display/ClusterRendererCommandLineInitializer", order = 1)]
    class ClusterRendererCommandLineInitializer : 
        SingletonScriptableObject<ClusterRendererCommandLineInitializer>,
        IClusterDisplayConfigurable
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
            if (!ClusterRenderer.TryGetInstance(out m_ClusterRenderer))
                return;
                
            if (ApplicationUtil.CommandLineArgExists(CommandLineArgs.k_Debug))
                m_ClusterRenderer.context.debug = true;

            Vector2Int gridSize;
            if (ApplicationUtil.ParseCommandLineArgs(CommandLineArgs.k_GridSize, out gridSize))
                m_ClusterRenderer.settings.gridSize = gridSize;
           
            Vector2 bezel;
            if (ApplicationUtil.ParseCommandLineArgs(CommandLineArgs.k_Bezel, out bezel))
                m_ClusterRenderer.settings.bezel = bezel;
            
            Vector2 physicalScreenSize;
            if (ApplicationUtil.ParseCommandLineArgs(CommandLineArgs.k_PhysicalScreenSize, out physicalScreenSize))
                m_ClusterRenderer.settings.physicalScreenSize = physicalScreenSize;

            int overscanInPixels;
            if (ApplicationUtil.ParseCommandLineArgs(CommandLineArgs.k_Overscan, out overscanInPixels))
                m_ClusterRenderer.settings.overScanInPixels = overscanInPixels;
        }

        protected override void OnAwake()
        {
        }
    }
}
