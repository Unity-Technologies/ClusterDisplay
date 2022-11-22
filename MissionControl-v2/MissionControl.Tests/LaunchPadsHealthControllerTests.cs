using System;
using System.Net;
using System.Text.Json;

namespace Unity.ClusterDisplay.MissionControl.MissionControl.Tests
{
    public class LaunchPadsHealthControllerTests
    {
        [SetUp]
        public void Setup()
        {
            m_LaunchPadsHealth = new();
            m_LaunchPadsHealthCts = new();
        }

        [TearDown]
        public void TearDown()
        {
            m_LaunchPadsHealthCts?.Cancel();

            m_ProcessHelper.Dispose();

            foreach (string folder in m_TestTempFolders)
            {
                try
                {
                    Directory.Delete(folder, true);
                }
                catch
                {
                    // ignored
                }
            }

            foreach (var stub in m_LaunchPadStubs)
            {
                stub.Stop();
            }
            m_LaunchPadStubs.Clear();
        }

        [Test]
        public async Task AddLaunchPad()
        {
            await StartProcessHelper();

            CreateLaunchPadStub(k_LaunchPadA1Port);
            CreateLaunchPadStub(k_LaunchPadA2Port);
            CreateLaunchPadStub(k_LaunchPadB1Port);

            var a1HealthDefinedTask = GetLaunchPadHealth(k_LaunchPadA1Id, h => h is {IsDefined: true});
            var a2HealthDefinedTask = GetLaunchPadHealth(k_LaunchPadA2Id, h => h is {IsDefined: true});
            var b1HealthDefinedTask = GetLaunchPadHealth(k_LaunchPadB1Id, h => h is {IsDefined: true});

            await m_ProcessHelper.PutLaunchComplex(k_ComplexA);

            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            var finishedTask = await Task.WhenAny(a1HealthDefinedTask, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(a1HealthDefinedTask)); // Otherwise we timed out
            finishedTask = await Task.WhenAny(a2HealthDefinedTask, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(a2HealthDefinedTask)); // Otherwise we timed out

            Assert.That(a1HealthDefinedTask.Result!.IsDefined, Is.True);
            Assert.That(a2HealthDefinedTask.Result!.IsDefined, Is.True);
            Assert.That(b1HealthDefinedTask.IsCompleted, Is.False);

            await m_ProcessHelper.PutLaunchComplex(k_ComplexB);

            timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            finishedTask = await Task.WhenAny(b1HealthDefinedTask, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(b1HealthDefinedTask)); // Otherwise we timed out
            Assert.That(b1HealthDefinedTask.Result!.IsDefined, Is.True);
        }

        [Test]
        public async Task Get()
        {
            await StartProcessHelper();

            CreateLaunchPadStub(k_LaunchPadA1Port);
            CreateLaunchPadStub(k_LaunchPadA2Port);
            CreateLaunchPadStub(k_LaunchPadB1Port);

            var a1HealthDefinedTask = GetLaunchPadHealth(k_LaunchPadA1Id, h => h is {IsDefined: true});
            var a2HealthDefinedTask = GetLaunchPadHealth(k_LaunchPadA2Id, h => h is {IsDefined: true});
            var b1HealthDefinedTask = GetLaunchPadHealth(k_LaunchPadB1Id, h => h is {IsDefined: true});

            await m_ProcessHelper.PutLaunchComplex(k_ComplexA);
            await m_ProcessHelper.PutLaunchComplex(k_ComplexB);

            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            var finishedTask = await Task.WhenAny(a1HealthDefinedTask, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(a1HealthDefinedTask)); // Otherwise we timed out
            finishedTask = await Task.WhenAny(a2HealthDefinedTask, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(a2HealthDefinedTask)); // Otherwise we timed out
            finishedTask = await Task.WhenAny(b1HealthDefinedTask, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(b1HealthDefinedTask)); // Otherwise we timed out

            Assert.That(a1HealthDefinedTask.Result!.IsDefined, Is.True);
            Assert.That(a2HealthDefinedTask.Result!.IsDefined, Is.True);
            Assert.That(b1HealthDefinedTask.Result!.IsDefined, Is.True);

            Assert.That(await m_ProcessHelper.GetLaunchPadHealth(k_LaunchPadA1Id),
                Is.EqualTo(a1HealthDefinedTask.Result!));
            Assert.That(await m_ProcessHelper.GetLaunchPadHealth(k_LaunchPadA2Id),
                Is.EqualTo(a2HealthDefinedTask.Result!));
            Assert.That(await m_ProcessHelper.GetLaunchPadHealth(k_LaunchPadB1Id),
                Is.EqualTo(b1HealthDefinedTask.Result!));
            Assert.That(await m_ProcessHelper.GetLaunchPadHealthWithStatusCode(Guid.NewGuid()),
                Is.EqualTo(HttpStatusCode.NotFound));

            HashSet<Guid> launchPadIds = new();
            var launchPadsHealth = await m_ProcessHelper.GetLaunchPadsHealth();
            lock (m_Lock)
            {
                Assert.That(launchPadsHealth.Length, Is.EqualTo(m_LaunchPadsHealth!.Count));
                foreach (var health in launchPadsHealth)
                {
                    Assert.That(launchPadIds.Add(health.Id), Is.True);
                    Assert.That(m_LaunchPadsHealth.ContainsKey(health.Id), Is.True);
                    var healthFromCollection = m_LaunchPadsHealth[health.Id];
                    Assert.That(health, Is.EqualTo(healthFromCollection));
                }
            }
        }

