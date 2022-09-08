using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Unity.ClusterDisplay.MissionControl.HangarBay.Library;

namespace Unity.ClusterDisplay.MissionControl.HangarBay.Tests
{
    internal class HangarBayProcessHelper : IDisposable
    {
        public async Task Start(string path, string fromPath = "")
        {
            if (string.IsNullOrEmpty(fromPath))
            {
                fromPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
                fromPath = fromPath.Replace("HangarBay.Tests", "HangarBay");
            }

            Assert.That(m_Process, Is.Null);
            var startInfo = new ProcessStartInfo()
            {
                FileName = Path.Combine(fromPath, "HangarBay.exe"),
                Arguments = $"-masterPid {Process.GetCurrentProcess().Id.ToString()} -c \"{path}/config.json\" " +
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
                Assert.That(m_Process.HasExited, Is.False);
                try
                {
                    var status = await m_HttpClient.GetFromJsonAsync<Status>("status");
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
        }

        public void Stop()
        {
            Assert.That(m_Process, Is.Not.Null);
            Assert.That(m_HttpClient, Is.Not.Null);

            // Send a shutdown command
            var stopwatch = Stopwatch.StartNew();
            var shutdownTask = PostCommand<ShutdownCommand>(new());
            if (shutdownTask.Wait(k_ConnectionTimeout - stopwatch.Elapsed))
            {
                Assert.That(shutdownTask.Result.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));
            }
            bool waitRet = m_Process.WaitForExit((int)(k_ConnectionTimeout - stopwatch.Elapsed).TotalMilliseconds);
            if (!waitRet)
            {
                m_Process.Kill();
            }
            Assert.That(waitRet, Is.True);
            m_Process = null;
        }

        public Task<Config> GetConfig()
        {
            Assert.That(m_HttpClient, Is.Not.Null);
            return m_HttpClient.GetFromJsonAsync<Config>("config", Json.SerializerOptions);
        }

        public Task<HttpResponseMessage> PutConfig(Config config)
        {
            Assert.That(m_HttpClient, Is.Not.Null);
            return m_HttpClient.PutAsJsonAsync("config", config, Json.SerializerOptions);
        }

        public Task<Status?> GetStatus()
        {
            Assert.That(m_HttpClient, Is.Not.Null);
            return m_HttpClient.GetFromJsonAsync<Status>("status", Json.SerializerOptions);
        }

        public Task<HttpResponseMessage> PostCommand<T>(T command) where T: Command
        {
            Assert.That(m_HttpClient, Is.Not.Null);
            return m_HttpClient.PostAsJsonAsync("commands", command, Json.SerializerOptions);
        }

        public async Task<string[]> GetErrorDetails(HttpContent response)
        {
            string errorDetailsString = await response.ReadAsStringAsync();
            var ret = JsonSerializer.Deserialize<string[]>(errorDetailsString);
            Assert.That(ret, Is.Not.Null);
            return ret;
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
