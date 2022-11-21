using System.Net;
using static Unity.ClusterDisplay.MissionControl.MissionControl.Tests.Helpers;

namespace Unity.ClusterDisplay.MissionControl.MissionControl.Tests
{
    public class MissionsControllerTests
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
        public async Task Get()
        {
            await m_ProcessHelper.Start(GetTestTempFolder());

            // Setup a simple launch configuration
            LaunchConfiguration launchConfiguration = new()
            {
                AssetId = await PostAsset(m_ProcessHelper, GetTestTempFolder(), k_SimpleLaunchCatalog,
                k_SimpleLaunchCatalogFileLength)
            };
            await m_ProcessHelper.PutLaunchConfiguration(launchConfiguration);

            // Save it
            SaveMissionCommand saveCommand = new()
            {
                Identifier = Guid.NewGuid(),
                Description = new() {
                    Name = "Saved mission",
                    Details = "Saved mission details"
                }
            };
            await m_ProcessHelper.PostCommand(saveCommand);

            // Get it
            var savedMission = await m_ProcessHelper.GetSavedMission(saveCommand.Identifier);
            Assert.That(savedMission.Id, Is.EqualTo(saveCommand.Identifier));
            Assert.That(savedMission.AssetId, Is.EqualTo(launchConfiguration.AssetId));

            // Get a mission that does not exist
            var ret = await m_ProcessHelper.GetSavedMissionWithStatusCode(Guid.NewGuid());
            Assert.That(ret, Is.EqualTo(HttpStatusCode.NotFound));
        }

        [Test]
        public async Task GetAll()
        {
            await m_ProcessHelper.Start(GetTestTempFolder());

            // Setup first launch configurations
            LaunchConfiguration launchConfiguration1 = new()
            {
                AssetId = await PostAsset(m_ProcessHelper, GetTestTempFolder(), k_SimpleLaunchCatalog,
                    k_SimpleLaunchCatalogFileLength, "First asset")
            };
            await m_ProcessHelper.PutLaunchConfiguration(launchConfiguration1);

            // Save it
            SaveMissionCommand saveCommand1 = new()
            {
                Identifier = Guid.NewGuid(),
                Description = new() {
                    Name = "First saved mission",
                    Details = "First saved mission details"
                }
            };
            await m_ProcessHelper.PostCommand(saveCommand1);

            // Setup second launch configurations
            LaunchConfiguration launchConfiguration2 = new()
            {
                AssetId = await PostAsset(m_ProcessHelper, GetTestTempFolder(), k_SimpleLaunchCatalog,
                    k_SimpleLaunchCatalogFileLength, "Second asset")
            };
            await m_ProcessHelper.PutLaunchConfiguration(launchConfiguration2);

            // Save it
            SaveMissionCommand saveCommand2 = new()
            {
                Identifier = Guid.NewGuid(),
                Description = new() {
                    Name = "Second saved mission",
                    Details = "Second saved mission details"
                }
            };
            await m_ProcessHelper.PostCommand(saveCommand2);

            // Get them
            var savedMissions = await m_ProcessHelper.GetSavedMissions();
            Assert.That(savedMissions.Length, Is.EqualTo(2));
            Dictionary<Guid, LaunchConfiguration> saveIdToLaunchConfiguration = new() {
                {saveCommand1.Identifier, launchConfiguration1}, {saveCommand2.Identifier, launchConfiguration2}};
            foreach (var savedMission in savedMissions)
            {
                var launchConfiguration = saveIdToLaunchConfiguration[savedMission.Id];
                saveIdToLaunchConfiguration.Remove(savedMission.Id);
                Assert.That(savedMission.AssetId, Is.EqualTo(launchConfiguration.AssetId));
            }
        }

        [Test]
        public async Task GetBlocking()
        {
            await m_ProcessHelper.Start(GetTestTempFolder());

            // Get the initial incremental update
            var blockingGetTask = m_ProcessHelper.GetIncrementalCollectionsUpdate(new List<(string, ulong)> {
                (k_MissionsCollectionName, 1) });

            // Setup first launch configurations
            LaunchConfiguration launchConfiguration1 = new()
            {
                AssetId = await PostAsset(m_ProcessHelper, GetTestTempFolder(), k_SimpleLaunchCatalog,
                    k_SimpleLaunchCatalogFileLength, "First asset")
            };
            await m_ProcessHelper.PutLaunchConfiguration(launchConfiguration1);

            await Task.Delay(100); // So that it has the time to complete if blocking would fail
            Assert.That(blockingGetTask.IsCompleted, Is.False);

            // Save it
            SaveMissionCommand saveCommand1 = new()
            {
                Identifier = Guid.NewGuid(),
                Description = new() {
                    Name = "First saved mission",
                    Details = "First saved mission details"
                }
            };
            await m_ProcessHelper.PostCommand(saveCommand1);

            // Above save should have unblocked the blocking get
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            var finishedTask = await Task.WhenAny(blockingGetTask, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(blockingGetTask)); // Otherwise we timed out
            var collectionUpdate = GetCollectionUpdate<SavedMissionSummary>(blockingGetTask.Result, k_MissionsCollectionName);
            Assert.That(collectionUpdate.UpdatedObjects.Count, Is.EqualTo(1));
            Assert.That(collectionUpdate.RemovedObjects, Is.Empty);
            Assert.That(collectionUpdate.NextUpdate, Is.EqualTo(2));

            // Ask for the next update
            blockingGetTask = m_ProcessHelper.GetIncrementalCollectionsUpdate(new List<(string, ulong)> {
                (k_MissionsCollectionName, 2) });

            // Setup second launch configurations
            LaunchConfiguration launchConfiguration2 = new()
            {
                AssetId = await PostAsset(m_ProcessHelper, GetTestTempFolder(), k_SimpleLaunchCatalog,
                    k_SimpleLaunchCatalogFileLength, "Second asset")
            };
            await m_ProcessHelper.PutLaunchConfiguration(launchConfiguration2);

            await Task.Delay(100); // So that it has the time to complete if blocking would fail
            Assert.That(blockingGetTask.IsCompleted, Is.False);

            // Save it
            SaveMissionCommand saveCommand2 = new()
            {
                Identifier = Guid.NewGuid(),
                Description = new()
                {
                    Name = "Second saved mission",
                    Details = "Second saved mission details"
                }
            };
            await m_ProcessHelper.PostCommand(saveCommand2);

            // Above save should have unblocked the blocking get
            timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            finishedTask = await Task.WhenAny(blockingGetTask, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(blockingGetTask)); // Otherwise we timed out
            collectionUpdate = GetCollectionUpdate<SavedMissionSummary>(blockingGetTask.Result, k_MissionsCollectionName);
            Assert.That(collectionUpdate.UpdatedObjects.Count, Is.EqualTo(1));
            Assert.That(collectionUpdate.RemovedObjects, Is.Empty);
            Assert.That(collectionUpdate.NextUpdate, Is.EqualTo(3));
        }

