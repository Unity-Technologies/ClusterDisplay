using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Unity.ClusterDisplay.MissionControl
{
    static class Launcher
    {
        static readonly string k_LocalPath = Environment.SpecialFolder.LocalApplicationData + "\\ClusterDisplayBuilds";
        const string k_CopyParams = "/MIR /FFT /Z /XA:H";

        static async Task<string> CopyProjectDir(LaunchInfo launchInfo, CancellationToken token)
        {
            var projectName = new DirectoryInfo(launchInfo.ProjectDir).Name;
            var localProjectionDir = Path.Combine(k_LocalPath, projectName);

            Console.WriteLine($"Syncing project from {launchInfo.ProjectDir} to {localProjectionDir}");

            var copyProcess = new Process
            {
                StartInfo =
                {
                    FileName = "robocopy.exe",
                    Arguments = $"{launchInfo.ProjectDir} {localProjectionDir} {k_CopyParams}"
                }
            };
            Console.WriteLine($"{copyProcess.StartInfo.FileName} {copyProcess.StartInfo.Arguments}");
            
            copyProcess.Start();
            try
            {
                await copyProcess.WaitForExitAsync(token);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
            finally
            {
                if (!copyProcess.HasExited)
                {
                    copyProcess.Kill();
                }
            }

            var dirInfo = new DirectoryInfo(localProjectionDir);
            return dirInfo.GetFiles("*.exe").FirstOrDefault()?.FullName;
        }

        static string GetCommandLineArgString(in LaunchInfo launchInfo)
        {
            var outgoingPort = launchInfo.NodeID == 0 ? launchInfo.RxPort.ToString() : launchInfo.TxPort.ToString();
            var incomingPort = launchInfo.NodeID == 0 ? launchInfo.TxPort.ToString() : launchInfo.RxPort.ToString();
            var address = new IPAddress(launchInfo.MulticastAddress).ToString();
            var args = new List<string>
            {
                launchInfo.NodeID == 0 ? "-masterNode" : "-node",
                launchInfo.NodeID.ToString(),
                launchInfo.NodeID == 0 ? launchInfo.NumRepeaters.ToString() : string.Empty,
                $"{address}:{outgoingPort},{incomingPort}",
                "-handshakeTimeout",
                launchInfo.HandshakeTimeoutMilliseconds.ToString(),
                "-communicationTimeout",
                launchInfo.TimeoutMilliseconds.ToString()
            };
            return string.Join(" ", args);
        }

        public static async IAsyncEnumerable<NodeStatus> Launch(
            LaunchInfo launchInfo,
            [EnumeratorCancellation] CancellationToken token)
        {
            yield return NodeStatus.SyncFiles;
            token.ThrowIfCancellationRequested();
            var exePath = await CopyProjectDir(launchInfo, token);

            if (exePath == null)
            {
                yield return NodeStatus.Error;
                yield break;
            }

            yield return NodeStatus.Ready;

            var argString = GetCommandLineArgString(launchInfo);
            var runProjectProcess = new Process
            {
                StartInfo =
                {
                    FileName = exePath,
                    Arguments = argString
                }
            };

            Console.WriteLine($"Running...\n{exePath} {argString}");
            runProjectProcess.Start();
            yield return NodeStatus.Running;

            token.ThrowIfCancellationRequested();
            try
            {
                await runProjectProcess.WaitForExitAsync(token);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
            finally
            {
                if (!runProjectProcess.HasExited)
                {
                    runProjectProcess.Kill();
                }
            }
        }
    }
}
