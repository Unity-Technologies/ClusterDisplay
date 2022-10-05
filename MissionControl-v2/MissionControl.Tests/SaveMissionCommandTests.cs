using System.Net;
using static Unity.ClusterDisplay.MissionControl.MissionControl.Tests.Helpers;

namespace Unity.ClusterDisplay.MissionControl.MissionControl.Tests
{
    public class SaveMissionCommandTests
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
        public async Task Name()
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
                Description = "Saved mission description"
            };
            var ret = await m_ProcessHelper.PostCommandWithStatusCode(saveCommand);
            Assert.That(ret, Is.EqualTo(HttpStatusCode.BadRequest));

            // This time give a name and it should work
            saveCommand.Name = "Something";
            await m_ProcessHelper.PostCommand(saveCommand);

            // In fact, we should be able to save multiple with the same name
            saveCommand.Identifier = Guid.NewGuid();
            await m_ProcessHelper.PostCommand(saveCommand);

            var savedMissions = await m_ProcessHelper.GetSavedMissions();
            Assert.That(savedMissions.Count(), Is.EqualTo(2));
        }

        [Test]
        public async Task Identifier()
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
                Name = "Some name",
                Description = "Some description"
            };
            await m_ProcessHelper.PostCommand(saveCommand);

            // In fact, save it a second time, since identifier is Guid.Empty, it should save it with a new identifier
            // creating two saved missions.
            saveCommand.Identifier = Guid.NewGuid();
            await m_ProcessHelper.PostCommand(saveCommand);

            var savedMissions = await m_ProcessHelper.GetSavedMissions();
            Assert.That(savedMissions.Count(), Is.EqualTo(2));
            Assert.That(savedMissions[0].Id, Is.Not.EqualTo(Guid.Empty));
            Assert.That(savedMissions[1].Id, Is.Not.EqualTo(Guid.Empty));

            // Specify the identifier to overwrite it
            saveCommand.Identifier = savedMissions[0].Id;
            saveCommand.Name = "New name";
            saveCommand.Description = "New description";
            await m_ProcessHelper.PostCommand(saveCommand);

            var savedMissionsTake2 = await m_ProcessHelper.GetSavedMissions();
            Assert.That(savedMissions.Count(), Is.EqualTo(2));
            Assert.That(savedMissionsTake2[0].Id, Is.EqualTo(savedMissions[0].Id));
            Assert.That(savedMissionsTake2[0].Name, Is.EqualTo("New name"));
            Assert.That(savedMissionsTake2[0].Description, Is.EqualTo("New description"));
            Assert.That(savedMissionsTake2[1].Id, Is.EqualTo(savedMissions[1].Id));
            Assert.That(savedMissionsTake2[1].Name, Is.EqualTo("Some name"));
            Assert.That(savedMissionsTake2[1].Description, Is.EqualTo("Some description"));
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
