using System;
using System.Net;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    public class ComplexesControllerTests
    {
        [TearDown]
        public void TearDown()
        {
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
        }

        [Test]
        public async Task Add()
        {
            await m_ProcessHelper.Start(GetTestTempFolder());

            // Add one
            await m_ProcessHelper.PutLaunchComplex(k_ComplexA);
            Assert.That((await m_ProcessHelper.GetComplexes()).Length, Is.EqualTo(1));
            await TestComplex(k_ComplexA);

            // Add a second one
            await m_ProcessHelper.PutLaunchComplex(k_ComplexB);
            Assert.That((await m_ProcessHelper.GetComplexes()).Length, Is.EqualTo(2));
            await TestComplex(k_ComplexA);
            await TestComplex(k_ComplexB);
        }

        [Test]
        [TestCase(State.Idle, HttpStatusCode.OK)]
        [TestCase(State.Preparing, HttpStatusCode.Conflict)]
        [TestCase(State.Launched, HttpStatusCode.Conflict)]
        [TestCase(State.Failure, HttpStatusCode.Conflict)]
        public async Task Put(State missionControlState, HttpStatusCode expectedStatusCode)
        {
            await m_ProcessHelper.Start(GetTestTempFolder());

            using (m_ProcessHelper.ForceState(missionControlState))
            {
                var statusCode = await m_ProcessHelper.PutLaunchComplexWithStatusCode(k_ComplexA);
                Assert.That(statusCode, Is.EqualTo(expectedStatusCode));
                if (expectedStatusCode == HttpStatusCode.OK)
                {
                    await TestComplex(k_ComplexA);
                }
            }
        }

        [Test]
        public async Task AddBadComplex()
        {
            await m_ProcessHelper.Start(GetTestTempFolder());

            var clonedComplexA = k_ComplexA.DeepClone();
            clonedComplexA.HangarBay.Identifier = Guid.NewGuid();
            Assert.That(await m_ProcessHelper.PutLaunchComplexWithStatusCode(clonedComplexA),
                Is.EqualTo(HttpStatusCode.BadRequest));
        }

        [Test]
        public async Task Delete()
        {
            await m_ProcessHelper.Start(GetTestTempFolder());

            // Add
            await m_ProcessHelper.PutLaunchComplex(k_ComplexA);
            await m_ProcessHelper.PutLaunchComplex(k_ComplexB);
            Assert.That((await m_ProcessHelper.GetComplexes()).Length, Is.EqualTo(2));

            // Remove
            await m_ProcessHelper.DeleteComplex(k_ComplexA.Id);
            Assert.That((await m_ProcessHelper.GetComplexes()).Length, Is.EqualTo(1));
            await TestComplex(k_ComplexB);
        }

        [Test]
        [TestCase(State.Idle, HttpStatusCode.OK)]
        [TestCase(State.Preparing, HttpStatusCode.Conflict)]
        [TestCase(State.Launched, HttpStatusCode.Conflict)]
        [TestCase(State.Failure, HttpStatusCode.Conflict)]
        public async Task Delete(State missionControlState, HttpStatusCode expectedStatusCode)
        {
            await m_ProcessHelper.Start(GetTestTempFolder());

            // Add
            await m_ProcessHelper.PutLaunchComplex(k_ComplexA);
            await m_ProcessHelper.PutLaunchComplex(k_ComplexB);
            Assert.That((await m_ProcessHelper.GetComplexes()).Length, Is.EqualTo(2));

            // Try to delete
            using (m_ProcessHelper.ForceState(missionControlState))
            {
                var statusCode = await m_ProcessHelper.DeleteComplexWithStatusCode(k_ComplexA.Id);
                Assert.That(statusCode, Is.EqualTo(expectedStatusCode));

                int expectedComplexes;
                if (expectedStatusCode == HttpStatusCode.OK)
                {
                    expectedComplexes = 1;
                }
                else
                {
                    expectedComplexes = 2;
                    await TestComplex(k_ComplexA);
                }
                Assert.That((await m_ProcessHelper.GetComplexes()).Length, Is.EqualTo(expectedComplexes));
                await TestComplex(k_ComplexB);
            }
        }

        [Test]
        public async Task DeleteWrongId()
        {
            await m_ProcessHelper.Start(GetTestTempFolder());

            await m_ProcessHelper.PutLaunchComplex(k_ComplexA);
            await m_ProcessHelper.PutLaunchComplex(k_ComplexB);

            // Remove
            Assert.That(await m_ProcessHelper.DeleteComplexWithStatusCode(Guid.NewGuid()),
                Is.EqualTo(HttpStatusCode.NotFound));

            // Test that remove really did nothing...
            Assert.That((await m_ProcessHelper.GetComplexes()).Length, Is.EqualTo(2));
            await TestComplex(k_ComplexA);
            await TestComplex(k_ComplexB);
        }

        [Test]
        public async Task LoadSave()
        {
            string missionControlFolder = GetTestTempFolder();
            await m_ProcessHelper.Start(missionControlFolder);

            // Add
            await m_ProcessHelper.PutLaunchComplex(k_ComplexA);
            await m_ProcessHelper.PutLaunchComplex(k_ComplexB);

            // Check everything ok before shutdown
            Assert.That((await m_ProcessHelper.GetComplexes()).Length, Is.EqualTo(2));
            await TestComplex(k_ComplexA);
            await TestComplex(k_ComplexB);

            // Restart MissionControl
            m_ProcessHelper.Stop();
            await m_ProcessHelper.Start(missionControlFolder);

            // Check everything appear to be loaded back
            Assert.That((await m_ProcessHelper.GetComplexes()).Length, Is.EqualTo(2));
            await TestComplex(k_ComplexA);
            await TestComplex(k_ComplexB);

            // Delete one of them
            await m_ProcessHelper.DeleteComplex(k_ComplexB.Id);

            // Restart MissionControl
            m_ProcessHelper.Stop();
            await m_ProcessHelper.Start(missionControlFolder);

            // Check everything appear to be loaded back
            Assert.That((await m_ProcessHelper.GetComplexes()).Length, Is.EqualTo(1));
            await TestComplex(k_ComplexA);
        }

        [Test]
        public async Task IncrementalCollection()
        {
            await m_ProcessHelper.Start(GetTestTempFolder());

            // Setup initial get, should block since there is never been anything in the asset collection yet
            var blockingGetTask = m_ProcessHelper.GetIncrementalCollectionsUpdate(new List<(string, ulong)> {
                (k_ComplexesCollectionName, 1 ) });
            await Task.Delay(100); // So that it has the time to complete if blocking would fail
            Assert.That(blockingGetTask.IsCompleted, Is.False);

            // Add asset
            await m_ProcessHelper.PutLaunchComplex(k_ComplexA);

            // Should unblock the incremental collection update
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            var finishedTask = await Task.WhenAny(blockingGetTask, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(blockingGetTask)); // Otherwise we timed out
            var collectionUpdate = Helpers.GetCollectionUpdate<LaunchComplex>(blockingGetTask.Result,
                k_ComplexesCollectionName);
            Assert.That(collectionUpdate.UpdatedObjects.Count, Is.EqualTo(1));
            Assert.That(collectionUpdate.RemovedObjects, Is.Empty);
            Assert.That(collectionUpdate.NextUpdate, Is.EqualTo(2));

            // Start another get that should get stuck until we perform the delete
            blockingGetTask = m_ProcessHelper.GetIncrementalCollectionsUpdate(new List<(string, ulong)> {
                (k_ComplexesCollectionName, collectionUpdate.NextUpdate ) });
            await Task.Delay(100); // So that it has the time to complete if blocking would fail
            Assert.That(blockingGetTask.IsCompleted, Is.False);

            // Delete
            await m_ProcessHelper.DeleteComplex(k_ComplexA.Id);

            // Should unblock the incremental collection update
            timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            finishedTask = await Task.WhenAny(blockingGetTask, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(blockingGetTask)); // Otherwise we timed out
            collectionUpdate = Helpers.GetCollectionUpdate<LaunchComplex>(blockingGetTask.Result,
                k_ComplexesCollectionName);
            Assert.That(collectionUpdate.UpdatedObjects, Is.Empty);
            Assert.That(collectionUpdate.RemovedObjects.Count, Is.EqualTo(1));
            Assert.That(collectionUpdate.NextUpdate, Is.EqualTo(3));
        }

        static readonly LaunchComplex k_ComplexA;
        static readonly LaunchComplex k_ComplexB;

        static ComplexesControllerTests()
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
                        Identifier = Guid.NewGuid(),
                        Name = "A1",
                        Endpoint = new("http://127.0.0.1:8201"),
                        SuitableFor = new []{ "clusterNode" }
                    },
                    new LaunchPad()
                    {
                        Identifier = Guid.NewGuid(),
                        Name = "A2",
                        Endpoint = new("http://127.0.0.1:8202"),
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
                        Identifier = Guid.NewGuid(),
                        Name = "B1",
                        Endpoint = new("http://127.0.0.1:8203"),
                        SuitableFor = new []{ "clusterNode" }
                    }
                }
            };
        }

        async Task TestComplex(LaunchComplex expected)
        {
            var complex = await m_ProcessHelper.GetComplex(expected.Id);
            Assert.That(complex, Is.EqualTo(expected));
        }

        string GetTestTempFolder()
        {
            var folderPath = Path.Combine(Path.GetTempPath(), "ComplexesControllerTests_" + Guid.NewGuid().ToString());
            m_TestTempFolders.Add(folderPath);
            return folderPath;
        }

        const string k_ComplexesCollectionName = "complexes";

        MissionControlProcessHelper m_ProcessHelper = new();
        List<string> m_TestTempFolders = new();
    }
}
