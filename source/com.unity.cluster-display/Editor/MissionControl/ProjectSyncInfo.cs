using System;
using System.Runtime.InteropServices;

namespace Unity.ClusterDisplay.MissionControl
{
    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Unicode)]
    public struct ProjectSyncInfo
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = Constants.PathMaxLength)]
        public readonly string SharedProjectDir;

        public ProjectSyncInfo(string sharedProjectDir)
        {
            SharedProjectDir = sharedProjectDir;
        }
    }
}
