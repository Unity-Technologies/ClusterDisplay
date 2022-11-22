using System;
using System.Net;
using System.Text.Json;

using static Unity.ClusterDisplay.MissionControl.MissionControl.Tests.Helpers;

namespace Unity.ClusterDisplay.MissionControl.MissionControl.Tests
{
    public class CurrentMissionLaunchConfigurationControllerTests
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

        [Test]
        public async Task Put()
        {
            await m_ProcessHelper.Start(GetTestTempFolder());

            // Put first configuration (without an asset)
            LaunchConfiguration launchConfigPut = new()
            {
                LaunchComplexes = new[] {
                    new LaunchComplexConfiguration() {
                        Identifier = Guid.NewGuid(),
                        LaunchPads = new[]
                        {
                            new LaunchPadConfiguration() {
                                Identifier = Guid.NewGuid()
                            }
                        }
                    } }
            };
            await m_ProcessHelper.PutLaunchConfiguration(launchConfigPut);
            Assert.That(await m_ProcessHelper.GetLaunchConfiguration(), Is.EqualTo(launchConfigPut));

            // Try to reference an unknown asset
            var invalidLaunchConfigPut = launchConfigPut.DeepClone();
            invalidLaunchConfigPut.AssetId = Guid.NewGuid();
            var putRet = await m_ProcessHelper.PutLaunchConfigurationWithStatusCode(invalidLaunchConfigPut);
            Assert.That(putRet, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(await m_ProcessHelper.GetLaunchConfiguration(), Is.EqualTo(launchConfigPut));

            // Create an asset
            var assetId = await PostAsset(m_ProcessHelper, GetTestTempFolder(), k_SimpleLaunchCatalog,
                k_SimpleLaunchCatalogFileLength);

            // Set the launch configuration with that asset
            var launchConfigWithAssetPut = launchConfigPut.DeepClone();
            launchConfigWithAssetPut.AssetId = assetId;
            await m_ProcessHelper.PutLaunchConfiguration(launchConfigWithAssetPut);
            Assert.That(await m_ProcessHelper.GetLaunchConfiguration(), Is.EqualTo(launchConfigWithAssetPut));
        }

        [Test]
        [TestCase(State.Idle, HttpStatusCode.OK)]
        [TestCase(State.Preparing, HttpStatusCode.Conflict)]
        [TestCase(State.Launched, HttpStatusCode.Conflict)]
        [TestCase(State.Failure, HttpStatusCode.Conflict)]
        public async Task PutInState(State missionControlState, HttpStatusCode expectedStatusCode)
        {
            await m_ProcessHelper.Start(GetTestTempFolder());

            LaunchConfiguration launchConfigPut = new()
            {
                LaunchComplexes = new[] {
                    new LaunchComplexConfiguration() {
                        Identifier = Guid.NewGuid(),
                        LaunchPads = new[]
                        {
                            new LaunchPadConfiguration() {
                                Identifier = Guid.NewGuid()
                            }
                        }
                    } }
            };

            using (m_ProcessHelper.ForceState(missionControlState))
            {
                var status = await m_ProcessHelper.PutLaunchConfigurationWithStatusCode(launchConfigPut);
                Assert.That(status, Is.EqualTo(expectedStatusCode));
            }
        }

        [Test]
        public async Task BlockingGet()
        {
            await m_ProcessHelper.Start(GetTestTempFolder());

            var objectsUpdate = await m_ProcessHelper.GetObjectsUpdate(new[] { (k_LaunchConfigurationObjectName, 0ul) });
            var launchConfigUpdate1 = objectsUpdate[k_LaunchConfigurationObjectName];
            var objectsUpdateTask = m_ProcessHelper.GetObjectsUpdate(
                new[] { (k_LaunchConfigurationObjectName, launchConfigUpdate1.NextUpdate) });
            await Task.Delay(100); // So that objectsUpdateTask can have the time to complete if it wouldn't block
            Assert.That(objectsUpdateTask.IsCompleted, Is.False);

            // Put first configuration (without an asset)
            LaunchConfiguration launchConfigPut = new()
            {
                LaunchComplexes = new[] {
                    new LaunchComplexConfiguration() {
                        Identifier = Guid.NewGuid(),
                        LaunchPads = new[]
                        {
                            new LaunchPadConfiguration() {
                                Identifier = Guid.NewGuid()
                            }
                        }
                    } }
            };
            await m_ProcessHelper.PutLaunchConfiguration(launchConfigPut);
            Assert.That(await m_ProcessHelper.GetLaunchConfiguration(), Is.EqualTo(launchConfigPut));

            // Should have unlocked the get
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            var awaitTask = await Task.WhenAny(objectsUpdateTask, timeoutTask);
            Assert.That(awaitTask, Is.SameAs(objectsUpdateTask)); // Or else timed out
            var launchConfigUpdate2 = objectsUpdateTask.Result[k_LaunchConfigurationObjectName];
            Assert.That(JsonSerializer.Deserialize<LaunchConfiguration>(launchConfigUpdate2.Updated, Json.SerializerOptions),
                Is.EqualTo(launchConfigPut));

            // Start another blocking get
            objectsUpdateTask = m_ProcessHelper.GetObjectsUpdate(
                new[] { (k_LaunchConfigurationObjectName, launchConfigUpdate2.NextUpdate) });

            // Put the same thing, it shouldn't unblock the previous get
            await m_ProcessHelper.PutLaunchConfiguration(launchConfigPut);
            await Task.Delay(100); // So that objectsUpdateTask can have the time to complete if it wouldn't block
            Assert.That(objectsUpdateTask.IsCompleted, Is.False);
        }

        [Test]
        public async Task SerializeWithRemovingAsset()
        {
            var configFolder = GetTestTempFolder();
            await m_ProcessHelper.Start(configFolder);

            // Create an asset
            var assetId = await PostAsset(m_ProcessHelper, GetTestTempFolder(), k_SimpleLaunchCatalog,
                k_SimpleLaunchCatalogFileLength);

            // Stall its deletion
            string stallDeletePath = Path.Combine(configFolder, $"stall_{assetId}");
            File.WriteAllText(stallDeletePath, "Stall delete of asset");

            // Delete the asset (or in fact, start deleting it).
            var deleteTask = m_ProcessHelper.DeleteAsset(assetId);
            await Task.Delay(100);
            Assert.That(deleteTask.IsCompleted, Is.False);

            // Start setting the current launch configuration to launch that asset.
            LaunchConfiguration launchConfigPut = new()
            {
                LaunchComplexes = new[] {
                    new LaunchComplexConfiguration() {
                        Identifier = Guid.NewGuid(),
                        LaunchPads = new[]
                        {
                            new LaunchPadConfiguration() {
                                Identifier = Guid.NewGuid()
                            }
                        }
                    } },
                AssetId = assetId
            };
            var putConfigurationTask = m_ProcessHelper.PutLaunchConfigurationWithStatusCode(launchConfigPut);
            await Task.Delay(100);
            Assert.That(putConfigurationTask.IsCompleted, Is.False);

            // Resume delete which should allow everything to finish
            File.Delete(stallDeletePath);

            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            var awaitTask = await Task.WhenAny(deleteTask, timeoutTask);
            Assert.That(awaitTask, Is.SameAs(deleteTask)); // Or else it timed out
            awaitTask = await Task.WhenAny(putConfigurationTask, timeoutTask);
            Assert.That(awaitTask, Is.SameAs(putConfigurationTask)); // Or else it timed out

            // Set launch configuration should have failed because the asset did not exist
            Assert.That(putConfigurationTask.Result, Is.EqualTo(HttpStatusCode.BadRequest));

            // Just to be sure this is really why it failed, set the AssetId to something valid and try again.
            launchConfigPut.AssetId = Guid.Empty;
            await m_ProcessHelper.PutLaunchConfiguration(launchConfigPut);
        }

        [Test]
        public async Task Persisted()
        {
            string missionControlFolder = GetTestTempFolder();
            await m_ProcessHelper.Start(missionControlFolder);

            // Set the launch configuration with that asset
            var assetId = await PostAsset(m_ProcessHelper, GetTestTempFolder(), k_SimpleLaunchCatalog,
                k_SimpleLaunchCatalogFileLength);
            LaunchConfiguration launchConfigPut = new()
            {
                AssetId = assetId,
                LaunchComplexes = new[] {
                    new LaunchComplexConfiguration() {
                        Identifier = Guid.NewGuid(),
                        LaunchPads = new[]
                        {
                            new LaunchPadConfiguration() {
                                Identifier = Guid.NewGuid()
                            }
                        }
                    } }
            };
            await m_ProcessHelper.PutLaunchConfiguration(launchConfigPut);

            // Shutdown mission control
            m_ProcessHelper.Stop();
            await m_ProcessHelper.Start(missionControlFolder);

            // LaunchConfiguration should have been restored
            Assert.That(await m_ProcessHelper.GetLaunchConfiguration(), Is.EqualTo(launchConfigPut));
        }

        string GetTestTempFolder()
        {
            var folderPath = Path.Combine(Path.GetTempPath(), "CurrentMissionLaunchConfigurationControllerTests_" + Guid.NewGuid().ToString());
            m_TestTempFolders.Add(folderPath);
            return folderPath;
        }

        const string k_LaunchConfigurationObjectName = "currentMission/launchConfiguration";

        MissionControlProcessHelper m_ProcessHelper = new();
        List<string> m_TestTempFolders = new();
    }
}
