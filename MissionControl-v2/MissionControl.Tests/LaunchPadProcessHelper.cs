using System.Diagnostics;
using System.Net.Http.Json;
using System.Net;
using System.Reflection;
using System.Text.Json;

using LaunchPadConfig = Unity.ClusterDisplay.MissionControl.LaunchPad.Config;
using LaunchPadStatus = Unity.ClusterDisplay.MissionControl.LaunchPad.Status;
using LaunchPadCommand = Unity.ClusterDisplay.MissionControl.LaunchPad.Command;
using LaunchPadShutdownCommand = Unity.ClusterDisplay.MissionControl.LaunchPad.ShutdownCommand;
using LaunchPadHealth = Unity.ClusterDisplay.MissionControl.LaunchPad.Health;
using LaunchPadState = Unity.ClusterDisplay.MissionControl.LaunchPad.State;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    class LaunchPadProcessHelper : IDisposable
    {
        public async Task Start(string configPath, int port = 8200, string fromPath = "")
        {
            if (string.IsNullOrEmpty(fromPath))
            {
                fromPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
                fromPath = fromPath.Replace("MissionControl.Tests", "LaunchPad");
            }

            // Create config before starting the LaunchPad process, otherwise it is too late to set the endpoint
            // without restarting (making the test longer for nothing).
            m_Id = Guid.NewGuid();
            m_EndPoint = new Uri($"http://127.0.0.1:{port}/");
            LaunchPadConfig config = new()
            {
                Identifier = m_Id,
                ControlEndPoints = new[] { m_EndPoint.ToString() }
            };
            Directory.CreateDirectory(configPath);
            await using (var fileStream = File.OpenWrite(Path.Combine(configPath, "config.json")))
            {
                fileStream.SetLength(0);
                await JsonSerializer.SerializeAsync(fileStream, config, Json.SerializerOptions);
            }

            Assert.That(m_Process, Is.Null);
            m_LaunchFolder = Path.Combine(configPath, "launchFolder");
            var startInfo = new ProcessStartInfo()
            {
                FileName = Path.Combine(fromPath, "LaunchPad.exe"),
                Arguments = $"--masterPid {Process.GetCurrentProcess().Id.ToString()} -c \"{configPath}/config.json\" " +
                    $"-l \"{m_LaunchFolder}\"",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Minimized,
                WorkingDirectory = fromPath
            };
            m_Process = Process.Start(startInfo);
            Assert.That(m_Process, Is.Not.Null);

            m_HttpClient = new();
            m_HttpClient.BaseAddress = new Uri(m_EndPoint, "api/v1/");
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
        }

        // ReSharper disable once MemberCanBePrivate.Global -> Kept public because future test might need to stop it
        public void Stop()
        {
            Assert.That(m_Process, Is.Not.Null);
            Assert.That(m_HttpClient, Is.Not.Null);

            // Send a shutdown command
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var shutdownTask = PostCommandWithStatusCode(new LaunchPadShutdownCommand());
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

        public Task<LaunchPadConfig> GetConfig()
        {
            Assert.That(m_HttpClient, Is.Not.Null);
            return m_HttpClient!.GetFromJsonAsync<LaunchPadConfig>("config", Json.SerializerOptions);
        }

        public async Task PutConfig(LaunchPadConfig config)
        {
            Assert.That(m_HttpClient, Is.Not.Null);
            var ret = await m_HttpClient!.PutAsJsonAsync("config", config, Json.SerializerOptions);
            ret.EnsureSuccessStatusCode();
        }

        // ReSharper disable once MemberCanBePrivate.Global -> Kept public because future tests might need it
        public async Task<LaunchPadStatus> GetStatus()
        {
            Assert.That(m_HttpClient, Is.Not.Null);
            var ret = await m_HttpClient!.GetFromJsonAsync<LaunchPadStatus>("status", Json.SerializerOptions);
            Assert.That(ret, Is.Not.Null);
            return ret!;
        }

        public async Task<LaunchPadHealth> GetHealth()
        {
            Assert.That(m_HttpClient, Is.Not.Null);
            var ret = await m_HttpClient!.GetFromJsonAsync<LaunchPadHealth>("health", Json.SerializerOptions);
            Assert.That(ret, Is.Not.Null);
            return ret!;
        }

        public async Task PostCommand(LaunchPadCommand command)
        {
            var ret = await m_HttpClient!.PostAsJsonAsync("commands", command, Json.SerializerOptions);
            Assert.That(ret.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        // ReSharper disable once MemberCanBePrivate.Global -> Kept public because future tests might need it
        public async Task<HttpStatusCode> PostCommandWithStatusCode(LaunchPadCommand command)
        {
            var ret = await m_HttpClient!.PostAsJsonAsync("commands", command, Json.SerializerOptions);
            return ret.StatusCode;
        }

        public void Dispose()
        {
            if (m_Process != null)
            {
                Stop();
            }
        }

        public async Task WaitForState(LaunchPadState state)
        {
            Stopwatch waitLaunched = Stopwatch.StartNew();
            while (waitLaunched.Elapsed < TimeSpan.FromSeconds(15))
            {
                if ((await GetStatus()).State == state)
                {
                    break;
                }
                await Task.Delay(25); // Wait a little bit so that the situation can change
            }
            var status = await GetStatus();
            Assert.That(status.State, Is.EqualTo(state));
        }

        public string LaunchFolder => m_LaunchFolder;
        public Guid Id => m_Id;
        public Uri EndPoint => m_EndPoint;

        static readonly TimeSpan k_ConnectionTimeout = TimeSpan.FromSeconds(30);

        Process? m_Process;
        HttpClient? m_HttpClient;
        Guid m_Id = Guid.Empty;
        Uri m_EndPoint = new Uri("http://127.0.0.1");
        string m_LaunchFolder = "";

    }
}
