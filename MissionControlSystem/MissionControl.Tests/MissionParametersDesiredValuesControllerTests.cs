using System.Diagnostics;
using System.Net;
using System.Text.Json;

using static Unity.ClusterDisplay.MissionControl.MissionControl.Helpers;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    public class MissionParametersDesiredValuesControllerTests
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

            MissionParameterValue valueToPut = new(Guid.NewGuid())
            {
                ValueIdentifier = "test value identifier",
                Value = ParseJsonToElement("[42,28]")
            };
            await m_ProcessHelper.PutDesiredParameterValue(valueToPut, HttpStatusCode.Conflict);
        }

        [Test]
        public async Task PutGet()
        {
            await m_ProcessHelper.Start(GetTestTempFolder());

            var assetId = await PostNewAssetAsync(k_LaunchCatalog1, k_LaunchCatalog1FileLength);
            await SetCurrentLaunchAsset(assetId);

            // Put first
            MissionParameterValue valueToPut1 = new(Guid.NewGuid())
            {
                ValueIdentifier = "test value identifier",
                Value = ParseJsonToElement("[42,28]")
            };
            await m_ProcessHelper.PutDesiredParameterValue(valueToPut1);

            var parameters = await m_ProcessHelper.GetDesiredParametersValues();
            Assert.That(parameters.Count(), Is.EqualTo(1));
            Assert.That(parameters.First(), Is.EqualTo(valueToPut1));

            var parameter = await m_ProcessHelper.GetDesiredParameterValue(valueToPut1.Id);
            Assert.That(parameter, Is.EqualTo(valueToPut1));

            // Put second
            MissionParameterValue valueToPut2 = new(Guid.NewGuid())
            {
                ValueIdentifier = "another value identifier",
                Value = ParseJsonToElement("[28,42,\"mixed type array\"]")
            };
            await m_ProcessHelper.PutDesiredParameterValue(valueToPut2);

            parameters = await m_ProcessHelper.GetDesiredParametersValues();
            Assert.That(parameters.Count(), Is.EqualTo(2));
            Assert.That(parameters.First(p => p.Id == valueToPut1.Id), Is.EqualTo(valueToPut1));
            Assert.That(parameters.First(p => p.Id == valueToPut2.Id), Is.EqualTo(valueToPut2));

            parameter = await m_ProcessHelper.GetDesiredParameterValue(valueToPut1.Id);
            Assert.That(parameter, Is.EqualTo(valueToPut1));
            parameter = await m_ProcessHelper.GetDesiredParameterValue(valueToPut2.Id);
            Assert.That(parameter, Is.EqualTo(valueToPut2));

            // While at it, try a get with an invalid id
            Assert.That(await m_ProcessHelper.GetDesiredParameterValueStatusCode(Guid.NewGuid()),
                Is.EqualTo(HttpStatusCode.NotFound));
        }

        [Test]
        public async Task DetectValueIdentifierCollisions()
        {
            await m_ProcessHelper.Start(GetTestTempFolder());

            var assetId = await PostNewAssetAsync(k_LaunchCatalog1, k_LaunchCatalog1FileLength);
            await SetCurrentLaunchAsset(assetId);

            // Put first
            MissionParameterValue valueToPut1 = new(Guid.NewGuid())
            {
                ValueIdentifier = "valueIdentifier",
                Value = ParseJsonToElement("42")
            };
            await m_ProcessHelper.PutDesiredParameterValue(valueToPut1);

            // Put the same value identifier but on another MissionParameterValue (different id), should fail.
            MissionParameterValue valueToPut2 = new(Guid.NewGuid())
            {
                ValueIdentifier = "valueIdentifier",
                Value = ParseJsonToElement("28")
            };
            await m_ProcessHelper.PutDesiredParameterValue(valueToPut2, HttpStatusCode.BadRequest);

            // However we should be able to update valueToPut1 to another value
            valueToPut1.Value = ParseJsonToElement("\"Fourty two\"");
            await m_ProcessHelper.PutDesiredParameterValue(valueToPut1);

            // And changing value identifier of valueToPut1 should now allow us to put valueToPut2
            valueToPut1.ValueIdentifier = "valueIdentifier to avoid conflicts";
            await m_ProcessHelper.PutDesiredParameterValue(valueToPut1);
            await m_ProcessHelper.PutDesiredParameterValue(valueToPut2);

            var parameters = await m_ProcessHelper.GetDesiredParametersValues();
            Assert.That(parameters.Count(), Is.EqualTo(2));
            Assert.That(parameters.First(p => p.Id == valueToPut1.Id), Is.EqualTo(valueToPut1));
            Assert.That(parameters.First(p => p.Id == valueToPut2.Id), Is.EqualTo(valueToPut2));
        }

        [Test]
        public async Task PutIdenticalDoesGenerateNotifications()
        {
            await m_ProcessHelper.Start(GetTestTempFolder());

            var assetId = await PostNewAssetAsync(k_LaunchCatalog1, k_LaunchCatalog1FileLength);
            await SetCurrentLaunchAsset(assetId);

            var blockingGetTask = m_ProcessHelper.GetIncrementalCollectionsUpdate(new List<(string, ulong)> {
                (IncrementalCollectionsName.CurrentMissionParametersDesiredValues, 1) });
            await Task.Delay(100); // So that it has the time to complete if blocking would fail
            Assert.That(blockingGetTask.IsCompleted, Is.False);

            // Put first
            MissionParameterValue valueToPut = new(Guid.NewGuid())
            {
                ValueIdentifier = "valueIdentifier",
                Value = ParseJsonToElement("42")
            };
            await m_ProcessHelper.PutDesiredParameterValue(valueToPut);

            // Should unblock previous get
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            var finishedTask = await Task.WhenAny(blockingGetTask, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(blockingGetTask)); // Otherwise we timed out
            var collectionUpdate = GetCollectionUpdate<MissionParameterValue>(blockingGetTask.Result,
                IncrementalCollectionsName.CurrentMissionParametersDesiredValues);
            Assert.That(collectionUpdate.UpdatedObjects.Count, Is.EqualTo(1));

            blockingGetTask = m_ProcessHelper.GetIncrementalCollectionsUpdate(new List<(string, ulong)> {
                (IncrementalCollectionsName.CurrentMissionParametersDesiredValues, collectionUpdate.NextUpdate) });
            await Task.Delay(100); // So that it has the time to complete if blocking would fail
            Assert.That(blockingGetTask.IsCompleted, Is.False);

            // Repeat put
            await m_ProcessHelper.PutDesiredParameterValue(valueToPut);

            // Blocking get should still be blocked
            await Task.Delay(100); // So that it has the time to complete if blocking would fail
            Assert.That(blockingGetTask.IsCompleted, Is.False);

            // Now do a real change
            valueToPut.Value = ParseJsonToElement("28");
            await m_ProcessHelper.PutDesiredParameterValue(valueToPut);

            timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            finishedTask = await Task.WhenAny(blockingGetTask, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(blockingGetTask)); // Otherwise we timed out
            collectionUpdate = GetCollectionUpdate<MissionParameterValue>(blockingGetTask.Result,
                IncrementalCollectionsName.CurrentMissionParametersDesiredValues);
            Assert.That(collectionUpdate.UpdatedObjects.Count, Is.EqualTo(1));
            Assert.That(collectionUpdate.UpdatedObjects.First(), Is.EqualTo(valueToPut));
        }

        [Test]
        public async Task RefusesEmptyValueIdentifier()
        {
            await m_ProcessHelper.Start(GetTestTempFolder());

            var assetId = await PostNewAssetAsync(k_LaunchCatalog1, k_LaunchCatalog1FileLength);
            await SetCurrentLaunchAsset(assetId);

            // Put empty, should fail
            MissionParameterValue valueToPut = new(Guid.NewGuid())
            {
                Value = ParseJsonToElement("42")
            };
            await m_ProcessHelper.PutDesiredParameterValue(valueToPut, HttpStatusCode.BadRequest);

            // Sanity check, put not empty should work
            valueToPut.ValueIdentifier = "Not empty";
            await m_ProcessHelper.PutDesiredParameterValue(valueToPut);
        }

        [Test]
        public async Task SaveAndLoad()
        {
            var missionControlFolder = GetTestTempFolder();
            await m_ProcessHelper.Start(missionControlFolder);

            var assetId = await PostNewAssetAsync(k_LaunchCatalog1, k_LaunchCatalog1FileLength);
            await SetCurrentLaunchAsset(assetId);

            // Put some values
            MissionParameterValue valueToPut1 = new(Guid.NewGuid())
            {
                ValueIdentifier = "test value identifier",
                Value = ParseJsonToElement("[42,28]")
            };
            await m_ProcessHelper.PutDesiredParameterValue(valueToPut1);
            MissionParameterValue valueToPut2 = new(Guid.NewGuid())
            {
                ValueIdentifier = "another value identifier",
                Value = ParseJsonToElement("[28,42,\"mixed type array\"]")
            };
            await m_ProcessHelper.PutDesiredParameterValue(valueToPut2);

            // Wait until values gets saved (they are saved every few seconds)
            var saveFile = Path.Combine(m_ProcessHelper.ConfigPath, "currentMission",
                "desiredMissionParametersValues.json");
            var waitStartTime = Stopwatch.StartNew();
            while (waitStartTime.Elapsed < TimeSpan.FromSeconds(15))
            {
                try
                {
                    await using var fileStream = File.OpenRead(saveFile);
                    var savedValues = JsonSerializer.Deserialize<MissionParameterValue[]>(fileStream,
                        Json.SerializerOptions);
                    if (savedValues != null && savedValues.Length != 2)
                    {
                        // Not enough values yet, wait a little bit and try again
                        await Task.Delay(100);
                        continue;
                    }
                    break;
                }
                catch(Exception)
                {
                    // Do nothing, this is normal, try again shortly
                    await Task.Delay(100);
                }
            }

            // Ok, we can stop the process
            m_ProcessHelper.Stop();

            // Restart from the same folder
            await m_ProcessHelper.Start(missionControlFolder);

            // Check the values have been loaded back
            var parameters = await m_ProcessHelper.GetDesiredParametersValues();
            Assert.That(parameters.Count(), Is.EqualTo(2));
            Assert.That(parameters.First(p => p.Id == valueToPut1.Id), Is.EqualTo(valueToPut1));
            Assert.That(parameters.First(p => p.Id == valueToPut2.Id), Is.EqualTo(valueToPut2));

            // Also test everything was setup to detect value identifier conflicts
            MissionParameterValue valueToPut3 = new(Guid.NewGuid())
            {
                ValueIdentifier = "test value identifier",
                Value = ParseJsonToElement("[1,2,3,4]")
            };
            await m_ProcessHelper.PutDesiredParameterValue(valueToPut3, HttpStatusCode.BadRequest);
        }

        [Test]
        public async Task Delete()
        {
            var missionControlFolder = GetTestTempFolder();
            await m_ProcessHelper.Start(missionControlFolder);

            var assetId = await PostNewAssetAsync(k_LaunchCatalog1, k_LaunchCatalog1FileLength);
            await SetCurrentLaunchAsset(assetId);

            // Put some values
            MissionParameterValue valueToPut1 = new(Guid.NewGuid())
            {
                ValueIdentifier = "a value identifier",
                Value = ParseJsonToElement("1")
            };
            await m_ProcessHelper.PutDesiredParameterValue(valueToPut1);
            MissionParameterValue valueToPut2 = new(Guid.NewGuid())
            {
                ValueIdentifier = "another value identifier",
                Value = ParseJsonToElement("2")
            };
            await m_ProcessHelper.PutDesiredParameterValue(valueToPut2);

            // Delete a value
            await m_ProcessHelper.DeleteDesiredParameterValue(valueToPut1.Id);

            // Check the values are all as expected
            var parameters = await m_ProcessHelper.GetDesiredParametersValues();
            Assert.That(parameters.Count(), Is.EqualTo(1));
            Assert.That(parameters.First(p => p.Id == valueToPut2.Id), Is.EqualTo(valueToPut2));

            // We should be able to add something back with the same value identifier.
            MissionParameterValue valueToPut3 = new(Guid.NewGuid())
            {
                ValueIdentifier = "a value identifier",
                Value = ParseJsonToElement("3")
            };
            await m_ProcessHelper.PutDesiredParameterValue(valueToPut3);

            // Check the values are all as expected
            parameters = await m_ProcessHelper.GetDesiredParametersValues();
            Assert.That(parameters.Count(), Is.EqualTo(2));
            Assert.That(parameters.First(p => p.Id == valueToPut2.Id), Is.EqualTo(valueToPut2));
            Assert.That(parameters.First(p => p.Id == valueToPut3.Id), Is.EqualTo(valueToPut3));
        }

        [Test]
        public async Task ClearedWhenChangingAsset()
        {
            var missionControlFolder = GetTestTempFolder();
            await m_ProcessHelper.Start(missionControlFolder);

            var assetId = await PostNewAssetAsync(k_LaunchCatalog1, k_LaunchCatalog1FileLength);
            await SetCurrentLaunchAsset(assetId);

            // Put some values
            MissionParameterValue valueToPut1 = new(Guid.NewGuid())
            {
                ValueIdentifier = "a value identifier",
                Value = ParseJsonToElement("1")
            };
            await m_ProcessHelper.PutDesiredParameterValue(valueToPut1);
            MissionParameterValue valueToPut2 = new(Guid.NewGuid())
            {
                ValueIdentifier = "another value identifier",
                Value = ParseJsonToElement("2")
            };
            await m_ProcessHelper.PutDesiredParameterValue(valueToPut2);

            var parameters = await m_ProcessHelper.GetDesiredParametersValues();
            Assert.That(parameters.Count(), Is.EqualTo(2));

            // Change asset
            var newAssetId = await PostNewAssetAsync(k_LaunchCatalog1, k_LaunchCatalog1FileLength);
            await SetCurrentLaunchAsset(newAssetId);

            parameters = await m_ProcessHelper.GetDesiredParametersValues();
            Assert.That(parameters.Count(), Is.EqualTo(0));

            // We should be able to add back values with the same value identifier
            MissionParameterValue valueToPut3 = new(Guid.NewGuid())
            {
                ValueIdentifier = "a value identifier",
                Value = ParseJsonToElement("3")
            };
            await m_ProcessHelper.PutDesiredParameterValue(valueToPut3);

            // Check the values are all as expected
            parameters = await m_ProcessHelper.GetDesiredParametersValues();
            Assert.That(parameters.Count(), Is.EqualTo(1));
            Assert.That(parameters.First(p => p.Id == valueToPut3.Id), Is.EqualTo(valueToPut3));
        }

        [Test]
        public async Task ClearedWhenClearingAsset()
        {
            var missionControlFolder = GetTestTempFolder();
            await m_ProcessHelper.Start(missionControlFolder);

            var assetId = await PostNewAssetAsync(k_LaunchCatalog1, k_LaunchCatalog1FileLength);
            await SetCurrentLaunchAsset(assetId);

            // Put some values
            MissionParameterValue valueToPut1 = new(Guid.NewGuid())
            {
                ValueIdentifier = "a value identifier",
                Value = ParseJsonToElement("1")
            };
            await m_ProcessHelper.PutDesiredParameterValue(valueToPut1);
            MissionParameterValue valueToPut2 = new(Guid.NewGuid())
            {
                ValueIdentifier = "another value identifier",
                Value = ParseJsonToElement("2")
            };
            await m_ProcessHelper.PutDesiredParameterValue(valueToPut2);

            var parameters = await m_ProcessHelper.GetDesiredParametersValues();
            Assert.That(parameters.Count(), Is.EqualTo(2));

            // Change asset
            await SetCurrentLaunchAsset(Guid.Empty);

            parameters = await m_ProcessHelper.GetDesiredParametersValues();
            Assert.That(parameters.Count(), Is.EqualTo(0));
        }

        [Test]
        public async Task PreservedWhenChangingSomethingElseThanAsset()
        {
            var missionControlFolder = GetTestTempFolder();
            await m_ProcessHelper.Start(missionControlFolder);

            var assetId = await PostNewAssetAsync(k_LaunchCatalog1, k_LaunchCatalog1FileLength);
            await SetCurrentLaunchAsset(assetId);

            // Put some values
            MissionParameterValue valueToPut1 = new(Guid.NewGuid())
            {
                ValueIdentifier = "a value identifier",
                Value = ParseJsonToElement("1")
            };
            await m_ProcessHelper.PutDesiredParameterValue(valueToPut1);
            MissionParameterValue valueToPut2 = new(Guid.NewGuid())
            {
                ValueIdentifier = "another value identifier",
                Value = ParseJsonToElement("2")
            };
            await m_ProcessHelper.PutDesiredParameterValue(valueToPut2);

            var parameters = await m_ProcessHelper.GetDesiredParametersValues();
            Assert.That(parameters.Count(), Is.EqualTo(2));

            // Change something else than the asset in the launch configuration
            var launchConfiguration = await m_ProcessHelper.GetLaunchConfiguration();
            launchConfiguration.Parameters =
                new LaunchParameterValue[] { new() { Id = "some.parameter", Value = 42 } };
            launchConfiguration.LaunchComplexes =
                new LaunchComplexConfiguration[] { new() { Identifier = Guid.NewGuid() } };
            await m_ProcessHelper.PutLaunchConfiguration(launchConfiguration);

            // Check all parameters are still present
            parameters = await m_ProcessHelper.GetDesiredParametersValues();
            Assert.That(parameters.Count(), Is.EqualTo(2));
            Assert.That(parameters.First(p => p.Id == valueToPut1.Id), Is.EqualTo(valueToPut1));
            Assert.That(parameters.First(p => p.Id == valueToPut2.Id), Is.EqualTo(valueToPut2));
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
            var folderPath = Path.Combine(Path.GetTempPath(), "MissionParametersDesiredValuesControllerTests_" +
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
