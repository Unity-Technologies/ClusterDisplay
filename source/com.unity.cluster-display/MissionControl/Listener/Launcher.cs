﻿using System;
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

        public static string GetLocalProjectDir(string sharedProjectDir)
        {
            var projectName = new DirectoryInfo(sharedProjectDir).Name;
            return Path.Combine(k_LocalPath, projectName);
        }
        
        public static async Task SyncProjectDir(string sharedProjectDir, CancellationToken token)
        {
            var localProjectionDir = GetLocalProjectDir(sharedProjectDir);

            Console.WriteLine($"Syncing project from {sharedProjectDir} to {localProjectionDir}");

            var copyProcess = new Process
            {
                StartInfo =
                {
                    FileName = "robocopy.exe",
                    Arguments = $"{sharedProjectDir} {localProjectionDir} {k_CopyParams}"
                }
            };
            Console.WriteLine($"{copyProcess.StartInfo.FileName} {copyProcess.StartInfo.Arguments}");
            //
            // await Task.Delay(30000, token);
            // return;
            
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

        public static async Task Launch(
            LaunchInfo launchInfo,
            CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var localProjectionDir = GetLocalProjectDir(launchInfo.ProjectDir);
            var dirInfo = new DirectoryInfo(localProjectionDir);
            var exePath = dirInfo.GetFiles("*.exe").FirstOrDefault()?.FullName;
            if (exePath == null)
            {
                Console.WriteLine($"No executable found in project directory.");
                return;
            }

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

            try
            {
                await runProjectProcess.WaitForExitAsync(token);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
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
