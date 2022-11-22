using System;
using System.Net;
using System.Text.Json;

namespace Unity.ClusterDisplay.MissionControl.MissionControl.Tests
{
    public class LaunchPadsStatusControllerTests
    {
        [SetUp]
        public void Setup()
        {
            m_LaunchPadsStatus = new();
            m_LaunchPadsStatusCts = new();
        }

        [TearDown]
        public void TearDown()
        {
            m_LaunchPadsStatusCts?.Cancel();

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

            var a1StatusDefinedTask = GetLaunchPadStatus(k_LaunchPadA1Id, s => s is {IsDefined: true});
            var a2StatusDefinedTask = GetLaunchPadStatus(k_LaunchPadA2Id, s => s is {IsDefined: true});
            var b1StatusDefinedTask = GetLaunchPadStatus(k_LaunchPadB1Id, s => s is {IsDefined: true});

            await m_ProcessHelper.PutLaunchComplex(k_ComplexA);

            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            var finishedTask = await Task.WhenAny(a1StatusDefinedTask, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(a1StatusDefinedTask)); // Otherwise we timed out
            finishedTask = await Task.WhenAny(a2StatusDefinedTask, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(a2StatusDefinedTask)); // Otherwise we timed out

            Assert.That(a1StatusDefinedTask.Result!.IsDefined, Is.True);
            Assert.That(a2StatusDefinedTask.Result!.IsDefined, Is.True);
            Assert.That(b1StatusDefinedTask.IsCompleted, Is.False);

            await m_ProcessHelper.PutLaunchComplex(k_ComplexB);

            timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            finishedTask = await Task.WhenAny(b1StatusDefinedTask, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(b1StatusDefinedTask)); // Otherwise we timed out
            Assert.That(b1StatusDefinedTask.Result!.IsDefined, Is.True);
        }

