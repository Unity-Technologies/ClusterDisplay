using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Unity.ClusterDisplay.MissionControl
{
    static class Launcher
    {
        const string k_CopyParams = "/MIR /FFT /Z /XA:H";

        static string ExecutablePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ClusterDisplayBuilds");

        public static string GetLocalProjectDir(string sharedProjectDir)
        {
            var projectName = new DirectoryInfo(sharedProjectDir).Name;
            return Path.Combine(ExecutablePath, projectName);
        }

        public static async Task SyncProjectDir(string sharedProjectDir, CancellationToken token)
        {
            var localProjectionDir = GetLocalProjectDir(sharedProjectDir);
            if (!Directory.Exists(localProjectionDir))
            {
                Directory.CreateDirectory(localProjectionDir);
            }

            Trace.WriteLine($"Syncing project from {sharedProjectDir} to {localProjectionDir}");

            var copyProcess = new Process
            {
                StartInfo =
                {
                    FileName = "robocopy.exe",
                    Arguments = $"\"{sharedProjectDir}\" \"{localProjectionDir}\" {k_CopyParams}"
                }
            };
            Trace.WriteLine($"{copyProcess.StartInfo.FileName} {copyProcess.StartInfo.Arguments}");

            copyProcess.Start();
            try
            {
                await copyProcess.WaitForExitAsync(token);
                if ((copyProcess.ExitCode & 0x08) != 0)
                {
                    throw new Exception("Robocopy encountered errors");
                }
            }
            finally
            {
                if (!copyProcess.HasExited)
                {
                    copyProcess.Kill();
                }
            }
        }

        static string GetCommandLineArgString(in LaunchInfo launchInfo)
        {
            var address = new IPAddress(launchInfo.MulticastAddress).ToString();
            var isEmitter = launchInfo.NodeID == 0;
            const string emitterArg = "-emitterNode";
            const string repeaterArg = "-node";

            var args = new List<string>
            {
                isEmitter ? emitterArg : repeaterArg,
                launchInfo.NodeID.ToString(),
                isEmitter ? launchInfo.NumRepeaters.ToString() : string.Empty,
                $"{address}:{launchInfo.Port}",
                "-handshakeTimeout",
                launchInfo.HandshakeTimeoutMilliseconds.ToString(),
                "-communicationTimeout",
                launchInfo.TimeoutMilliseconds.ToString(),
                launchInfo.ExtraArgs
            };
            return string.Join(" ", args);
        }

        public static async Task Launch(
            LaunchInfo launchInfo,
            CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (!FolderUtils.TryGetPlayerInfo(GetLocalProjectDir(launchInfo.PlayerDir), out var playerInfo))
            {
                throw new ArgumentException("Folder does not contain an executable");
            }

            var isUnityPlayer = !string.IsNullOrEmpty(playerInfo.ProductName);
            if (isUnityPlayer && launchInfo.ClearRegistry)
            {
                try
                {
                    FolderUtils.DeleteRegistryKey(playerInfo.CompanyName, playerInfo.ProductName);
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"Could not clear the registry key: {ex.Message}");
                }
            }

            var argString = isUnityPlayer ? GetCommandLineArgString(launchInfo) : launchInfo.ExtraArgs;
            var runProjectProcess = new Process
            {
                StartInfo =
                {
                    FileName = $"\"{playerInfo.ExecutablePath}\"",
                    Arguments = argString,
                    WorkingDirectory = Directory.GetParent(playerInfo.ExecutablePath)?.FullName ?? string.Empty
                }
            };

            Trace.WriteLine($"Running...\n{playerInfo.ExecutablePath} {argString}");
            runProjectProcess.Start();

            try
            {
                await runProjectProcess.WaitForExitAsync(token);
            }
            catch (Exception e)
            {
                Trace.WriteLine(e.Message);
            }
            finally
            {
                if (!runProjectProcess.HasExited)
                {
                    runProjectProcess.Kill();
                }

                Trace.WriteLine($"Process exited with code {runProjectProcess.ExitCode}");
            }
        }
    }
}
