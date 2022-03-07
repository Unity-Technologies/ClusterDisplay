using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Unity.ClusterDisplay.MissionControl
{
    public static class ConsoleHelper
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool AllocConsole();

        /// <summary>
        /// Open a console window and set up outputs.
        /// </summary>
        public static bool CreateConsole()
        {
            return AllocConsole();
        }
    }
}
