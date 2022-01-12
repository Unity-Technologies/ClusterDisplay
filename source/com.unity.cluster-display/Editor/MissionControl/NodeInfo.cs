using System.Net;
using System.Runtime.InteropServices;

namespace Unity.ClusterDisplay.MissionControl
{
    /// <summary>
    /// A struct that contains information about a server instance.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Unicode)]
    public readonly struct NodeInfo
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = Constants.StringMaxLength + 1)]
        readonly string Name;
    }
}
