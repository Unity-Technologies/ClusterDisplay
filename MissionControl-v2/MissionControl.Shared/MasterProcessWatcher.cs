using System;
using System.Diagnostics;

namespace Unity.ClusterDisplay.MissionControl
{
    /// <summary>
    /// Helper that will terminate this process if master process is gone.
    /// </summary>
    public static class MasterProcessWatcher
    {
        /// <summary>
        /// Setup watching of master process (only if the command line parameters specifying it is present).
        /// </summary>
        public static void Setup(CancellationToken cancellationToken)
        {
            string? processIdString = null;
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-masterPid")
                {
                    processIdString = args[i + 1];
                    break;
                }
            }
            if (processIdString == null)
            {
                return;
            }

            Process toWatch;
            try
            {
                int processId = Convert.ToInt32(processIdString);
                toWatch = Process.GetProcessById(processId);
            }
            catch(Exception)
            {
                return;
            }

            toWatch.WaitForExitAsync(cancellationToken).ContinueWith(t => {
                if (!t.IsCanceled)
                {
                    Environment.Exit(-1);
                }
            }, cancellationToken);
        }
    }
}