        [Test]
        public async Task RemoveLaunchPad()
        {
            await StartProcessHelper();

            CreateLaunchPadStub(k_LaunchPadA1Port);
            CreateLaunchPadStub(k_LaunchPadA2Port);
            CreateLaunchPadStub(k_LaunchPadB1Port);

            var a1HealthDefinedTask = GetLaunchPadHealth(k_LaunchPadA1Id, h => h is {IsDefined: true});
            var a2HealthDefinedTask = GetLaunchPadHealth(k_LaunchPadA2Id, h => h is {IsDefined: true});
            var b1HealthDefinedTask = GetLaunchPadHealth(k_LaunchPadB1Id, h => h is {IsDefined: true});

            await m_ProcessHelper.PutLaunchComplex(k_ComplexA);
            await m_ProcessHelper.PutLaunchComplex(k_ComplexB);

            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            var finishedTask = await Task.WhenAny(a1HealthDefinedTask, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(a1HealthDefinedTask)); // Otherwise we timed out
            finishedTask = await Task.WhenAny(a2HealthDefinedTask, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(a2HealthDefinedTask)); // Otherwise we timed out
            finishedTask = await Task.WhenAny(b1HealthDefinedTask, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(b1HealthDefinedTask)); // Otherwise we timed out

            var a1HealthDeletedTask = GetLaunchPadHealth(k_LaunchPadA1Id, h => h == null);
            var a2HealthDeletedTask = GetLaunchPadHealth(k_LaunchPadA2Id, h => h == null);
            var b1HealthDeletedTask = GetLaunchPadHealth(k_LaunchPadB1Id, h => h == null);

            // Delete the launch complex (at the same time removing the launchpad)
            await m_ProcessHelper.DeleteComplex(k_ComplexB.Id);

            timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            finishedTask = await Task.WhenAny(b1HealthDeletedTask, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(b1HealthDeletedTask)); // Otherwise we timed out

            Assert.That(a1HealthDeletedTask.IsCompleted, Is.False);
            Assert.That(a2HealthDeletedTask.IsCompleted, Is.False);
            Assert.That(b1HealthDeletedTask.Result, Is.Null);

            // Remove the other launch complex
            await m_ProcessHelper.DeleteComplex(k_ComplexA.Id);

            timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            finishedTask = await Task.WhenAny(b1HealthDeletedTask, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(b1HealthDeletedTask)); // Otherwise we timed out

            Assert.That(a1HealthDeletedTask.Result, Is.Null);
            Assert.That(a2HealthDeletedTask.Result, Is.Null);
        }

