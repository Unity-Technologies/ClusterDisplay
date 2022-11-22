using System.Diagnostics;
using System.Net.Http.Json;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text;

namespace Unity.ClusterDisplay.MissionControl.MissionControl.Tests
{
    class MissionControlProcessHelper : IDisposable
    {
        public async Task Start(string configPath, bool createNewDefaultConfig = true)
        {
            m_ConfigPath = configPath;
            string exePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
            exePath = exePath.Replace("MissionControl.Tests", "MissionControl");

            // Create config before starting the MissionControl process, otherwise changing storage folder once started
            // would lead in moving all the content from the default storage folder to the new one!
            if (createNewDefaultConfig)
            {
                Config config = new()
                {
                    ControlEndPoints = new[] { "http://127.0.0.1:8000/" },
                    StorageFolders = new[] { new StorageFolderConfig()
                    {
                        Path = Path.Combine(m_ConfigPath, "testDefaultStorage"),
                        MaximumSize = 10L * 1024 * 1024 * 1024
                    } }
                };
                Directory.CreateDirectory(m_ConfigPath);
                await using var fileStream = File.OpenWrite(Path.Combine(m_ConfigPath, "config.json"));
                fileStream.SetLength(0);
                await JsonSerializer.SerializeAsync(fileStream, config, Json.SerializerOptions);
            }

            Assert.That(m_Process, Is.Null);
            var startInfo = new ProcessStartInfo()
            {
                FileName = Path.Combine(exePath, "MissionControl.exe"),
                Arguments = $"--masterPid {Process.GetCurrentProcess().Id.ToString()} -c \"{m_ConfigPath}/config.json\" -t true",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Minimized,
                WorkingDirectory = exePath
            };
            m_Process = Process.Start(startInfo);
            Assert.That(m_Process, Is.Not.Null);

            m_HttpClient = new();
            m_HttpClient.BaseAddress = new Uri("http://127.0.0.1:8000/api/v1/");
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
            var shutdownTask = PostCommandWithStatusCode<ShutdownCommand>(new());
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

        public void Kill()
        {
            if (m_Process != null)
            {
                m_Process.Kill();
                m_Process = null;
            }
        }

        public string ConfigPath => m_ConfigPath ?? string.Empty;

        public HttpClient HttpClient => m_HttpClient!;

        public Task<Config> GetConfig()
        {
            return HttpClient.GetFromJsonAsync<Config>("config", Json.SerializerOptions);
        }

        public async Task PutConfig(Config config)
        {
            var ret = await HttpClient.PutAsJsonAsync("config", config, Json.SerializerOptions);
            Assert.That(ret.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        public Task<HttpResponseMessage> PutConfigWithResponse(Config config)
        {
            return HttpClient.PutAsJsonAsync("config", config, Json.SerializerOptions);
        }

        public async Task<Status> GetStatus()
        {
            var ret = await HttpClient.GetFromJsonAsync<Status>("status", Json.SerializerOptions);
            Assert.That(ret, Is.Not.Null);
            return ret!;
        }

        public async Task<Dictionary<string, ObservableObjectUpdate>>
            GetObjectsUpdate(IEnumerable<(string name, ulong fromVersion)> updates)
        {
            StringBuilder urlBuilder = new("objectsUpdate");
            int objectIndex = 0;
            foreach (var (name, fromVersion) in updates)
            {
                urlBuilder.Append(objectIndex == 0 ? "?" : "&");
                urlBuilder.AppendFormat("name{0}={1}&fromVersion{0}={2}", objectIndex, name, fromVersion);
                ++objectIndex;
            }

            var received = await HttpClient.GetFromJsonAsync<Dictionary<string, ObservableObjectUpdate>>(
                urlBuilder.ToString(), Json.SerializerOptions);
            Assert.That(received, Is.Not.Null);

            return received!;
        }

        // ReSharper disable once MemberCanBePrivate.Global -> Could be needed by some unit tests
        public async Task PostCommand<T>(T command) where T: Command
        {
            var ret = await HttpClient.PostAsJsonAsync("commands", command, Json.SerializerOptions);
            Assert.That(ret.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        // ReSharper disable once MemberCanBePrivate.Global -> Could be needed by some unit tests
        public async Task<HttpStatusCode> PostCommandWithStatusCode<T>(T command) where T: Command
        {
            var ret = await HttpClient.PostAsJsonAsync("commands", command, Json.SerializerOptions);
            return ret.StatusCode;
        }

        public Task<Dictionary<string, JsonElement>> GetIncrementalCollectionsUpdate(
            List<(string name, ulong fromVersion)> toGet)
        {
            return GetIncrementalCollectionsUpdate(toGet, CancellationToken.None);
        }

        public async Task<Dictionary<string, JsonElement>> GetIncrementalCollectionsUpdate(
            List<(string name, ulong fromVersion)> toGet, CancellationToken cancellationToken)
        {
            StringBuilder stringBuilder = new("incrementalCollectionsUpdate");
            for (int toGetIndex = 0; toGetIndex < toGet.Count; ++toGetIndex)
            {
                (string name, ulong fromVersion) = toGet[toGetIndex];
                stringBuilder.Append(toGetIndex == 0 ? "?" : "&");
                stringBuilder.AppendFormat("name{0}={1}&fromVersion{0}={2}", toGetIndex, name, fromVersion);
            }

            var ret = await HttpClient.GetFromJsonAsync<Dictionary<string, JsonElement>>(stringBuilder.ToString(),
                Json.SerializerOptions, cancellationToken);
            Assert.That(ret, Is.Not.Null);
            return ret!;
        }

        public async Task<Guid> PostAsset(AssetPost asset)
        {
            var ret = await HttpClient.PostAsJsonAsync("assets", asset, Json.SerializerOptions);
            if (!ret.IsSuccessStatusCode)
            {
                var failureMessage = await ret.Content.ReadAsStringAsync();
                Assert.Fail($"Failed with status code of {ret.StatusCode} and body of {failureMessage}");
            }
            Assert.That(ret.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var stream = await ret.Content.ReadAsStreamAsync();
            return await JsonSerializer.DeserializeAsync<Guid>(stream);
        }

        public async Task<HttpStatusCode> PostAssetWithStatusCode(AssetPost asset)
        {
            var ret = await HttpClient.PostAsJsonAsync("assets", asset, Json.SerializerOptions);
            return ret.StatusCode;
        }

        public async Task DeleteAsset(Guid id)
        {
            var ret = await HttpClient.DeleteAsync($"assets/{id}");
            Assert.That(ret.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        public async Task<HttpStatusCode> DeleteAssetWithStatusCode(Guid id)
        {
            var ret = await HttpClient.DeleteAsync($"assets/{id}");
            return ret.StatusCode;
        }

        public async Task<Asset[]> GetAssets()
        {
            var ret = await HttpClient.GetFromJsonAsync<Asset[]>($"assets", Json.SerializerOptions);
            Assert.That(ret, Is.Not.Null);
            return ret!;
        }

        public async Task<Asset> GetAsset(Guid id)
        {
            var ret = await HttpClient.GetFromJsonAsync<Asset>($"assets/{id}", Json.SerializerOptions);
            Assert.That(ret, Is.Not.Null);
            return ret!;
        }

        public async Task<Payload> GetPayload(Guid id)
        {
            var ret = await HttpClient.GetFromJsonAsync<Payload>($"payloads/{id}", Json.SerializerOptions);
            Assert.That(ret, Is.Not.Null);
            return ret!;
        }

        public async Task<Stream> GetFileBlob(Guid id)
        {
            var ret = await HttpClient.GetAsync($"fileBlobs/{id}");
            Assert.That(ret, Is.Not.Null);
            return await ret.Content.ReadAsStreamAsync();
        }

        public async Task<string[]> GetErrorDetails(HttpContent response)
        {
            string errorDetailsString = await response.ReadAsStringAsync();
            var ret = JsonSerializer.Deserialize<string[]>(errorDetailsString);
            Assert.That(ret, Is.Not.Null);
            return ret!;
        }

        public async Task PutLaunchComplex(LaunchComplex complex)
        {
            var ret = await HttpClient.PutAsJsonAsync("complexes", complex, Json.SerializerOptions);
            Assert.That(ret.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        public async Task<HttpStatusCode> PutLaunchComplexWithStatusCode(LaunchComplex complex)
        {
            var ret = await HttpClient.PutAsJsonAsync("complexes", complex, Json.SerializerOptions);
            return ret.StatusCode;
        }

        public async Task<LaunchComplex[]> GetComplexes()
        {
            var ret = await HttpClient.GetFromJsonAsync<LaunchComplex[]>($"complexes", Json.SerializerOptions);
            Assert.That(ret, Is.Not.Null);
            return ret!;
        }

        public async Task<LaunchComplex> GetComplex(Guid id)
        {
            var ret = await HttpClient.GetFromJsonAsync<LaunchComplex>($"complexes/{id}", Json.SerializerOptions);
            Assert.That(ret, Is.Not.Null);
            return ret!;
        }

        public async Task DeleteComplex(Guid id)
        {
            var ret = await HttpClient.DeleteAsync($"complexes/{id}");
            Assert.That(ret.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        public async Task<HttpStatusCode> DeleteComplexWithStatusCode(Guid id)
        {
            var ret = await HttpClient.DeleteAsync($"complexes/{id}");
            return ret.StatusCode;
        }

        public async Task<LaunchPadStatus> GetLaunchPadStatus(Guid id)
        {
            var ret = await HttpClient.GetFromJsonAsync<LaunchPadStatus>($"launchPadsStatus/{id}", Json.SerializerOptions);
            Assert.That(ret, Is.Not.Null);
            return ret!;
        }

        public async Task<HttpStatusCode> GetLaunchPadStatusWithStatusCode(Guid id)
        {
            var ret = await HttpClient.GetAsync($"launchPadsStatus/{id}");
            return ret.StatusCode;
        }

        public async Task<LaunchPadStatus[]> GetLaunchPadsStatus()
        {
            var ret = await HttpClient.GetFromJsonAsync<LaunchPadStatus[]>($"launchPadsStatus", Json.SerializerOptions);
            Assert.That(ret, Is.Not.Null);
            return ret!;
        }

        public async Task<LaunchPadHealth> GetLaunchPadHealth(Guid id)
        {
            var ret = await HttpClient.GetFromJsonAsync<LaunchPadHealth>($"launchPadsHealth/{id}", Json.SerializerOptions);
            Assert.That(ret, Is.Not.Null);
            return ret!;
        }

        public async Task<HttpStatusCode> GetLaunchPadHealthWithStatusCode(Guid id)
        {
            var ret = await HttpClient.GetAsync($"launchPadsHealth/{id}");
            return ret.StatusCode;
        }

        public async Task<LaunchPadHealth[]> GetLaunchPadsHealth()
        {
            var ret = await HttpClient.GetFromJsonAsync<LaunchPadHealth[]>($"launchPadsHealth", Json.SerializerOptions);
            Assert.That(ret, Is.Not.Null);
            return ret!;
        }

        public Task PutLaunchConfiguration(LaunchConfiguration config)
        {
            return HttpClient.PutAsJsonAsync("currentMission/launchConfiguration", config, Json.SerializerOptions);
        }

        public async Task<HttpStatusCode> PutLaunchConfigurationWithStatusCode(LaunchConfiguration config)
        {
            var ret = await HttpClient.PutAsJsonAsync("currentMission/launchConfiguration", config, Json.SerializerOptions);
            return ret.StatusCode;
        }

        public async Task<LaunchConfiguration> GetLaunchConfiguration()
        {
            var ret = await HttpClient.GetFromJsonAsync<LaunchConfiguration>($"currentMission/launchConfiguration",
                Json.SerializerOptions);
            Assert.That(ret, Is.Not.Null);
            return ret!;
        }

        public async Task PostCommand(MissionCommand command, HttpStatusCode expectedRet = HttpStatusCode.OK)
        {
            var ret = await HttpClient.PostAsJsonAsync("currentMission/commands", command, Json.SerializerOptions);
            Assert.That(ret.StatusCode, Is.EqualTo(expectedRet));
        }

        public async Task<HttpStatusCode> PostCommandWithStatusCode(MissionCommand command)
        {
            var ret = await HttpClient.PostAsJsonAsync("currentMission/commands", command, Json.SerializerOptions);
            return ret.StatusCode;
        }

        public async Task<SavedMissionSummary> GetSavedMission(Guid id)
        {
            var ret = await HttpClient.GetFromJsonAsync<SavedMissionSummary>($"missions/{id}", Json.SerializerOptions);
            Assert.That(ret, Is.Not.Null);
            return ret!;
        }

        public async Task<HttpStatusCode> GetSavedMissionWithStatusCode(Guid id)
        {
            var ret = await HttpClient.GetAsync($"missions/{id}");
            return ret.StatusCode;
        }

        public async Task<SavedMissionSummary[]> GetSavedMissions()
        {
            var ret = await HttpClient.GetFromJsonAsync<SavedMissionSummary[]>($"missions", Json.SerializerOptions);
            Assert.That(ret, Is.Not.Null);
            return ret!;
        }

        public async Task DeleteSavedMission(Guid id)
        {
            var ret = await HttpClient.DeleteAsync($"missions/{id}");
            Assert.That(ret.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        public async Task<HttpStatusCode> DeleteSavedMissionWithStatusCode(Guid id)
        {
            var ret = await HttpClient.DeleteAsync($"missions/{id}");
            return ret.StatusCode;
        }

        /// <summary>
        /// Force the state of MissionControl the specified value until the returned <see cref="IDisposable"/> is
        /// disposed of.
        /// </summary>
        /// <param name="forcedState">The forced state.</param>
        /// <param name="keepLocked">Do we keep the status object locked until the returned <see cref="IDisposable"/>
        /// is disposed of?</param>
        /// <returns><see cref="IDisposable"/> to dispose of to unlock the locked resource.</returns>
        public IDisposable ForceState(State forcedState, bool keepLocked = false)
        {
            ForceStateCommand forceCommand = new()
            {
                State = forcedState,
                KeepLocked = keepLocked,
                ControlFile = Path.Combine(m_ConfigPath!, Guid.NewGuid().ToString())
            };
            File.WriteAllText(forceCommand.ControlFile, "Something");
            PostCommand(forceCommand).Wait();
            return new LockResourceUnLocker(forceCommand.ControlFile);
        }

        public void Dispose()
        {
            if (m_Process != null)
            {
                Stop();
            }
        }

        /// <summary>
        /// <see cref="IDisposable"/> returned by <see cref="ForceState"/>.
        /// </summary>
        class LockResourceUnLocker: IDisposable
        {
            public LockResourceUnLocker(string toDelete)
            {
                m_ToDelete = toDelete;
            }

            public void Dispose()
            {
                string? toDelete = Interlocked.Exchange(ref m_ToDelete, null);
                if (toDelete != null)
                {
                    File.Delete(toDelete);
                }
            }

            string? m_ToDelete;

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
            Assert.That(status.State, Is.EqualTo(state));
        }

        static readonly TimeSpan k_ConnectionTimeout = TimeSpan.FromSeconds(30);

        string? m_ConfigPath;
        Process? m_Process;
        HttpClient? m_HttpClient;
    }
}
