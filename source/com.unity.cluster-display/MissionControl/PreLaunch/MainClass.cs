using System;
using System.Runtime.InteropServices;
using Newtonsoft.Json.Linq;

namespace Unity.ClusterDisplay.MissionControl.PreLaunch
{
    public static class MainClass
    {
        public static void Main()
        {
            var envLaunchData = Environment.GetEnvironmentVariable("LAUNCH_DATA");
            var envLaunchableData = Environment.GetEnvironmentVariable("LAUNCHABLE_DATA");

            JObject parsedLaunchData = string.IsNullOrEmpty(envLaunchData) ? new() : JObject.Parse(envLaunchData);
            JObject parsedLaunchableData = string.IsNullOrEmpty(envLaunchableData) ? new() : JObject.Parse(envLaunchableData);

            ClearRegistry(parsedLaunchData, parsedLaunchableData);
        }

        static void ClearRegistry(JObject parsedLaunchData, JObject parsedLaunchableData)
        {
            bool delete = parsedLaunchData.Value<bool?>(LaunchParameterConstants.DeleteRegistryKeyParameterId) ??
                LaunchParameterConstants.DefaultDeleteRegistryKey;
            if (!delete)
            {
                return;
            }
            string companyName = parsedLaunchableData.Value<string>(LaunchableDataConstants.CompanyNameProperty);
            string programName = parsedLaunchableData.Value<string>(LaunchableDataConstants.ProgramNameProperty);
            if (string.IsNullOrEmpty(companyName) || string.IsNullOrEmpty(programName))
            {
                // TODO: Log
                return;
            }

            bool ret = DeleteRegistryKey(HKEY_CURRENT_USER, $"SOFTWARE\\{companyName}\\{programName}");
            if (!ret)
            {
                // TODO: Log
            }
        }

        static bool DeleteRegistryKey(UIntPtr baseKey, string subkey)
        {
            if (string.IsNullOrEmpty(subkey))
                return false;

            UIntPtr hkey = UIntPtr.Zero;
            try
            {
                // The key must have been opened with the following access rights: DELETE, KEY_ENUMERATE_SUB_KEYS, and KEY_QUERY_VALUE.
                uint lResult = RegOpenKeyEx(baseKey, string.Empty, 0,
                    (int)REGSAM.AllAccess | (int)REGSAM.KEY_WOW64_64KEY, out hkey);
                if (lResult != SUCCESS)
                {
                    return false;
                }

                lResult = RegDeleteTree(hkey, subkey);
                if (lResult != SUCCESS)
                {
                    return false;
                }
            }
            finally
            {
                if (UIntPtr.Zero != hkey)
                {
                    RegCloseKey(hkey);
                }
            }

            return true;
        }

        public enum REGSAM
        {
            QueryValue = 0x0001,
            SetValue = 0x0002,
            CreateSubKey = 0x0004,
            EnumerateSubKeys = 0x0008,
            Notify = 0x0010,
            CreateLink = 0x0020,
            KEY_WOW64_32KEY = 0x0200,
            KEY_WOW64_64KEY = 0x0100,
            WOW64_Res = 0x0300,
            Read = 0x00020019,
            Write = 0x00020006,
            Execute = 0x00020019,
            AllAccess = 0x000f003f
        }

        static readonly UIntPtr HKEY_LOCAL_MACHINE = new UIntPtr(0x80000002u);
        static readonly UIntPtr HKEY_CURRENT_USER = new UIntPtr(0x80000001u);
        static readonly UIntPtr HKEY_CLASSES_ROOT = new UIntPtr(0x80000000u);
        static readonly UIntPtr HKEY_USERS = new UIntPtr(0x80000003u);
        static readonly UIntPtr HKEY_CURRENT_CONFIG = new UIntPtr(0x80000005u);

        const int SUCCESS = 0;

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegOpenKeyEx",
            CallingConvention = CallingConvention.Winapi, SetLastError = true)]
        static extern uint RegOpenKeyEx(UIntPtr hKey, string lpSubKey, uint ulOptions, int samDesired,
            out UIntPtr phkResult);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegCloseKey",
            CallingConvention = CallingConvention.Winapi, SetLastError = true)]
        static extern uint RegCloseKey(UIntPtr hKey);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegDeleteTree",
            CallingConvention = CallingConvention.Winapi, SetLastError = true)]
        static extern uint RegDeleteTree(UIntPtr hKey, [MarshalAs(UnmanagedType.LPWStr)]string lpSubKey);
    }
}
