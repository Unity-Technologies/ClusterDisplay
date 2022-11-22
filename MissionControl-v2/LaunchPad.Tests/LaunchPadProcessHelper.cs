using System.Diagnostics;
using System.Net.Http.Json;
using System.Net;
using System.Reflection;
using System.Text.Json;

namespace Unity.ClusterDisplay.MissionControl.LaunchPad.Tests
{
    class LaunchPadProcessHelper : IDisposable
    {
        public async Task Start(string path, string fromPath = "", TimeSpan blockingCallMax = default)
        {
            if (string.IsNullOrEmpty(fromPath))
            {
                fromPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
                fromPath = fromPath.Replace("LaunchPad.Tests", "LaunchPad");
            }

            Assert.That(m_Process, Is.Null);
            m_LaunchFolder = Path.Combine(path, "launchFolder");
            var startInfo = new ProcessStartInfo()
            {
                FileName = Path.Combine(fromPath, "LaunchPad.exe"),
                Arguments = $"--masterPid {Process.GetCurrentProcess().Id.ToString()} -c \"{path}/config.json\" " +
                    $"-l \"{m_LaunchFolder}\"",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Minimized,
                WorkingDirectory = fromPath
            };
            if (blockingCallMax != default)
            {
                startInfo.Arguments += $" --blockingCallMaxSec {(int)blockingCallMax.TotalSeconds}";
            }
            m_Process = Process.Start(startInfo);
            Assert.That(m_Process, Is.Not.Null);

            m_HttpClient = new();
            m_HttpClient.BaseAddress = new Uri("http://127.0.0.1:8200/api/v1/");
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

        public void Stop()
        {
            Assert.That(m_Process, Is.Not.Null);
            Assert.That(m_HttpClient, Is.Not.Null);

            // Send a shutdown command
            var stopwatch = Stopwatch.StartNew();
            var shutdownTask = PostCommandWithStatusCode(new ShutdownCommand());
            if (shutdownTask.Wait(k_ConnectionTimeout - stopwatch.Elapsed))
            {
                Assert.That(shutdownTask.Result, Is.EqualTo(HttpStatusCode.Accepted));
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

        public Task<Config> GetConfig()
        {
            Assert.That(m_HttpClient, Is.Not.Null);
            return m_HttpClient!.GetFromJsonAsync<Config>("config", Json.SerializerOptions);
        }

        public Task<HttpResponseMessage> PutConfig(Config config)
        {
            Assert.That(m_HttpClient, Is.Not.Null);
            return m_HttpClient!.PutAsJsonAsync("config", config, Json.SerializerOptions);
        }

        public async Task<Status> GetStatus()
        {
            Assert.That(m_HttpClient, Is.Not.Null);
            var ret = await m_HttpClient!.GetFromJsonAsync<Status>("status", Json.SerializerOptions);
            Assert.That(ret, Is.Not.Null);
            return ret!;
        }

        public async Task<Status?> GetStatus(ulong minStatusNumber)
        {
            Assert.That(m_HttpClient, Is.Not.Null);

            // Convert lastChanged to a string using JsonSerializer to be sure we get the same value
            var response = await m_HttpClient!.GetAsync($"status?minStatusNumber={minStatusNumber}");
            response.EnsureSuccessStatusCode();
            if (response.StatusCode == HttpStatusCode.NoContent)
            {
                return null;
            }
            return JsonSerializer.Deserialize<Status>(await response.Content.ReadAsStreamAsync(), Json.SerializerOptions);
        }

        public async Task<Health> GetHealth()
        {
            Assert.That(m_HttpClient, Is.Not.Null);
            var ret = await m_HttpClient!.GetFromJsonAsync<Health>("health", Json.SerializerOptions);
            Assert.That(ret, Is.Not.Null);
            return ret!;
        }

        public async Task PostCommand(Command command, HttpStatusCode expectedRet = HttpStatusCode.OK)
        {
            Assert.That(m_HttpClient, Is.Not.Null);
            var ret = await m_HttpClient!.PostAsJsonAsync("commands", command, Json.SerializerOptions);
            Assert.That(ret.StatusCode, Is.EqualTo(expectedRet));
        }

        public async Task<HttpStatusCode> PostCommandWithStatusCode(Command command)
        {
            Assert.That(m_HttpClient, Is.Not.Null);
            var ret = await m_HttpClient!.PostAsJsonAsync("commands", command, Json.SerializerOptions);
            return ret.StatusCode;
        }

        public Task<HttpResponseMessage> PostCommandWithResponse(Command command)
        {
            Assert.That(m_HttpClient, Is.Not.Null);
            return m_HttpClient!.PostAsJsonAsync("commands", command, Json.SerializerOptions);
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

        public async Task WaitForState(State state)
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
            Assert.That(status, Is.Not.Null);
            Assert.That(status.State, Is.EqualTo(state));
        }

        public string LaunchFolder => m_LaunchFolder;

        static readonly TimeSpan k_ConnectionTimeout = TimeSpan.FromSeconds(30);

        Process? m_Process;
        HttpClient? m_HttpClient;
        string m_LaunchFolder = "";
    }
}