        [Test]
        public async Task Delete()
        {
            await m_ProcessHelper.Start(GetTestTempFolder());

            // Setup first launch configurations
            LaunchConfiguration launchConfiguration1 = new()
            {
                AssetId = await PostAsset(m_ProcessHelper, GetTestTempFolder(), k_SimpleLaunchCatalog,
                    k_SimpleLaunchCatalogFileLength, "First asset")
            };
            await m_ProcessHelper.PutLaunchConfiguration(launchConfiguration1);

            // Save it
            SaveMissionCommand saveCommand1 = new()
            {
                Identifier = Guid.NewGuid(),
                Description = new() {
                    Name = "First saved mission",
                    Details = "First saved mission details"
                }
            };
            await m_ProcessHelper.PostCommand(saveCommand1);

            // Setup second launch configurations
            LaunchConfiguration launchConfiguration2 = new()
            {
                AssetId = await PostAsset(m_ProcessHelper, GetTestTempFolder(), k_SimpleLaunchCatalog,
                    k_SimpleLaunchCatalogFileLength, "Second asset")
            };
            await m_ProcessHelper.PutLaunchConfiguration(launchConfiguration2);

            // Save it
            SaveMissionCommand saveCommand2 = new()
            {
                Identifier = Guid.NewGuid(),
                Description = new() {
                    Name = "Second saved mission",
                    Details = "Second saved mission details"
                }
            };
            await m_ProcessHelper.PostCommand(saveCommand2);

            // Get them
            var savedMission = await m_ProcessHelper.GetSavedMission(saveCommand1.Identifier);
            Assert.That(savedMission.Id, Is.EqualTo(saveCommand1.Identifier));
            Assert.That(savedMission.AssetId, Is.EqualTo(launchConfiguration1.AssetId));
            savedMission = await m_ProcessHelper.GetSavedMission(saveCommand2.Identifier);
            Assert.That(savedMission.Id, Is.EqualTo(saveCommand2.Identifier));
            Assert.That(savedMission.AssetId, Is.EqualTo(launchConfiguration2.AssetId));

            // Delete one of them and do some bad deletes
            await m_ProcessHelper.DeleteSavedMission(saveCommand1.Identifier);
            Assert.That(await m_ProcessHelper.DeleteSavedMissionWithStatusCode(saveCommand1.Identifier),
                Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(await m_ProcessHelper.DeleteSavedMissionWithStatusCode(Guid.NewGuid()),
                Is.EqualTo(HttpStatusCode.NotFound));

            // Check everything still ok
            Assert.That(await m_ProcessHelper.GetSavedMissionWithStatusCode(saveCommand1.Identifier),
                Is.EqualTo(HttpStatusCode.NotFound));
            savedMission = await m_ProcessHelper.GetSavedMission(saveCommand2.Identifier);
            Assert.That(savedMission.Id, Is.EqualTo(saveCommand2.Identifier));
            Assert.That(savedMission.AssetId, Is.EqualTo(launchConfiguration2.AssetId));
        }

        static readonly LaunchCatalog.Catalog k_SimpleLaunchCatalog = new()
        {
            Payloads = new[] {
                new LaunchCatalog.Payload()
                {
                    Name = "Payload",
                    Files = new []
                    {
                        new LaunchCatalog.PayloadFile() { Path = "file" }
                    }
                }
            },
            Launchables = new[] {
                new LaunchCatalog.Launchable()
                {
                    Name = "Cluster Node",
                    Type = "clusterNode",
                    Payloads = new [] { "Payload" }
                }
            }
        };
        static readonly Dictionary<string, int> k_SimpleLaunchCatalogFileLength =
            new() { { "file", 1024 } };

        string GetTestTempFolder()
        {
            var folderPath = Path.Combine(Path.GetTempPath(), "MissionsControllerTests_" + Guid.NewGuid().ToString());
            m_TestTempFolders.Add(folderPath);
            return folderPath;
        }

        const string k_MissionsCollectionName = "missions";

        MissionControlProcessHelper m_ProcessHelper = new();
        List<string> m_TestTempFolders = new();
    }
}