        [Test]
        public async Task ChangeLaunchPadEndPoint()
        {
            await StartProcessHelper();

            var launchPadB1 = CreateLaunchPadStub(k_LaunchPadB1Port);
            var launchPadB2 = CreateLaunchPadStub(k_LaunchPadB1Port + 1);

            ClusterDisplay.MissionControl.LaunchPad.Health b1Health = new()
            {
                MemoryInstalled = 28 * 1024 * 1024
            };
            launchPadB1.SetHealth(b1Health);
            ClusterDisplay.MissionControl.LaunchPad.Health b2Health = new()
            {
                MemoryInstalled = 42 * 1024 * 1024
            };
            launchPadB2.SetHealth(b2Health);

            var b1HealthTask = GetLaunchPadHealth(k_LaunchPadB1Id,
                h => h != null && h.MemoryInstalled == b1Health.MemoryInstalled);
            var b2HealthTask = GetLaunchPadHealth(k_LaunchPadB1Id,
                h => h != null && h.MemoryInstalled == b2Health.MemoryInstalled);

            await m_ProcessHelper.PutLaunchComplex(k_ComplexB);

            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            var finishedTask = await Task.WhenAny(b1HealthTask, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(b1HealthTask)); // Otherwise we timed out
            Assert.That(b2HealthTask.IsCompleted, Is.False);

            // Change the LaunchPad endpoint
            var complexWithNewLaunchpadEndpoint = k_ComplexB.DeepClone();
            complexWithNewLaunchpadEndpoint.LaunchPads.First().Endpoint =
                new Uri($"http://127.0.0.1:{k_LaunchPadB1Port + 1}");
            await m_ProcessHelper.PutLaunchComplex(complexWithNewLaunchpadEndpoint);

            timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            finishedTask = await Task.WhenAny(b2HealthTask, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(b2HealthTask)); // Otherwise we timed out
        }

        [Test]
        public async Task ChangeHealth()
        {
            await StartProcessHelper();

            var launchPadB1 = CreateLaunchPadStub(k_LaunchPadB1Port);

            ClusterDisplay.MissionControl.LaunchPad.Health b1Health = new()
            {
                CpuUtilization = 0.28f,
                MemoryUsage = 28 * 1024 * 1024,
                MemoryInstalled = 42 * 1024 * 1024
            };
            launchPadB1.SetHealth(b1Health);

            var twentyHeightPercentTask = GetLaunchPadHealth(k_LaunchPadB1Id, h => h is {CpuUtilization: 0.28f});
            var fortyTwoPercentTask = GetLaunchPadHealth(k_LaunchPadB1Id, h => h is {CpuUtilization: 0.42f});

            DateTime minTime = DateTime.Now;
            await m_ProcessHelper.PutLaunchComplex(k_ComplexB);

            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            var finishedTask = await Task.WhenAny(twentyHeightPercentTask, timeoutTask);
            DateTime maxTime = DateTime.Now;
            Assert.That(finishedTask, Is.SameAs(twentyHeightPercentTask)); // Otherwise we timed out
            Assert.That(fortyTwoPercentTask.IsCompleted, Is.False);
            Assert.That(twentyHeightPercentTask.Result!.UpdateTime, Is.InRange(minTime, maxTime));

            // Change the health
            minTime = DateTime.Now;
            b1Health.CpuUtilization = 0.42f;
            launchPadB1.SetHealth(b1Health);

            timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            finishedTask = await Task.WhenAny(fortyTwoPercentTask, timeoutTask);
            maxTime = DateTime.Now;
            Assert.That(finishedTask, Is.SameAs(fortyTwoPercentTask)); // Otherwise we timed out
            Assert.That(fortyTwoPercentTask.Result!.UpdateTime, Is.InRange(minTime, maxTime));
        }

        [Test]
        public async Task UpdateError()
        {
            await StartProcessHelper();

            CreateLaunchPadStub(k_LaunchPadA1Port);
            CreateLaunchPadStub(k_LaunchPadA2Port);
            CreateLaunchPadStub(k_LaunchPadB1Port);

            var a1HealthDefinedTask = GetLaunchPadHealth(k_LaunchPadA1Id, h => h is {IsDefined: true});
            var a2HealthDefinedTask = GetLaunchPadHealth(k_LaunchPadA2Id, h => h is {IsDefined: true});
            var b1HealthDefinedTask = GetLaunchPadHealth(k_LaunchPadB1Id, h => h is {IsDefined: true});

            await m_ProcessHelper.PutLaunchComplex(k_ComplexA);
            await m_ProcessHelper.PutLaunchComplex(k_ComplexB);

            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            var finishedTask = await Task.WhenAny(a1HealthDefinedTask, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(a1HealthDefinedTask)); // Otherwise we timed out
            finishedTask = await Task.WhenAny(a2HealthDefinedTask, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(a2HealthDefinedTask)); // Otherwise we timed out
            finishedTask = await Task.WhenAny(b1HealthDefinedTask, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(b1HealthDefinedTask)); // Otherwise we timed out

            Assert.That(a1HealthDefinedTask.Result!.IsDefined, Is.True);
            Assert.That(a2HealthDefinedTask.Result!.IsDefined, Is.True);
            Assert.That(b1HealthDefinedTask.Result!.IsDefined, Is.True);

            var b1HealthInErrorTask = GetLaunchPadHealth(k_LaunchPadB1Id, h => h is {IsDefined: false});

            m_LaunchPadStubs.Last().Stop();
            m_LaunchPadStubs.RemoveAt(m_LaunchPadStubs.Count - 1);

            timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            finishedTask = await Task.WhenAny(b1HealthInErrorTask, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(b1HealthInErrorTask)); // Otherwise we timed out

            Assert.That(b1HealthInErrorTask.Result!.IsDefined, Is.False);
            Assert.That(b1HealthInErrorTask.Result!.UpdateError, Is.Not.Empty);
        }