        [Test]
        public async Task Get()
        {
            await StartProcessHelper();

            CreateLaunchPadStub(k_LaunchPadA1Port);
            CreateLaunchPadStub(k_LaunchPadA2Port);
            CreateLaunchPadStub(k_LaunchPadB1Port);

            var a1StatusDefinedTask = GetLaunchPadStatus(k_LaunchPadA1Id, s => s is {IsDefined: true});
            var a2StatusDefinedTask = GetLaunchPadStatus(k_LaunchPadA2Id, s => s is {IsDefined: true});
            var b1StatusDefinedTask = GetLaunchPadStatus(k_LaunchPadB1Id, s => s is {IsDefined: true});

            await m_ProcessHelper.PutLaunchComplex(k_ComplexA);
            await m_ProcessHelper.PutLaunchComplex(k_ComplexB);

            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            var finishedTask = await Task.WhenAny(a1StatusDefinedTask, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(a1StatusDefinedTask)); // Otherwise we timed out
            finishedTask = await Task.WhenAny(a2StatusDefinedTask, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(a2StatusDefinedTask)); // Otherwise we timed out
            finishedTask = await Task.WhenAny(b1StatusDefinedTask, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(b1StatusDefinedTask)); // Otherwise we timed out

            Assert.That(a1StatusDefinedTask.Result!.IsDefined, Is.True);
            Assert.That(a2StatusDefinedTask.Result!.IsDefined, Is.True);
            Assert.That(b1StatusDefinedTask.Result!.IsDefined, Is.True);

            Assert.That(await m_ProcessHelper.GetLaunchPadStatus(k_LaunchPadA1Id),
                Is.EqualTo(a1StatusDefinedTask.Result!));
            Assert.That(await m_ProcessHelper.GetLaunchPadStatus(k_LaunchPadA2Id),
                Is.EqualTo(a2StatusDefinedTask.Result!));
            Assert.That(await m_ProcessHelper.GetLaunchPadStatus(k_LaunchPadB1Id),
                Is.EqualTo(b1StatusDefinedTask.Result!));
            Assert.That(await m_ProcessHelper.GetLaunchPadStatusWithStatusCode(Guid.NewGuid()),
                Is.EqualTo(HttpStatusCode.NotFound));

            HashSet<Guid> launchPadIds = new();
            var launchPadsStatus = await m_ProcessHelper.GetLaunchPadsStatus();
            lock (m_Lock)
            {
                Assert.That(launchPadsStatus.Length, Is.EqualTo(m_LaunchPadsStatus!.Count));
                foreach (var status in launchPadsStatus)
                {
                    Assert.That(launchPadIds.Add(status.Id), Is.True);
                    Assert.That(m_LaunchPadsStatus.ContainsKey(status.Id), Is.True);
                    var statusFromCollection = m_LaunchPadsStatus[status.Id];
                    Assert.That(status, Is.EqualTo(statusFromCollection));
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

            var a1StatusDefinedTask = GetLaunchPadStatus(k_LaunchPadA1Id, s => s is {IsDefined: true});
            var a2StatusDefinedTask = GetLaunchPadStatus(k_LaunchPadA2Id, s => s is {IsDefined: true});
            var b1StatusDefinedTask = GetLaunchPadStatus(k_LaunchPadB1Id, s => s is {IsDefined: true});

            await m_ProcessHelper.PutLaunchComplex(k_ComplexA);
            await m_ProcessHelper.PutLaunchComplex(k_ComplexB);

            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            var finishedTask = await Task.WhenAny(a1StatusDefinedTask, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(a1StatusDefinedTask)); // Otherwise we timed out
            finishedTask = await Task.WhenAny(a2StatusDefinedTask, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(a2StatusDefinedTask)); // Otherwise we timed out
            finishedTask = await Task.WhenAny(b1StatusDefinedTask, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(b1StatusDefinedTask)); // Otherwise we timed out

            var a1StatusDeletedTask = GetLaunchPadStatus(k_LaunchPadA1Id, s => s == null);
            var a2StatusDeletedTask = GetLaunchPadStatus(k_LaunchPadA2Id, s => s == null);
            var b1StatusDeletedTask = GetLaunchPadStatus(k_LaunchPadB1Id, s => s == null);

            // Delete the launch complex (at the same time removing the launchpad)
            await m_ProcessHelper.DeleteComplex(k_ComplexB.Id);

            timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            finishedTask = await Task.WhenAny(b1StatusDeletedTask, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(b1StatusDeletedTask)); // Otherwise we timed out

            Assert.That(a1StatusDeletedTask.IsCompleted, Is.False);
            Assert.That(a2StatusDeletedTask.IsCompleted, Is.False);
            Assert.That(b1StatusDeletedTask.Result, Is.Null);

            // Remove the other launch complex
            await m_ProcessHelper.DeleteComplex(k_ComplexA.Id);

            timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            finishedTask = await Task.WhenAny(b1StatusDeletedTask, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(b1StatusDeletedTask)); // Otherwise we timed out

            Assert.That(a1StatusDeletedTask.Result, Is.Null);
            Assert.That(a2StatusDeletedTask.Result, Is.Null);
        }

        [Test]
        public async Task ChangeLaunchPadEndPoint()
        {
            await StartProcessHelper();

            var launchPadB1 = CreateLaunchPadStub(k_LaunchPadB1Port);
            var launchPadB2 = CreateLaunchPadStub(k_LaunchPadB1Port + 1);

            ClusterDisplay.MissionControl.LaunchPad.Status b1Status = new()
            {
                StartTime = DateTime.Now
            };
            launchPadB1.SetStatus(b1Status);
            ClusterDisplay.MissionControl.LaunchPad.Status b2Status = new()
            {
                StartTime = DateTime.Now + TimeSpan.FromHours(1)
            };
            launchPadB2.SetStatus(b2Status);

            var b1StatusTask = GetLaunchPadStatus(k_LaunchPadB1Id, s => s != null && s.StartTime == b1Status.StartTime);
            var b2StatusTask = GetLaunchPadStatus(k_LaunchPadB1Id, s => s != null && s.StartTime == b2Status.StartTime);

            await m_ProcessHelper.PutLaunchComplex(k_ComplexB);

            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            var finishedTask = await Task.WhenAny(b1StatusTask, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(b1StatusTask)); // Otherwise we timed out
            Assert.That(b2StatusTask.IsCompleted, Is.False);

            // Change the LaunchPad endpoint
            var complexWithNewLaunchpadEndpoint = k_ComplexB.DeepClone();
            complexWithNewLaunchpadEndpoint.LaunchPads.First().Endpoint =
                new Uri($"http://127.0.0.1:{k_LaunchPadB1Port + 1}");
            await m_ProcessHelper.PutLaunchComplex(complexWithNewLaunchpadEndpoint);

            timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            finishedTask = await Task.WhenAny(b2StatusTask, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(b2StatusTask)); // Otherwise we timed out
        }

        [Test]
        public async Task ChangeStatus()
        {
            await StartProcessHelper();

            var launchPadB1 = CreateLaunchPadStub(k_LaunchPadB1Port);

            ClusterDisplay.MissionControl.LaunchPad.Status b1Status = new()
            {
                Version = "1.0.0.0",
                StartTime = DateTime.Now,
                LastChanged = DateTime.Now
            };
            launchPadB1.SetStatus(b1Status);

            var idleStateTask = GetLaunchPadStatus(k_LaunchPadB1Id,
                s => s != null && s.StartTime == b1Status.StartTime &&
                    s.State == ClusterDisplay.MissionControl.LaunchPad.State.Idle);
            var launchedStateTask = GetLaunchPadStatus(k_LaunchPadB1Id,
                s => s != null && s.StartTime == b1Status.StartTime &&
                    s.State == ClusterDisplay.MissionControl.LaunchPad.State.Launched);

            await m_ProcessHelper.PutLaunchComplex(k_ComplexB);

            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            var finishedTask = await Task.WhenAny(idleStateTask, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(idleStateTask)); // Otherwise we timed out
            Assert.That(launchedStateTask.IsCompleted, Is.False);

            // Change the status
            b1Status.State = ClusterDisplay.MissionControl.LaunchPad.State.Launched;
            launchPadB1.SetStatus(b1Status);

            timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            finishedTask = await Task.WhenAny(launchedStateTask, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(launchedStateTask)); // Otherwise we timed out
        }

        [Test]
        public async Task UpdateError()
        {
            await StartProcessHelper();

            CreateLaunchPadStub(k_LaunchPadA1Port);
            CreateLaunchPadStub(k_LaunchPadA2Port);
            CreateLaunchPadStub(k_LaunchPadB1Port);

            var a1StatusDefinedTask = GetLaunchPadStatus(k_LaunchPadA1Id, s => s is {IsDefined: true});
            var a2StatusDefinedTask = GetLaunchPadStatus(k_LaunchPadA2Id, s => s is {IsDefined: true});
            var b1StatusDefinedTask = GetLaunchPadStatus(k_LaunchPadB1Id, s => s is {IsDefined: true});

            await m_ProcessHelper.PutLaunchComplex(k_ComplexA);
            await m_ProcessHelper.PutLaunchComplex(k_ComplexB);

            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            var finishedTask = await Task.WhenAny(a1StatusDefinedTask, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(a1StatusDefinedTask)); // Otherwise we timed out
            finishedTask = await Task.WhenAny(a2StatusDefinedTask, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(a2StatusDefinedTask)); // Otherwise we timed out
            finishedTask = await Task.WhenAny(b1StatusDefinedTask, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(b1StatusDefinedTask)); // Otherwise we timed out

            Assert.That(a1StatusDefinedTask.Result!.IsDefined, Is.True);
            Assert.That(a2StatusDefinedTask.Result!.IsDefined, Is.True);
            Assert.That(b1StatusDefinedTask.Result!.IsDefined, Is.True);

            var b1StatusInErrorTask = GetLaunchPadStatus(k_LaunchPadB1Id, s => s is {IsDefined: false});

            m_LaunchPadStubs.Last().Stop();
            m_LaunchPadStubs.RemoveAt(m_LaunchPadStubs.Count - 1);

            timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            finishedTask = await Task.WhenAny(b1StatusInErrorTask, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(b1StatusInErrorTask)); // Otherwise we timed out

            Assert.That(b1StatusInErrorTask.Result!.IsDefined, Is.False);
            Assert.That(b1StatusInErrorTask.Result!.UpdateError, Is.Not.Empty);
        }

        [Test]
        public async Task StartStop()
        {
            // Start the MissionControl process without updating out local copies of the launchpads status (we could,
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

            // It should reload the added launch complexes and fetch the status from LaunchPads without us having to do
            // anything.
            var a1StatusDefinedTask = GetLaunchPadStatus(k_LaunchPadA1Id, s => s is {IsDefined: true});
            var a2StatusDefinedTask = GetLaunchPadStatus(k_LaunchPadA2Id, s => s is {IsDefined: true});
            var b1StatusDefinedTask = GetLaunchPadStatus(k_LaunchPadB1Id, s => s is {IsDefined: true});

            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            var finishedTask = await Task.WhenAny(a1StatusDefinedTask, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(a1StatusDefinedTask)); // Otherwise we timed out
            finishedTask = await Task.WhenAny(a2StatusDefinedTask, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(a2StatusDefinedTask)); // Otherwise we timed out
            finishedTask = await Task.WhenAny(b1StatusDefinedTask, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(b1StatusDefinedTask)); // Otherwise we timed out

            Assert.That(a1StatusDefinedTask.Result!.IsDefined, Is.True);
            Assert.That(a2StatusDefinedTask.Result!.IsDefined, Is.True);
            Assert.That(b1StatusDefinedTask.Result!.IsDefined, Is.True);
        }

        async Task<string> StartProcessHelper(bool startStatusUpdateLoop = true, string folder = "")
        {
            if (string.IsNullOrEmpty(folder))
            {
                folder = GetTestTempFolder();
            }
            await m_ProcessHelper.Start(folder);
            if (startStatusUpdateLoop)
            {
                _ = FetchLaunchPadsStatusLoop();
            }
            return folder;
        }

        async Task FetchLaunchPadsStatusLoop()
        {
            ulong fromVersion = 0;
            while (!m_LaunchPadsStatusCts!.IsCancellationRequested)
            {
                var update = await m_ProcessHelper.GetIncrementalCollectionsUpdate(new List<(string, ulong)> {
                    (k_StatusesCollectionName, fromVersion ) }, m_LaunchPadsStatusCts.Token);
                if (update.ContainsKey(k_StatusesCollectionName))
                {
                    var incrementalUpdate = JsonSerializer.Deserialize<IncrementalCollectionUpdate<LaunchPadStatus>>(
                        update[k_StatusesCollectionName], Json.SerializerOptions);
                    if (incrementalUpdate != null)
                    {
                        lock (m_Lock)
                        {
                            m_LaunchPadsStatus!.ApplyDelta(incrementalUpdate);
                            var toSetResultOf = m_LaunchPadsStatusUpdated;
                            m_LaunchPadsStatusUpdated = null;
                            toSetResultOf?.TrySetResult();
                        }
                        fromVersion = incrementalUpdate.NextUpdate;
                    }
                }
            }
        }

        async Task<LaunchPadStatus?> GetLaunchPadStatus(Guid id, Func<LaunchPadStatus?, bool> eval)
        {
            var deadlineTask = Task.Delay(TimeSpan.FromSeconds(10));
            while (!deadlineTask.IsCompleted)
            {
                LaunchPadStatus? candidate = null;
                Task toWaitOn;
                lock (m_Lock)
                {
                    if (m_LaunchPadsStatus!.TryGetValue(id, out var status))
                    {
                        candidate = status.DeepClone();
                    }
                    m_LaunchPadsStatusUpdated ??= new();
                    toWaitOn = m_LaunchPadsStatusUpdated.Task;
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

        static LaunchPadsStatusControllerTests()
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
            var folderPath = Path.Combine(Path.GetTempPath(), "LaunchPadsStatusControllerTests_" + Guid.NewGuid().ToString());
            m_TestTempFolders.Add(folderPath);
            return folderPath;
        }

        const string k_StatusesCollectionName = "launchPadsStatus";

        readonly MissionControlProcessHelper m_ProcessHelper = new();
        readonly List<string> m_TestTempFolders = new();
        readonly List<LaunchPadStub> m_LaunchPadStubs = new();

        readonly object m_Lock = new();
        IncrementalCollection<LaunchPadStatus>? m_LaunchPadsStatus;
        CancellationTokenSource? m_LaunchPadsStatusCts;
        TaskCompletionSource? m_LaunchPadsStatusUpdated;
    }
}
