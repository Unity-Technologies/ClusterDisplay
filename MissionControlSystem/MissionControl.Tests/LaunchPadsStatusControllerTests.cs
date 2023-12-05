using System;
using System.Net;
using System.Text.Json;

using static Unity.ClusterDisplay.MissionControl.MissionControl.Helpers;
using LaunchPadCommand = Unity.ClusterDisplay.MissionControl.LaunchPad.Command;
using StatusFromLaunchpad = Unity.ClusterDisplay.MissionControl.LaunchPad.Status;
using LaunchPadState = Unity.ClusterDisplay.MissionControl.LaunchPad.State;
using LaunchPadCommandType = Unity.ClusterDisplay.MissionControl.LaunchPad.CommandType;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
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

            StatusFromLaunchpad b1Status = new()
            {
                StartTime = DateTime.Now
            };
            launchPadB1.Status = b1Status;
            StatusFromLaunchpad b2Status = new()
            {
                StartTime = DateTime.Now + TimeSpan.FromHours(1)
            };
            launchPadB2.Status = b2Status;

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

            StatusFromLaunchpad b1Status = new()
            {
                Version = "1.0.0.0",
                StartTime = DateTime.Now,
                LastChanged = DateTime.Now
            };
            launchPadB1.Status = b1Status;

            var idleStateTask = GetLaunchPadStatus(k_LaunchPadB1Id,
                s => s != null && s.StartTime == b1Status.StartTime && s.State == LaunchPadState.Idle);
            var launchedStateTask = GetLaunchPadStatus(k_LaunchPadB1Id,
                s => s != null && s.StartTime == b1Status.StartTime && s.State == LaunchPadState.Launched);

            await m_ProcessHelper.PutLaunchComplex(k_ComplexB);

            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            var finishedTask = await Task.WhenAny(idleStateTask, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(idleStateTask)); // Otherwise we timed out
            Assert.That(launchedStateTask.IsCompleted, Is.False);

            // Change the status
            b1Status.State = LaunchPadState.Launched;
            launchPadB1.Status = b1Status;

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

            timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            finishedTask = await Task.WhenAny(b1StatusInErrorTask, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(b1StatusInErrorTask)); // Otherwise we timed out

            Assert.That(a1StatusDefinedTask.Result!.IsDefined, Is.True);
            Assert.That(a2StatusDefinedTask.Result!.IsDefined, Is.True);
            Assert.That(b1StatusInErrorTask.Result!.IsDefined, Is.False);
            Assert.That(b1StatusInErrorTask.Result!.UpdateError, Is.Not.Empty);

            // Status should recover if we restart the launchpad
            b1StatusInErrorTask = GetLaunchPadStatus(k_LaunchPadB1Id, s => s is {IsDefined: true});

            m_LaunchPadStubs.Last().Start();

            timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            finishedTask = await Task.WhenAny(b1StatusInErrorTask, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(b1StatusInErrorTask)); // Otherwise we timed out

            Assert.That(b1StatusInErrorTask.Result!.IsDefined, Is.True);
            Assert.That(b1StatusInErrorTask.Result!.UpdateError, Is.Empty);
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

        [Test]
        public async Task PutDynamicEntriesIdle()
        {
            await StartProcessHelper();

            CreateLaunchPadStub(k_LaunchPadA1Port);
            CreateLaunchPadStub(k_LaunchPadA2Port);

            await m_ProcessHelper.PutLaunchComplex(k_ComplexA);

            Assert.That(await m_ProcessHelper.PutLaunchPadStatusDynamicEntryWithStatusCode(k_LaunchPadA1Id,
                new LaunchPadReportDynamicEntry() { Name = "Test", Value = "Value" }),
                Is.EqualTo(HttpStatusCode.Conflict));
        }

        [Test]
        public async Task PutDynamicEntries()
        {
            await StartProcessHelper();

            CreateLaunchPadStub(k_LaunchPadA1Port, k_LaunchPadA1Id);
            CreateLaunchPadStub(k_LaunchPadA2Port, k_LaunchPadA2Id);
            PrepareLaunchPadStubForFakeLaunch(m_LaunchPadStubs[0]);
            PrepareLaunchPadStubForFakeLaunch(m_LaunchPadStubs[1]);

            await m_ProcessHelper.PutLaunchComplex(k_ComplexA);

            string assetUrl = await CreateAsset(GetTestTempFolder(), k_LaunchCatalog, new(), k_LaunchCatalogFilesContent);
            AssetPost assetPost = new()
            {
                Name = "Test asset",
                Url = assetUrl
            };
            var assetId = await m_ProcessHelper.PostAsset(assetPost);

            LaunchConfiguration launchConfiguration = new() {
                AssetId = assetId,
                LaunchComplexes = new[]
                {
                    new LaunchComplexConfiguration()
                    {
                        Identifier = k_ComplexA.Id,
                        LaunchPads = m_LaunchPadStubs.Select(
                            lps => new LaunchPadConfiguration() { Identifier = lps.Id, LaunchableName = "Cluster Node" }).ToList()
                    }
                }
            };
            await m_ProcessHelper.PutLaunchConfiguration(launchConfiguration);

            await m_ProcessHelper.PostCommand(new LaunchMissionCommand(), HttpStatusCode.Accepted);
            await m_ProcessHelper.WaitForState(State.Launched);

            // Validate LaunchPadStatus are correctly filled (without dynamic entries so far)
            var a1LaunchedTask = GetLaunchPadStatus(k_LaunchPadA1Id, s => s is {State: LaunchPadState.Launched});
            var a2LaunchedTask = GetLaunchPadStatus(k_LaunchPadA2Id, s => s is {State: LaunchPadState.Launched});
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            var finishedTask = await Task.WhenAny(Task.WhenAll(a1LaunchedTask, a2LaunchedTask), timeoutTask);
            Assert.That(finishedTask, Is.Not.SameAs(timeoutTask)); // Otherwise we timed out

            Assert.That(a1LaunchedTask.Result!.State, Is.EqualTo(LaunchPadState.Launched));
            Assert.That(a1LaunchedTask.Result.DynamicEntries, Is.Empty);
            Assert.That(a2LaunchedTask.Result!.State, Is.EqualTo(LaunchPadState.Launched));
            Assert.That(a2LaunchedTask.Result.DynamicEntries, Is.Empty);

            // Add new dynamic properties
            await Task.WhenAll(
                m_ProcessHelper.PutLaunchPadStatusDynamicEntry(k_LaunchPadA1Id,
                    new() { Name = "Name1", Value = "Value1" }),
                m_ProcessHelper.PutLaunchPadStatusDynamicEntry(k_LaunchPadA2Id,
                    new() { Name = "Name2", Value = "Value2" })
            );

            var a1DynamicEntryTask =
                GetLaunchPadStatus(k_LaunchPadA1Id, s => s!.DynamicEntries.Any(e => e.Name == "Name1"));
            var a2DynamicEntryTask =
                GetLaunchPadStatus(k_LaunchPadA2Id, s => s!.DynamicEntries.Any(e => e.Name == "Name2"));
            timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            finishedTask = await Task.WhenAny(Task.WhenAll(a1DynamicEntryTask, a2DynamicEntryTask), timeoutTask);
            Assert.That(finishedTask, Is.Not.SameAs(timeoutTask)); // Otherwise we timed out

            Assert.That(a1DynamicEntryTask.Result!.DynamicEntries.Count(), Is.EqualTo(1));
            Assert.That(a1DynamicEntryTask.Result!.DynamicEntries.First().Name, Is.EqualTo("Name1"));
            Assert.That(a1DynamicEntryTask.Result!.DynamicEntries.First().Value, Is.EqualTo("Value1"));
            Assert.That(a2DynamicEntryTask.Result!.DynamicEntries.Count(), Is.EqualTo(1));
            Assert.That(a2DynamicEntryTask.Result!.DynamicEntries.First().Name, Is.EqualTo("Name2"));
            Assert.That(a2DynamicEntryTask.Result!.DynamicEntries.First().Value, Is.EqualTo("Value2"));

            // Update dynamic properties
            await Task.WhenAll(
                m_ProcessHelper.PutLaunchPadStatusDynamicEntry(k_LaunchPadA1Id,
                    new() { Name = "Name1", Value = "Value1A" }),
                m_ProcessHelper.PutLaunchPadStatusDynamicEntry(k_LaunchPadA2Id,
                    new() { Name = "Name2", Value = "Value2A" })
            );

            var a1UpdatedDynamicEntryTask = GetLaunchPadStatus(k_LaunchPadA1Id,
                s => (string)s!.DynamicEntries.First(e => e.Name == "Name1").Value == "Value1A");
            var a2UpdatedDynamicEntryTask = GetLaunchPadStatus(k_LaunchPadA2Id,
                s => (string)s!.DynamicEntries.First(e => e.Name == "Name2").Value == "Value2A");
            timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            finishedTask = await Task.WhenAny(
                Task.WhenAll(a1UpdatedDynamicEntryTask, a2UpdatedDynamicEntryTask), timeoutTask);
            Assert.That(finishedTask, Is.Not.SameAs(timeoutTask)); // Otherwise we timed out

            // Stop everything
            await m_ProcessHelper.PostCommand(new StopMissionCommand(), HttpStatusCode.Accepted);

            // This should clear all the dynamic entries
            var a1ClearedDynamicEntriesTask = GetLaunchPadStatus(k_LaunchPadA1Id, s => s!.DynamicEntries.Any());
            var a2ClearedDynamicEntriesTask = GetLaunchPadStatus(k_LaunchPadA2Id, s => s!.DynamicEntries.Any());
            timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            finishedTask = await Task.WhenAny(
                Task.WhenAll(a1ClearedDynamicEntriesTask, a2ClearedDynamicEntriesTask), timeoutTask);
            Assert.That(finishedTask, Is.Not.SameAs(timeoutTask)); // Otherwise we timed out
        }

        [Test]
        public async Task PutDynamicEntriesWhenFailed()
        {
            await StartProcessHelper();

            CreateLaunchPadStub(k_LaunchPadA1Port, k_LaunchPadA1Id);
            CreateLaunchPadStub(k_LaunchPadA2Port, k_LaunchPadA2Id);
            CreateLaunchPadStub(k_LaunchPadB1Port, k_LaunchPadB1Id);
            PrepareLaunchPadStubForFakeLaunch(m_LaunchPadStubs[0]);
            PrepareLaunchPadStubForFakeLaunch(m_LaunchPadStubs[1]);
            PrepareLaunchPadStubForFakeLaunch(m_LaunchPadStubs[2]);

            await m_ProcessHelper.PutLaunchComplex(k_ComplexA);
            await m_ProcessHelper.PutLaunchComplex(k_ComplexB);

            string assetUrl = await CreateAsset(GetTestTempFolder(), k_LaunchCatalog, new(), k_LaunchCatalogFilesContent);
            AssetPost assetPost = new()
            {
                Name = "Test asset",
                Url = assetUrl
            };
            var assetId = await m_ProcessHelper.PostAsset(assetPost);

            LaunchConfiguration launchConfiguration = new() {
                AssetId = assetId,
                LaunchComplexes = new[]
                {
                    new LaunchComplexConfiguration()
                    {
                        Identifier = k_ComplexA.Id,
                        LaunchPads = m_LaunchPadStubs.Where(lps => lps.Id == k_LaunchPadA1Id || lps.Id ==k_LaunchPadA2Id)
                            .Select(lps => new LaunchPadConfiguration() { Identifier = lps.Id, LaunchableName = "Cluster Node" })
                            .ToList()
                    },
                    new LaunchComplexConfiguration()
                    {
                        Identifier = k_ComplexB.Id,
                        LaunchPads = m_LaunchPadStubs.Where(lps => lps.Id == k_LaunchPadB1Id)
                            .Select(lps => new LaunchPadConfiguration() { Identifier = lps.Id, LaunchableName = "Cluster Node" })
                            .ToList()
                    }
                }
            };
            await m_ProcessHelper.PutLaunchConfiguration(launchConfiguration);

            await m_ProcessHelper.PostCommand(new LaunchMissionCommand(), HttpStatusCode.Accepted);
            await m_ProcessHelper.WaitForState(State.Launched);

            // Stop one of the launchpads so that the global status becomes failure.
            var overStatus = m_LaunchPadStubs[2].Status;
            overStatus.State = LaunchPadState.Over;
            m_LaunchPadStubs[2].Status = overStatus;
            await m_ProcessHelper.WaitForState(State.Failure);

            // Validate LaunchPadStatus are correctly filled (without dynamic entries so far)
            var a1LaunchedTask = GetLaunchPadStatus(k_LaunchPadA1Id, s => s is {State: LaunchPadState.Launched});
            var a2LaunchedTask = GetLaunchPadStatus(k_LaunchPadA2Id, s => s is {State: LaunchPadState.Launched});
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            var finishedTask = await Task.WhenAny(Task.WhenAll(a1LaunchedTask, a2LaunchedTask), timeoutTask);
            Assert.That(finishedTask, Is.Not.SameAs(timeoutTask)); // Otherwise we timed out

            Assert.That(a1LaunchedTask.Result!.State, Is.EqualTo(LaunchPadState.Launched));
            Assert.That(a1LaunchedTask.Result.DynamicEntries, Is.Empty);
            Assert.That(a2LaunchedTask.Result!.State, Is.EqualTo(LaunchPadState.Launched));
            Assert.That(a2LaunchedTask.Result.DynamicEntries, Is.Empty);

            // Add new dynamic properties
            await Task.WhenAll(
                m_ProcessHelper.PutLaunchPadStatusDynamicEntry(k_LaunchPadA1Id,
                    new() { Name = "Name1", Value = "Value1" }),
                m_ProcessHelper.PutLaunchPadStatusDynamicEntry(k_LaunchPadA2Id,
                    new() { Name = "Name2", Value = "Value2" })
            );

            var a1DynamicEntryTask =
                GetLaunchPadStatus(k_LaunchPadA1Id, s => s!.DynamicEntries.Any(e => e.Name == "Name1"));
            var a2DynamicEntryTask =
                GetLaunchPadStatus(k_LaunchPadA2Id, s => s!.DynamicEntries.Any(e => e.Name == "Name2"));
            timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            finishedTask = await Task.WhenAny(Task.WhenAll(a1DynamicEntryTask, a2DynamicEntryTask), timeoutTask);
            Assert.That(finishedTask, Is.Not.SameAs(timeoutTask)); // Otherwise we timed out

            Assert.That(a1DynamicEntryTask.Result!.DynamicEntries.Count(), Is.EqualTo(1));
            Assert.That(a1DynamicEntryTask.Result!.DynamicEntries.First().Name, Is.EqualTo("Name1"));
            Assert.That(a1DynamicEntryTask.Result!.DynamicEntries.First().Value, Is.EqualTo("Value1"));
            Assert.That(a2DynamicEntryTask.Result!.DynamicEntries.Count(), Is.EqualTo(1));
            Assert.That(a2DynamicEntryTask.Result!.DynamicEntries.First().Name, Is.EqualTo("Name2"));
            Assert.That(a2DynamicEntryTask.Result!.DynamicEntries.First().Value, Is.EqualTo("Value2"));

            // Update dynamic properties
            await Task.WhenAll(
                m_ProcessHelper.PutLaunchPadStatusDynamicEntry(k_LaunchPadA1Id,
                    new() { Name = "Name1", Value = "Value1A" }),
                m_ProcessHelper.PutLaunchPadStatusDynamicEntry(k_LaunchPadA2Id,
                    new() { Name = "Name2", Value = "Value2A" })
            );

            var a1UpdatedDynamicEntryTask = GetLaunchPadStatus(k_LaunchPadA1Id,
                s => (string)s!.DynamicEntries.First(e => e.Name == "Name1").Value == "Value1A");
            var a2UpdatedDynamicEntryTask = GetLaunchPadStatus(k_LaunchPadA2Id,
                s => (string)s!.DynamicEntries.First(e => e.Name == "Name2").Value == "Value2A");
            timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            finishedTask = await Task.WhenAny(
                Task.WhenAll(a1UpdatedDynamicEntryTask, a2UpdatedDynamicEntryTask), timeoutTask);
            Assert.That(finishedTask, Is.Not.SameAs(timeoutTask)); // Otherwise we timed out

            // Stop everything
            await m_ProcessHelper.PostCommand(new StopMissionCommand(), HttpStatusCode.Accepted);

            // This should clear all the dynamic entries
            var a1ClearedDynamicEntriesTask = GetLaunchPadStatus(k_LaunchPadA1Id, s => s!.DynamicEntries.Any());
            var a2ClearedDynamicEntriesTask = GetLaunchPadStatus(k_LaunchPadA2Id, s => s!.DynamicEntries.Any());
            timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            finishedTask = await Task.WhenAny(
                Task.WhenAll(a1ClearedDynamicEntriesTask, a2ClearedDynamicEntriesTask), timeoutTask);
            Assert.That(finishedTask, Is.Not.SameAs(timeoutTask)); // Otherwise we timed out
        }

        [Test]
        public async Task PutDynamicEntriesArray()
        {
            await StartProcessHelper();

            CreateLaunchPadStub(k_LaunchPadA1Port, k_LaunchPadA1Id);
            CreateLaunchPadStub(k_LaunchPadA2Port, k_LaunchPadA2Id);
            PrepareLaunchPadStubForFakeLaunch(m_LaunchPadStubs[0]);
            PrepareLaunchPadStubForFakeLaunch(m_LaunchPadStubs[1]);

            await m_ProcessHelper.PutLaunchComplex(k_ComplexA);

            string assetUrl = await CreateAsset(GetTestTempFolder(), k_LaunchCatalog, new(), k_LaunchCatalogFilesContent);
            AssetPost assetPost = new()
            {
                Name = "Test asset",
                Url = assetUrl
            };
            var assetId = await m_ProcessHelper.PostAsset(assetPost);

            LaunchConfiguration launchConfiguration = new() {
                AssetId = assetId,
                LaunchComplexes = new[]
                {
                    new LaunchComplexConfiguration()
                    {
                        Identifier = k_ComplexA.Id,
                        LaunchPads = m_LaunchPadStubs.Select(
                            lps => new LaunchPadConfiguration() { Identifier = lps.Id, LaunchableName = "Cluster Node" }).ToList()
                    }
                }
            };
            await m_ProcessHelper.PutLaunchConfiguration(launchConfiguration);

            await m_ProcessHelper.PostCommand(new LaunchMissionCommand(), HttpStatusCode.Accepted);
            await m_ProcessHelper.WaitForState(State.Launched);

            // Validate LaunchPadStatus are correctly filled (without dynamic entries so far)
            var a1LaunchedTask = GetLaunchPadStatus(k_LaunchPadA1Id, s => s is {State: LaunchPadState.Launched});
            var a2LaunchedTask = GetLaunchPadStatus(k_LaunchPadA2Id, s => s is {State: LaunchPadState.Launched});
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            var finishedTask = await Task.WhenAny(Task.WhenAll(a1LaunchedTask, a2LaunchedTask), timeoutTask);
            Assert.That(finishedTask, Is.Not.SameAs(timeoutTask)); // Otherwise we timed out

            Assert.That(a1LaunchedTask.Result!.State, Is.EqualTo(LaunchPadState.Launched));
            Assert.That(a1LaunchedTask.Result.DynamicEntries, Is.Empty);
            Assert.That(a2LaunchedTask.Result!.State, Is.EqualTo(LaunchPadState.Launched));
            Assert.That(a2LaunchedTask.Result.DynamicEntries, Is.Empty);

            // Add new dynamic properties
            await Task.WhenAll(
                m_ProcessHelper.PutLaunchPadStatusDynamicEntries(k_LaunchPadA1Id,
                    new[] { new LaunchPadReportDynamicEntry{ Name = "Name1", Value = "A1 - Name1 - Value" },
                            new LaunchPadReportDynamicEntry{ Name = "Name2", Value = "A1 - Name2 - Value" }}),
                m_ProcessHelper.PutLaunchPadStatusDynamicEntries(k_LaunchPadA2Id,
                    new[] { new LaunchPadReportDynamicEntry{ Name = "Name1", Value = "A2 - Name1 - Value" },
                            new LaunchPadReportDynamicEntry{ Name = "Name2", Value = "A2 - Name2 - Value" }})
            );

            var a1DynamicEntryTask =
                GetLaunchPadStatus(k_LaunchPadA1Id, s => s!.DynamicEntries.Count() == 2);
            var a2DynamicEntryTask =
                GetLaunchPadStatus(k_LaunchPadA2Id, s => s!.DynamicEntries.Count() == 2);
            timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            finishedTask = await Task.WhenAny(Task.WhenAll(a1DynamicEntryTask, a2DynamicEntryTask), timeoutTask);
            Assert.That(finishedTask, Is.Not.SameAs(timeoutTask)); // Otherwise we timed out

            Assert.That(a1DynamicEntryTask.Result!.DynamicEntries.Count(), Is.EqualTo(2));
            Assert.That(a1DynamicEntryTask.Result!.DynamicEntries.Any(
                e => e.Name == "Name1" && (string)e.Value == "A1 - Name1 - Value"), Is.True);
            Assert.That(a1DynamicEntryTask.Result!.DynamicEntries.Any(
                e => e.Name == "Name2" && (string)e.Value == "A1 - Name2 - Value"), Is.True);
            Assert.That(a2DynamicEntryTask.Result!.DynamicEntries.Count(), Is.EqualTo(2));
            Assert.That(a2DynamicEntryTask.Result!.DynamicEntries.Any(
                e => e.Name == "Name1" && (string)e.Value == "A2 - Name1 - Value"), Is.True);
            Assert.That(a2DynamicEntryTask.Result!.DynamicEntries.Any(
                e => e.Name == "Name2" && (string)e.Value == "A2 - Name2 - Value"), Is.True);

            // Update dynamic properties
            await Task.WhenAll(
                m_ProcessHelper.PutLaunchPadStatusDynamicEntry(k_LaunchPadA1Id,
                    new(){ Name = "Name2", Value = "A1 - Name2 - Value2" }),
                m_ProcessHelper.PutLaunchPadStatusDynamicEntries(k_LaunchPadA2Id,
                    new[] { new LaunchPadReportDynamicEntry{ Name = "Name1", Value = "A2 - Name1 - Value2" },
                            new LaunchPadReportDynamicEntry{ Name = "Name2", Value = "A2 - Name2 - Value2" }})
            );

            var a1UpdatedDynamicEntryTask = GetLaunchPadStatus(k_LaunchPadA1Id,
                s => s!.DynamicEntries.Count(e => ((string)e.Value).EndsWith("Value2")) == 1);
            var a2UpdatedDynamicEntryTask = GetLaunchPadStatus(k_LaunchPadA2Id,
                s => s!.DynamicEntries.Count(e => ((string)e.Value).EndsWith("Value2")) == 2);
            timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            finishedTask = await Task.WhenAny(
                Task.WhenAll(a1UpdatedDynamicEntryTask, a2UpdatedDynamicEntryTask), timeoutTask);
            Assert.That(finishedTask, Is.Not.SameAs(timeoutTask)); // Otherwise we timed out

            Assert.That(a1UpdatedDynamicEntryTask.Result!.DynamicEntries.Count(), Is.EqualTo(2));
            Assert.That(a1UpdatedDynamicEntryTask.Result!.DynamicEntries.Any(
                e => e.Name == "Name1" && (string)e.Value == "A1 - Name1 - Value"), Is.True);
            Assert.That(a1UpdatedDynamicEntryTask.Result!.DynamicEntries.Any(
                e => e.Name == "Name2" && (string)e.Value == "A1 - Name2 - Value2"), Is.True);
            Assert.That(a2UpdatedDynamicEntryTask.Result!.DynamicEntries.Count(), Is.EqualTo(2));
            Assert.That(a2UpdatedDynamicEntryTask.Result!.DynamicEntries.Any(
                e => e.Name == "Name1" && (string)e.Value == "A2 - Name1 - Value2"), Is.True);
            Assert.That(a2UpdatedDynamicEntryTask.Result!.DynamicEntries.Any(
                e => e.Name == "Name2" && (string)e.Value == "A2 - Name2 - Value2"), Is.True);

            // Stop everything
            await m_ProcessHelper.PostCommand(new StopMissionCommand(), HttpStatusCode.Accepted);

            // This should clear all the dynamic entries
            var a1ClearedDynamicEntriesTask = GetLaunchPadStatus(k_LaunchPadA1Id, s => s!.DynamicEntries.Any());
            var a2ClearedDynamicEntriesTask = GetLaunchPadStatus(k_LaunchPadA2Id, s => s!.DynamicEntries.Any());
            timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            finishedTask = await Task.WhenAny(
                Task.WhenAll(a1ClearedDynamicEntriesTask, a2ClearedDynamicEntriesTask), timeoutTask);
            Assert.That(finishedTask, Is.Not.SameAs(timeoutTask)); // Otherwise we timed out
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
                    (IncrementalCollectionsName.LaunchPadsStatus, fromVersion ) }, m_LaunchPadsStatusCts.Token);
                if (update.ContainsKey(IncrementalCollectionsName.LaunchPadsStatus))
                {
                    var incrementalUpdate = JsonSerializer.Deserialize<IncrementalCollectionUpdate<LaunchPadStatus>>(
                        update[IncrementalCollectionsName.LaunchPadsStatus], Json.SerializerOptions);
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

        LaunchPadStub CreateLaunchPadStub(int port, Guid id = default)
        {
            LaunchPadStub newStub = new(port, id);
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

        static void PrepareLaunchPadStubForFakeLaunch(LaunchPadStub stub)
        {
            Dictionary<LaunchPadCommandType, LaunchPadState> commandTypeToState = new(){
                { LaunchPadCommandType.Prepare, LaunchPadState.WaitingForLaunch},
                { LaunchPadCommandType.Launch, LaunchPadState.Launched},
                { LaunchPadCommandType.Abort, LaunchPadState.Over} };
            stub.CommandHandler = command => {
                if (commandTypeToState.TryGetValue(command.Type, out var launchPadState))
                {
                    StatusFromLaunchpad runningStatus = new();
                    runningStatus.DeepCopyFrom(stub.Status);
                    runningStatus.State = launchPadState;
                    stub.Status = runningStatus;
                    return HttpStatusCode.OK;
                }
                else
                {
                    return HttpStatusCode.BadRequest;
                }
            };
        }

        static readonly LaunchCatalog.Catalog k_LaunchCatalog = new()
        {
            Payloads = new[] {
                new LaunchCatalog.Payload()
                {
                    Name = "Payload",
                    Files = new []
                    {
                        new LaunchCatalog.PayloadFile() { Path = "launch.ps1" }
                    }
                }
            },
            Launchables = new[] {
                new LaunchCatalog.Launchable()
                {
                    Name = "Cluster Node",
                    Type = "clusterNode",
                    Payloads = new [] { "Payload" },
                    LaunchPath = "launch.ps1"
                }
            }
        };
        static readonly Dictionary<string, MemoryStream> k_LaunchCatalogFilesContent = new() {
            { "launch.ps1", MemoryStreamFromString("dummy") }
        };

        readonly MissionControlProcessHelper m_ProcessHelper = new();
        readonly List<string> m_TestTempFolders = new();
        readonly List<LaunchPadStub> m_LaunchPadStubs = new();

        readonly object m_Lock = new();
        IncrementalCollection<LaunchPadStatus>? m_LaunchPadsStatus;
        CancellationTokenSource? m_LaunchPadsStatusCts;
        TaskCompletionSource? m_LaunchPadsStatusUpdated;
    }
}