        [Test]
        public async Task StartStop()
        {
            // Start the MissionControl process without updating out local copies of the LaunchPads health (we could,
            // but we would also need to clean it up between restart)
            string testFolder = await StartProcessHelper(false);

            // Add some launch complexes
            await m_ProcessHelper.PutLaunchComplex(k_ComplexA);
            await m_ProcessHelper.PutLaunchComplex(k_ComplexB);

            // Stop the MissionControl process
            m_ProcessHelper.Stop();

            // Start fake LaunchPads
            CreateLaunchPadStub(k_LaunchPadA1Port);
            CreateLaunchPadStub(k_LaunchPadA2Port);
            CreateLaunchPadStub(k_LaunchPadB1Port);

            // Restart mission control
            await StartProcessHelper(true, testFolder);

            // It should reload the added launch complexes and fetch the health from LaunchPads without us having to do
            // anything.
            var a1HealthDefinedTask = GetLaunchPadHealth(k_LaunchPadA1Id, h => h is {IsDefined: true});
            var a2HealthDefinedTask = GetLaunchPadHealth(k_LaunchPadA2Id, h => h is {IsDefined: true});
            var b1HealthDefinedTask = GetLaunchPadHealth(k_LaunchPadB1Id, h => h is {IsDefined: true});

            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            var finishedTask = await Task.WhenAny(a1HealthDefinedTask, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(a1HealthDefinedTask)); // Otherwise we timed out
            finishedTask = await Task.WhenAny(a2HealthDefinedTask, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(a2HealthDefinedTask)); // Otherwise we timed out
            finishedTask = await Task.WhenAny(b1HealthDefinedTask, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(b1HealthDefinedTask)); // Otherwise we timed out

            Assert.That(a1HealthDefinedTask.Result!.IsDefined, Is.True);
            Assert.That(a2HealthDefinedTask.Result!.IsDefined, Is.True);
            Assert.That(b1HealthDefinedTask.Result!.IsDefined, Is.True);
        }

