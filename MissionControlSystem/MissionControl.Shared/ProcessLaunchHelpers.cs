using System;
using System.Diagnostics;
using System.Reflection;
using System.Web;

namespace Unity.ClusterDisplay.MissionControl
{
    /// <summary>
    /// Various helpers used when launching processes.
    /// </summary>
    public static class ProcessLaunchHelpers
    {
        /// <summary>
        /// Analyze the <see cref="ProcessStartInfo"/> and modify it to allow starting some "non standard" executables.
        /// </summary>
        /// <param name="processStartInfo">Information to launch the process.</param>
        public static void PrepareProcessStartInfo(ProcessStartInfo processStartInfo)
        {
            if (AdaptProcessStartInfoForPs1(processStartInfo))
            {
                return;
            }
            if (AdaptProcessStartInfoForAssemblyRun(processStartInfo))
            {
                return;
            }

            // Look like it is a "vanilla" executable.  We need to change the filename to an absolute (looks like
            // Process.Start does not consider the WorkingDirectory when searching the .exe to start).
            processStartInfo.FileName = Path.Combine(processStartInfo.WorkingDirectory, processStartInfo.FileName);
        }

        /// <summary>
        /// Transform a ProcessStartInfo that has a .ps1 as the FileName so that it invokes PowerShell to launch it.
        /// </summary>
        /// <param name="processStartInfo">Information to launch the process.</param>
        /// <remarks>Does nothing if FileName is not the path to a .ps1.</remarks>
        /// <returns>Was it a .ps1 (and so preparation is complete).</returns>
        static bool AdaptProcessStartInfoForPs1(ProcessStartInfo processStartInfo)
        {
            if (!processStartInfo.FileName.EndsWith(".ps1"))
            {
                return false;
            }

            processStartInfo.Arguments = "-File \"" + processStartInfo.FileName + "\" " + processStartInfo.Arguments;
            processStartInfo.FileName = "powershell.exe";
            return true;
        }

        /// <summary>
        /// Transform a ProcessStartInfo that has an assemblyrun:// url as the FileName so that it invokes AssemblyRun
        /// to execute a static method in a class of a .Net assembly.
        /// </summary>
        /// <param name="processStartInfo">Information to launch the process.</param>
        /// <remarks>Does nothing if FileName is not an assemblyrun:// url.  An assemblyrun:// url is an url where each
        /// part of the path consist in an argument to be passed to AssemblyRun.exe.  Each argument must be url encoded
        /// if it contains characters that could conflict with parsing of a url.</remarks>
        /// <returns>Was it an assemblyrun:// url (and so preparation is complete).</returns>
        static bool AdaptProcessStartInfoForAssemblyRun(ProcessStartInfo processStartInfo)
        {
            const string prefix = "assemblyrun://";
            if (!processStartInfo.FileName.ToLower().StartsWith(prefix))
            {
                return false;
            }

            // Split the assemblyrun:// url into its components
            var commandLineArguments = processStartInfo.FileName.Substring(prefix.Length).Split('/');
            if (commandLineArguments.Length < 3)
            {
                throw new ArgumentException("assemblyrun url must have the following format: " +
                    "\"assemblyrun://relative assembly path/class name/method name\" and be optionally followed by " +
                    "string arguments (separated by slashes).");
            }
            commandLineArguments[0] = Path.Combine(processStartInfo.WorkingDirectory, commandLineArguments[0]);

            // Prepare each command line arguments
            for (int argumentIdx = 0; argumentIdx < commandLineArguments.Length; ++argumentIdx)
            {
                var currentArgument = commandLineArguments[argumentIdx];
                currentArgument = HttpUtility.UrlDecode(currentArgument);
                if (currentArgument.Contains(' '))
                {
                    currentArgument = '\"' + currentArgument + '\"';
                }
                commandLineArguments[argumentIdx] = currentArgument;
            }

            // Find the location of AssemblyRun.exe.
            var assemblyFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var assemblyRunPath = Path.Combine(assemblyFolder!, "AssemblyRun.exe");

            // Update ProcessStartInfo
            processStartInfo.FileName = assemblyRunPath;
            processStartInfo.Arguments = string.Join(' ', commandLineArguments);

            return true;
        }
    }
}
