using System.Net;

using static Unity.ClusterDisplay.MissionControl.MissionControl.Helpers;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    // Since MissionParametersService doesn't do much except making the base class accessible and is so quite similar
    // to MissionParametersDesiredValuesService, we will focus tests on what can be wrong...
    public class MissionParametersControllerTests
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
        public async Task PutFailIfNoAsset()
        {
            await m_ProcessHelper.Start(GetTestTempFolder());

            MissionParameter valueToPut = new(Guid.NewGuid())
            {
                ValueIdentifier = "parameter name",
                Name = "Parameter's name",
                Type = MissionParameterType.String
            };
            await m_ProcessHelper.PutMissionParameter(valueToPut, HttpStatusCode.Conflict);
        }

        [Test]
        public async Task PutGet()
        {
            await m_ProcessHelper.Start(GetTestTempFolder());

            var assetId = await PostNewAssetAsync(k_LaunchCatalog1, k_LaunchCatalog1FileLength);
            await SetCurrentLaunchAsset(assetId);

            // Put first
            MissionParameter valueToPut1 = new(Guid.NewGuid())
            {
                ValueIdentifier = "value to put 1",
                Name = "Value to put 1",
                Type = MissionParameterType.String
            };
            await m_ProcessHelper.PutMissionParameter(valueToPut1);

            var parameters = await m_ProcessHelper.GetMissionParameters();
            Assert.That(parameters.Count(), Is.EqualTo(1));
            Assert.That(parameters.First(), Is.EqualTo(valueToPut1));

            var parameter = await m_ProcessHelper.GetMissionParameter(valueToPut1.Id);
            Assert.That(parameter, Is.EqualTo(valueToPut1));

            // Put second
            MissionParameter valueToPut2 = new(Guid.NewGuid())
            {
                ValueIdentifier = "value to put 2",
                Name = "Value to put 2",
                Type = MissionParameterType.Integer
            };
            await m_ProcessHelper.PutMissionParameter(valueToPut2);

            parameters = await m_ProcessHelper.GetMissionParameters();
            Assert.That(parameters.Count(), Is.EqualTo(2));
            Assert.That(parameters.First(p => p.Id == valueToPut1.Id), Is.EqualTo(valueToPut1));
            Assert.That(parameters.First(p => p.Id == valueToPut2.Id), Is.EqualTo(valueToPut2));

            parameter = await m_ProcessHelper.GetMissionParameter(valueToPut1.Id);
            Assert.That(parameter, Is.EqualTo(valueToPut1));
            parameter = await m_ProcessHelper.GetMissionParameter(valueToPut2.Id);
            Assert.That(parameter, Is.EqualTo(valueToPut2));

            // While at it, try a get with an invalid id
            Assert.That(await m_ProcessHelper.GetEffectiveParameterValueStatusCode(Guid.NewGuid()),
                Is.EqualTo(HttpStatusCode.NotFound));
        }

        [Test]
        public async Task IncrementalCollectionsUpdate()
        {
            await m_ProcessHelper.Start(GetTestTempFolder());

            var assetId = await PostNewAssetAsync(k_LaunchCatalog1, k_LaunchCatalog1FileLength);
            await SetCurrentLaunchAsset(assetId);

            var blockingGetTask = m_ProcessHelper.GetIncrementalCollectionsUpdate(new List<(string, ulong)> {
                (IncrementalCollectionsName.CurrentMissionParameters, 1) });
            await Task.Delay(100); // So that it has the time to complete if blocking would fail
            Assert.That(blockingGetTask.IsCompleted, Is.False);

            // Put something
            MissionParameter valueToPut = new(Guid.NewGuid())
            {
                ValueIdentifier = "parameter name",
                Name = "Parameter's name",
                Type = MissionParameterType.String
            };
            await m_ProcessHelper.PutMissionParameter(valueToPut);

            // Should unblock previous get
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            var finishedTask = await Task.WhenAny(blockingGetTask, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(blockingGetTask)); // Otherwise we timed out
            var collectionUpdate = GetCollectionUpdate<MissionParameter>(blockingGetTask.Result,
                IncrementalCollectionsName.CurrentMissionParameters);
            Assert.That(collectionUpdate.UpdatedObjects.Count, Is.EqualTo(1));
            Assert.That(collectionUpdate.UpdatedObjects.First(), Is.EqualTo(valueToPut));
        }

        [Test]
        public async Task Delete()
        {
            var missionControlFolder = GetTestTempFolder();
            await m_ProcessHelper.Start(missionControlFolder);

            var assetId = await PostNewAssetAsync(k_LaunchCatalog1, k_LaunchCatalog1FileLength);
            await SetCurrentLaunchAsset(assetId);

            // Put some values
            MissionParameter valueToPut1 = new(Guid.NewGuid())
            {
                ValueIdentifier = "value to put 1",
                Name = "Value to put 1",
                Type = MissionParameterType.String
            };
            await m_ProcessHelper.PutMissionParameter(valueToPut1);
            MissionParameter valueToPut2 = new(Guid.NewGuid())
            {
                ValueIdentifier = "value to put 2",
                Name = "Value to put 2",
                Type = MissionParameterType.Integer
            };
            await m_ProcessHelper.PutMissionParameter(valueToPut2);

            // Delete a value
            await m_ProcessHelper.DeleteMissionParameter(valueToPut1.Id);

            // Check the values are all as expected
            var parameters = await m_ProcessHelper.GetMissionParameters();
            Assert.That(parameters.Count(), Is.EqualTo(1));
            Assert.That(parameters.First(p => p.Id == valueToPut2.Id), Is.EqualTo(valueToPut2));
        }

        [Test]
        public async Task ClearedWhenChangingAsset()
        {
            var missionControlFolder = GetTestTempFolder();
            await m_ProcessHelper.Start(missionControlFolder);

            var assetId = await PostNewAssetAsync(k_LaunchCatalog1, k_LaunchCatalog1FileLength);
            await SetCurrentLaunchAsset(assetId);

            // Put a value
            MissionParameter valueToPut = new(Guid.NewGuid())
            {
                ValueIdentifier = "parameter name",
                Name = "Parameter's name",
                Type = MissionParameterType.String
            };
            await m_ProcessHelper.PutMissionParameter(valueToPut);

            var parameters = await m_ProcessHelper.GetMissionParameters();
            Assert.That(parameters.Count(), Is.EqualTo(1));

            // Change asset
            var newAssetId = await PostNewAssetAsync(k_LaunchCatalog1, k_LaunchCatalog1FileLength);
            await SetCurrentLaunchAsset(newAssetId);

            parameters = await m_ProcessHelper.GetMissionParameters();
            Assert.That(parameters.Count(), Is.EqualTo(0));
        }

        [Test]
        public async Task ClearedWhenClearingAsset()
        {
            var missionControlFolder = GetTestTempFolder();
            await m_ProcessHelper.Start(missionControlFolder);

            var assetId = await PostNewAssetAsync(k_LaunchCatalog1, k_LaunchCatalog1FileLength);
            await SetCurrentLaunchAsset(assetId);

            // Put a value
            MissionParameter valueToPut = new(Guid.NewGuid())
            {
                ValueIdentifier = "parameter name",
                Name = "Parameter's name",
                Type = MissionParameterType.String
            };
            await m_ProcessHelper.PutMissionParameter(valueToPut);

            var parameters = await m_ProcessHelper.GetMissionParameters();
            Assert.That(parameters.Count(), Is.EqualTo(1));

            // Change asset
            await SetCurrentLaunchAsset(Guid.Empty);

            parameters = await m_ProcessHelper.GetMissionParameters();
            Assert.That(parameters.Count(), Is.EqualTo(0));
        }

        [Test]
        public async Task PreservedWhenChangingSomethingElseThanAsset()
        {
            var missionControlFolder = GetTestTempFolder();
            await m_ProcessHelper.Start(missionControlFolder);

            var assetId = await PostNewAssetAsync(k_LaunchCatalog1, k_LaunchCatalog1FileLength);
            await SetCurrentLaunchAsset(assetId);

            // Put a value
            MissionParameter valueToPut = new(Guid.NewGuid())
            {
                ValueIdentifier = "parameter name",
                Name = "Parameter's name",
                Type = MissionParameterType.String
            };
            await m_ProcessHelper.PutMissionParameter(valueToPut);

            var parameters = await m_ProcessHelper.GetMissionParameters();
            Assert.That(parameters.Count(), Is.EqualTo(1));

            // Change something else than the asset in the launch configuration
            var launchConfiguration = await m_ProcessHelper.GetLaunchConfiguration();
            launchConfiguration.Parameters = new LaunchParameterValue[] { new() { Id = "some.parameter", Value = 42 } };
            launchConfiguration.LaunchComplexes = new LaunchComplexConfiguration[] { new() { Identifier = Guid.NewGuid() } };
            await m_ProcessHelper.PutLaunchConfiguration(launchConfiguration);

            // Check all parameters are still present
            parameters = await m_ProcessHelper.GetMissionParameters();
            Assert.That(parameters.Count(), Is.EqualTo(1));
            Assert.That(parameters.First(), Is.EqualTo(valueToPut));
        }

        async Task<Guid> PostNewAssetAsync(LaunchCatalog.Catalog catalog, Dictionary<string, int> filesLength)
        {
            string assetUrl = await CreateAsset(GetTestTempFolder(), catalog, filesLength);

            // Post
            AssetPost assetPost = new()
            {
                Name = "My new asset",
                Description = "My new asset description",
                Url = assetUrl
            };
            var assetId = await m_ProcessHelper.PostAsset(assetPost);
            Assert.That(assetId, Is.Not.EqualTo(Guid.Empty));

            return assetId;
        }

        async Task SetCurrentLaunchAsset(Guid assetId)
        {
            var currentLaunchConfiguration = await m_ProcessHelper.GetLaunchConfiguration();
            currentLaunchConfiguration.AssetId = assetId;
            await m_ProcessHelper.PutLaunchConfiguration(currentLaunchConfiguration);
        }

        string GetTestTempFolder()
        {
            var folderPath = Path.Combine(Path.GetTempPath(), "MissionParametersEffectiveValuesControllerTests_" +
                Guid.NewGuid().ToString());
            m_TestTempFolders.Add(folderPath);
            return folderPath;
        }

        static readonly LaunchCatalog.Catalog k_LaunchCatalog1 = new()
        {
            Payloads = new[] {
                new LaunchCatalog.Payload()
                {
                    Name = "MyPayload",
                    Files = new []
                    {
                        new LaunchCatalog.PayloadFile() { Path = "myFile" }
                    }
                }
            },
            Launchables = new[] {
                new LaunchCatalog.Launchable()
                {
                    Name = "Cluster Node",
                    Type = "clusterNode",
                    Payloads = new [] { "MyPayload" }
                }
            }
        };
        static readonly Dictionary<string, int> k_LaunchCatalog1FileLength = new() { { "myFile", 42 } };

        MissionControlProcessHelper m_ProcessHelper = new();
        List<string> m_TestTempFolders = new();
    }
}