        [Test]
        public async Task InvalidUpdateInterval()
        {
            await StartProcessHelper();

            var currentConfig = await m_ProcessHelper.GetConfig();
            currentConfig.HealthMonitoringIntervalSec = 0;
            var ret = await m_ProcessHelper.PutConfigWithResponse(currentConfig);
            Assert.That(ret.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

            currentConfig.HealthMonitoringIntervalSec = -1;
            ret = await m_ProcessHelper.PutConfigWithResponse(currentConfig);
            Assert.That(ret.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

            currentConfig.HealthMonitoringIntervalSec = 1;
            ret = await m_ProcessHelper.PutConfigWithResponse(currentConfig);
            Assert.That(ret.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        async Task<string> StartProcessHelper(bool startHealthUpdateLoop = true, string folder = "")
        {
            if (string.IsNullOrEmpty(folder))
            {
                folder = GetTestTempFolder();
            }
            await m_ProcessHelper.Start(folder);
            if (startHealthUpdateLoop)
            {
                _ = FetchLaunchPadsHealthLoop();
            }

            var currentConfig = await m_ProcessHelper.GetConfig();
            currentConfig.HealthMonitoringIntervalSec = 0.1f;
            await m_ProcessHelper.PutConfig(currentConfig);

            return folder;
        }

        async Task FetchLaunchPadsHealthLoop()
        {
            ulong fromVersion = 0;
            while (!m_LaunchPadsHealthCts!.IsCancellationRequested)
            {
                var update = await m_ProcessHelper.GetIncrementalCollectionsUpdate(new List<(string, ulong)> {
                    (k_HealthCollectionName, fromVersion ) }, m_LaunchPadsHealthCts.Token);
                if (update.ContainsKey(k_HealthCollectionName))
                {
                    var incrementalUpdate = JsonSerializer.Deserialize<IncrementalCollectionUpdate<LaunchPadHealth>>(
                        update[k_HealthCollectionName], Json.SerializerOptions);
                    if (incrementalUpdate != null)
                    {
                        lock (m_Lock)
                        {
                            m_LaunchPadsHealth!.ApplyDelta(incrementalUpdate);
                            var toSetResultOf = m_LaunchPadsHealthUpdated;
                            m_LaunchPadsHealthUpdated = null;
                            toSetResultOf?.TrySetResult();
                        }
                        fromVersion = incrementalUpdate.NextUpdate;
                    }
                }
            }
        }

        async Task<LaunchPadHealth?> GetLaunchPadHealth(Guid id, Func<LaunchPadHealth?, bool> eval)
        {
            var deadlineTask = Task.Delay(TimeSpan.FromSeconds(10));
            while (!deadlineTask.IsCompleted)
            {
                LaunchPadHealth? candidate = null;
                Task toWaitOn;
                lock (m_Lock)
                {
                    if (m_LaunchPadsHealth!.TryGetValue(id, out var health))
                    {
                        candidate = health.DeepClone();
                    }
                    m_LaunchPadsHealthUpdated ??= new();
                    toWaitOn = m_LaunchPadsHealthUpdated.Task;
                }

                if (eval(candidate))
                {
                    return candidate;
                }

                await Task.WhenAny(toWaitOn, deadlineTask);
            }

            return null;
        }

        static readonly Guid k_LaunchPadA1Id = Guid.NewGuid();
        static readonly Guid k_LaunchPadA2Id = Guid.NewGuid();
        static readonly Guid k_LaunchPadB1Id = Guid.NewGuid();
        const int k_LaunchPadA1Port = 8201;
        const int k_LaunchPadA2Port = 8202;
        const int k_LaunchPadB1Port = 8203;
        static readonly LaunchComplex k_ComplexA;
        static readonly LaunchComplex k_ComplexB;

        static LaunchPadsHealthControllerTests()
        {
            Guid complexAId = Guid.NewGuid();
            k_ComplexA = new(complexAId)
            {
                Name = "Complex A",
                HangarBay = new()
                {
                    Identifier = complexAId,
                    Endpoint = new("http://127.0.0.1:8100")
                },
                LaunchPads = new[] {
                    new LaunchPad()
                    {
                        Identifier = k_LaunchPadA1Id,
                        Name = "A1",
                        Endpoint = new($"http://127.0.0.1:{k_LaunchPadA1Port}"),
                        SuitableFor = new []{ "clusterNode" }
                    },
                    new LaunchPad()
                    {
                        Identifier = k_LaunchPadA2Id,
                        Name = "A2",
                        Endpoint = new($"http://127.0.0.1:{k_LaunchPadA2Port}"),
                        SuitableFor = new []{ "clusterNode" }
                    }
                }
            };

            Guid complexBId = Guid.NewGuid();
            k_ComplexB = new(complexBId)
            {
                Name = "Complex B",
                HangarBay = new()
                {
                    Identifier = complexBId,
                    Endpoint = new("http://127.0.0.1:8101")
                },
                LaunchPads = new[] {
                    new LaunchPad()
                    {
                        Identifier = k_LaunchPadB1Id,
                        Name = "B1",
                        Endpoint = new($"http://127.0.0.1:{k_LaunchPadB1Port}"),
                        SuitableFor = new []{ "clusterNode" }
                    }
                }
            };
        }

        LaunchPadStub CreateLaunchPadStub(int port)
        {
            LaunchPadStub newStub = new(port);
            m_LaunchPadStubs.Add(newStub);
            newStub.Start();
            return newStub;
        }

        string GetTestTempFolder()
        {
            var folderPath = Path.Combine(Path.GetTempPath(), "LaunchPadsHealthControllerTests_" + Guid.NewGuid().ToString());
            m_TestTempFolders.Add(folderPath);
            return folderPath;
        }

        const string k_HealthCollectionName = "launchPadsHealth";

        readonly MissionControlProcessHelper m_ProcessHelper = new();
        readonly List<string> m_TestTempFolders = new();
        readonly List<LaunchPadStub> m_LaunchPadStubs = new();

        readonly object m_Lock = new();
        IncrementalCollection<LaunchPadHealth>? m_LaunchPadsHealth;
        CancellationTokenSource? m_LaunchPadsHealthCts;
        TaskCompletionSource? m_LaunchPadsHealthUpdated;
    }
}
