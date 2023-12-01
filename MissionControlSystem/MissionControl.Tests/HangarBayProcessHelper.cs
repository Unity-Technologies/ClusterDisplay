using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;

using HangarBayConfig = Unity.ClusterDisplay.MissionControl.HangarBay.Config;
using HangarBayStatus = Unity.ClusterDisplay.MissionControl.HangarBay.Status;
using HangarBayCommand = Unity.ClusterDisplay.MissionControl.HangarBay.Command;
using HangarBayShutdownCommand = Unity.ClusterDisplay.MissionControl.HangarBay.ShutdownCommand;
using HangarBayStorageFolderConfig = Unity.ClusterDisplay.MissionControl.HangarBay.StorageFolderConfig;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    class HangarBayProcessHelper : IDisposable
    {
        public async Task Start(string path, string fromPath = "")
        {
            if (string.IsNullOrEmpty(fromPath))
            {
                fromPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
                fromPath = fromPath.Replace("MissionControl.Tests", "HangarBay");
            }

            Assert.That(m_Process, Is.Null);
            var startInfo = new ProcessStartInfo()
            {
                FileName = Path.Combine(fromPath, "HangarBay.exe"),
                Arguments = $"--masterPid {Process.GetCurrentProcess().Id.ToString()} -c \"{path}/config.json\" " +
                    $"-p \"{path}/payloads\"",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Minimized,
                WorkingDirectory = fromPath
            };
            m_Process = Process.Start(startInfo);
            Assert.That(m_Process, Is.Not.Null);

            m_HttpClient = new();
            m_HttpClient.BaseAddress = new Uri("http://127.0.0.1:8100/api/v1/");
            Stopwatch startDeadline = Stopwatch.StartNew();
            while (startDeadline.Elapsed < k_ConnectionTimeout)
            {
                Assert.That(m_Process!.HasExited, Is.False);
                try
                {
                    var status = await m_HttpClient.GetFromJsonAsync<Status>("status", Json.SerializerOptions);
                    if (status != null)
                    {
                        break;
                    }
                }
                catch(Exception)
                {
                    // Let's still wait and try in a short delay, process might still be starting
                    await Task.Delay(250);
                }
            }

            // Set storage folder for hangar bay cache.
            var config = await GetConfig();
            config.StorageFolders = new[] {
                new HangarBayStorageFolderConfig() { Path = Path.Combine(path, "Cache"), MaximumSize = 1024 * 1024 * 1024 }
            };
            await PutConfig(config);
        }

        // ReSharper disable once MemberCanBePrivate.Global -> Kept public because future tests might need it
        public void Stop()
        {
            Assert.That(m_Process, Is.Not.Null);
            Assert.That(m_HttpClient, Is.Not.Null);

            // Send a shutdown command
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var shutdownTask = PostCommandWithStatusCode(new HangarBayShutdownCommand());
                if (shutdownTask.Wait(k_ConnectionTimeout - stopwatch.Elapsed))
                {
                    Assert.That(shutdownTask.Result, Is.EqualTo(HttpStatusCode.Accepted));
                }
            }
            catch (Exception)
            {
                // Unlikely be maybe possible, the process might had the time to exit (or close connections) before
                // completing sending the response.  So let's just skip the exception and continue waiting for the
                // process to exit.
            }
            int waitMilliseconds = Math.Max((int)(k_ConnectionTimeout - stopwatch.Elapsed).TotalMilliseconds, 0);
            bool waitRet = m_Process!.WaitForExit(waitMilliseconds);
            if (!waitRet)
            {
                m_Process.Kill();
            }
            Assert.That(waitRet, Is.True);
            m_Process = null;
        }


        // ReSharper disable once MemberCanBePrivate.Global -> Kept public because future tests might need it
        public Task<HangarBayConfig> GetConfig()
        {
            Assert.That(m_HttpClient, Is.Not.Null);
            return m_HttpClient!.GetFromJsonAsync<HangarBayConfig>("config", Json.SerializerOptions);
        }

        // ReSharper disable once MemberCanBePrivate.Global -> Kept public because future tests might need it
        public async Task PutConfig(HangarBayConfig config)
        {
            Assert.That(m_HttpClient, Is.Not.Null);
            var ret = await m_HttpClient!.PutAsJsonAsync("config", config, Json.SerializerOptions);
            ret.EnsureSuccessStatusCode();
        }

        public async Task<HangarBayStatus> GetStatus()
        {
            Assert.That(m_HttpClient, Is.Not.Null);
            var ret = await m_HttpClient!.GetFromJsonAsync<HangarBayStatus>("status", Json.SerializerOptions);
            Assert.That(ret, Is.Not.Null);
            return ret!;
        }

        public async Task PostCommand(HangarBayCommand command)
        {
            var ret = await m_HttpClient!.PostAsJsonAsync("commands", command, Json.SerializerOptions);
            Assert.That(ret.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        // ReSharper disable once MemberCanBePrivate.Global -> Kept public because future tests might need it
        public async Task<HttpStatusCode> PostCommandWithStatusCode(HangarBayCommand command)
        {
            var ret = await m_HttpClient!.PostAsJsonAsync("commands", command, Json.SerializerOptions);
            return ret.StatusCode;
        }

        public async Task<string[]> GetErrorDetails(HttpContent response)
        {
            string errorDetailsString = await response.ReadAsStringAsync();
            var ret = JsonSerializer.Deserialize<string[]>(errorDetailsString);
            Assert.That(ret, Is.Not.Null);
            return ret!;
        }

        public void Dispose()
        {
            if (m_Process != null)
            {
                Stop();
            }
        }

        static readonly TimeSpan k_ConnectionTimeout = TimeSpan.FromSeconds(30);

        Process? m_Process;
        HttpClient? m_HttpClient;
    }
}
