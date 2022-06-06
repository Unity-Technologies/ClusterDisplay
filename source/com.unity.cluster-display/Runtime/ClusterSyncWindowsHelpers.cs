using System;
using System.Runtime.InteropServices;

#if UNITY_STANDALONE_WIN
namespace Unity.ClusterDisplay
{
    /// <summary>
    /// Helpers for Windows OS related features of <see cref="ClusterSync"/>.
    /// </summary>
    internal static class ClusterSyncWindowsHelpers
    {
        // Various Win32 functions and constants to access the registry (since we are using .Net Standard that does not
        // include any access to the registry).
        [DllImport("advapi32.dll")]
        static extern int RegOpenKeyEx(UIntPtr hKey, string subKey, int ulOptions, int samDesired, out UIntPtr hkResult);
        [DllImport("advapi32.dll", SetLastError = true)]
        static extern int RegCloseKey(UIntPtr hKey);
        [DllImport("advapi32.dll", SetLastError = true)]
        static extern uint RegQueryValueEx(UIntPtr hKey, string lpValueName, int lpReserved, out int lpType,
            IntPtr lpData, ref int lpcbData);

        static UIntPtr k_HKLM = new UIntPtr(0x80000002u);

        const int k_ErrorSuccess = 0;

        const int k_ValueKindDword = 4;
        
        const int k_KeyReadAccess = 0x20019; // KEY_READ
        const int k_KeyWOW64 = 0x0100; // KEY_WOW64_64KEY

        /// <summary>
        /// Helper function that check if the OS setup is optimal to run an Cluster Display node.
        /// </summary>
        /// <remarks>It currently check if Multimedia Class Scheduler Service (MMCSS) network throttlingIndex is disabled
        /// as tests shown that it seriously increase latency and reduces so frame rate.  It will still continue if not
        /// properly configured but will generate a warning.</remarks>
        static public void CheckNodeSetup()
        {
            UIntPtr keyHandle = UIntPtr.Zero;
            IntPtr valueIntPtr = IntPtr.Zero;
            try
            {
                if (RegOpenKeyEx(k_HKLM, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile",
                        0, k_KeyReadAccess | k_KeyWOW64, out keyHandle) == k_ErrorSuccess)
                {
                    int valueKind;
                    int valueSize = (int)Marshal.SizeOf<uint>();
                    valueIntPtr = Marshal.AllocHGlobal(valueSize);
                    uint ret = RegQueryValueEx(keyHandle, "NetworkThrottlingIndex", 0, out valueKind,
                        valueIntPtr, ref valueSize);
                    if (ret == k_ErrorSuccess && valueKind == k_ValueKindDword && valueSize == Marshal.SizeOf<uint>())
                    {
                        var networkThrottlingIndex = (uint)Marshal.ReadInt32(valueIntPtr);
                        if (networkThrottlingIndex != uint.MaxValue)
                        {
                            ClusterDebug.LogWarning(
                                "Multimedia Class Scheduler Service (MMCSS) NetworkThrottlingIndex not set to 0xFFFFFFFF which can increase network latency and so reduce framerate.");
                        }
                    }
                    else
                    {
                        ClusterDebug.LogWarning(
                            "Failed validation of Multimedia Class Scheduler Service (MMCSS) NetworkThrottlingIndex.  Most likely the key is missing which is the equivalent of the default behavior, consider fixing the registry key to improve network latency and at the same time framerate.");
                    }
                }
            }
            finally
            {
                if (keyHandle != UIntPtr.Zero)
                {
                    RegCloseKey(keyHandle);
                }
                if (valueIntPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(valueIntPtr);
                }
            }
        }
    }
}
#endif
