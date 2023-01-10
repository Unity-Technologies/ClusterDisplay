using System.Net;
using static Unity.ClusterDisplay.MissionControl.MissionControl.Helpers;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    public class LoadMissionCommandTests
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
        public async Task Load()
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
                Description = new() { Name = "Mission name" }
            };
            await m_ProcessHelper.PostCommand(saveCommand);

            // Clear current launch configuration
            await m_ProcessHelper.PutLaunchConfiguration(new());

            // Load command but with default identifier (Guid.Empty), should fail.
            LoadMissionCommand loadCommand = new();
            var ret = await m_ProcessHelper.PostCommandWithStatusCode(loadCommand);
            Assert.That(ret, Is.EqualTo(HttpStatusCode.BadRequest));

            // Or with an identifier that was never saved
            loadCommand.Identifier = Guid.NewGuid();
            ret = await m_ProcessHelper.PostCommandWithStatusCode(loadCommand);
            Assert.That(ret, Is.EqualTo(HttpStatusCode.BadRequest));

            // Ensure current launch configuration is still intact (cleared)
            var currentConfiguration = await m_ProcessHelper.GetLaunchConfiguration();
            Assert.That(currentConfiguration, Is.EqualTo(new LaunchConfiguration()));

            // Now really load the launch configuration
            loadCommand.Identifier = saveCommand.Identifier;
            await m_ProcessHelper.PostCommand(loadCommand);

            // Ensure everything was loaded with success
            currentConfiguration = await m_ProcessHelper.GetLaunchConfiguration();
            Assert.That(currentConfiguration, Is.EqualTo(launchConfiguration));
        }

        [Test]
        [TestCase(State.Idle, HttpStatusCode.OK)]
        [TestCase(State.Preparing, HttpStatusCode.Conflict)]
        [TestCase(State.Launched, HttpStatusCode.Conflict)]
        [TestCase(State.Failure, HttpStatusCode.Conflict)]
        public async Task LoadInState(State missionControlState, HttpStatusCode expectedStatusCode)
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
                Description = new() { Name = "Mission name" }
            };
            await m_ProcessHelper.PostCommand(saveCommand);

            // Clear current launch configuration
            await m_ProcessHelper.PutLaunchConfiguration(new());

            // Try to send a load mission command when state is forced
            using (m_ProcessHelper.ForceState(missionControlState))
            {
                LoadMissionCommand loadCommand = new() { Identifier = saveCommand.Identifier };
                var ret = await m_ProcessHelper.PostCommandWithStatusCode(loadCommand);
                Assert.That(ret, Is.EqualTo(expectedStatusCode));
            }
        }

        // This is to test that LoadCommand is blocked by something else that is locking the current status.
        [Test]
        public async Task BlockedByStatusLock()
        {
            string configPath = GetTestTempFolder();
            await m_ProcessHelper.Start(configPath);

            // Setup a simple launch configuration
            LaunchConfiguration launchConfiguration1 = new()
            {
                AssetId = await PostAsset(m_ProcessHelper, GetTestTempFolder(), k_SimpleLaunchCatalog,
                    k_SimpleLaunchCatalogFileLength)
            };
            await m_ProcessHelper.PutLaunchConfiguration(launchConfiguration1);

            // Save it
            SaveMissionCommand saveCommand = new()
            {
                Identifier = Guid.NewGuid(),
                Description = new() { Name = "Mission name" }
            };
            await m_ProcessHelper.PostCommand(saveCommand);

            // Clear current launch configuration
            await m_ProcessHelper.PutLaunchConfiguration(new());

            // Lock the status of MissionControl
            Task stalledLoadMission;
            using (m_ProcessHelper.ForceState(State.Idle, true))
            {
                // Try to load, should stall waiting for the status lock to be released.
                LoadMissionCommand loadMission = new() { Identifier = saveCommand.Identifier };
                stalledLoadMission = m_ProcessHelper.PostCommand(loadMission);
                await Task.Delay(100); // So that it has the time to complete if blocking would fail
                Assert.That(stalledLoadMission.IsCompleted, Is.False);
            }

            // Wait for tasks to finish
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            var finishedTask = await Task.WhenAny(stalledLoadMission, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(stalledLoadMission)); // Otherwise we timed out

            // Get the current launch configuration (validating that everything executed even tough delayed).
            var currentConfiguration = await m_ProcessHelper.GetLaunchConfiguration();
            Assert.That(currentConfiguration, Is.EqualTo(launchConfiguration1));
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
            var folderPath = Path.Combine(Path.GetTempPath(), "AssetsControllerTests_" + Guid.NewGuid().ToString());
            m_TestTempFolders.Add(folderPath);
            return folderPath;
        }

        MissionControlProcessHelper m_ProcessHelper = new();
        List<string> m_TestTempFolders = new();
    }
}
