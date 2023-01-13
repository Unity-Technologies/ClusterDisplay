using System.IO.Compression;
using System.Net;
using System.Text.Json;
using NeoSmart.StreamCompare;

using static Unity.ClusterDisplay.MissionControl.MissionControl.Helpers;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    public class AssetsControllerTests
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

            string assetUrl = await CreateAsset(GetTestTempFolder(), k_LaunchCatalog1, k_LaunchCatalog1FileLength);

            // Post
            AssetPost assetPost = new()
            {
                Name = "My new asset",
                Description = "My new asset description",
                Url = assetUrl
            };
            var assetId = await m_ProcessHelper.PostAsset(assetPost);
            Assert.That(assetId, Is.Not.EqualTo(Guid.Empty));

            // Test it was well added
            await TestCatalog1(assetId, assetPost);

            // Get the array of assets (should contain one)
            var assetsGet = await m_ProcessHelper.GetAssets();
            Assert.That(assetsGet.Count(), Is.EqualTo(1));
        }

        [Test]
        public async Task AddWithMissingFile()
        {
            await m_ProcessHelper.Start(GetTestTempFolder());

            string assetUrl = await CreateAsset(GetTestTempFolder(), k_LaunchCatalog1, k_LaunchCatalog1FileLength);
            File.Delete(Path.Combine(assetUrl, "file2"));

            // Post
            AssetPost assetPost = new()
            {
                Name = "Bad asset",
                Description = "There is a missing file",
                Url = assetUrl
            };
            var statusCode = await m_ProcessHelper.PostAssetWithStatusCode(assetPost);
            Assert.That(statusCode, Is.EqualTo(HttpStatusCode.NotFound));

            var status = await m_ProcessHelper.GetStatus();
            Assert.That(status.StorageFolders.Count(), Is.EqualTo(1));
            Assert.That(status.StorageFolders.First().CurrentSize, Is.Zero);
        }

        [Test]
        public async Task AddWithMissingPayload()
        {
            await m_ProcessHelper.Start(GetTestTempFolder());

            LaunchCatalog.Catalog badCatalog = new()
            {
                Payloads = k_LaunchCatalog1.Payloads,
                Launchables = new[] {
                    new LaunchCatalog.Launchable()
                    {
                        Name = "Cluster Node",
                        Type = "clusterNode",
                        Payloads = new[] { "Payload1", "Payload2", "MissingPayload" }
                    }
                }
            };
            string assetUrl = await CreateAsset(GetTestTempFolder(), badCatalog, k_LaunchCatalog1FileLength);

            // Post
            AssetPost assetPost = new()
            {
                Name = "Bad asset",
                Description = "There is a missing file",
                Url = assetUrl
            };
            var statusCode = await m_ProcessHelper.PostAssetWithStatusCode(assetPost);
            Assert.That(statusCode, Is.EqualTo(HttpStatusCode.BadRequest));

            var status = await m_ProcessHelper.GetStatus();
            Assert.That(status.StorageFolders.Count(), Is.EqualTo(1));
            Assert.That(status.StorageFolders.First().CurrentSize, Is.Zero);
        }

        [Test]
        public async Task AddStorageFull()
        {
            await m_ProcessHelper.Start(GetTestTempFolder());
            var config = await m_ProcessHelper.GetConfig();
            config.StorageFolders.First().MaximumSize = 1024;
            await m_ProcessHelper.PutConfig(config);

            Dictionary<string, int> longerFileLengths = new();
            foreach (var pair in k_LaunchCatalog1FileLength)
            {
                longerFileLengths[pair.Key] = pair.Value * 100;
            }
            string assetUrl = await CreateAsset(GetTestTempFolder(), k_LaunchCatalog1, longerFileLengths);

            // Post
            AssetPost assetPost = new()
            {
                Name = "Bad asset",
                Description = "There is a missing file",
                Url = assetUrl
            };
            var statusCode = await m_ProcessHelper.PostAssetWithStatusCode(assetPost);
            Assert.That(statusCode, Is.EqualTo(HttpStatusCode.InsufficientStorage));

            var status = await m_ProcessHelper.GetStatus();
            Assert.That(status.StorageFolders.Count(), Is.EqualTo(1));
            Assert.That(status.StorageFolders.First().CurrentSize, Is.Zero);
        }

        [Test]
        public async Task AddWithBadChecksum()
        {
            await m_ProcessHelper.Start(GetTestTempFolder());

            string assetUrl = await CreateAsset(GetTestTempFolder(), k_LaunchCatalog1, k_LaunchCatalog1FileLength);
            var launchCatalogPath = Path.Combine(assetUrl, "LaunchCatalog.json");
            LaunchCatalog.Catalog? hackedCatalog;
            await using (var readStream = File.OpenRead(launchCatalogPath))
            {
                hackedCatalog = JsonSerializer.Deserialize<LaunchCatalog.Catalog>(readStream, Json.SerializerOptions);
            }
            Assert.That(hackedCatalog, Is.Not.Null);
            var lastFileMd5 = hackedCatalog!.Payloads.Last().Files.Last().Md5;
            hackedCatalog.Payloads.Last().Files.Last().Md5 = lastFileMd5.Substring(lastFileMd5.Length / 2, lastFileMd5.Length / 2) +
                lastFileMd5.Substring(0, lastFileMd5.Length / 2);
            await using (var writeStream = File.OpenWrite(launchCatalogPath))
            {
                await JsonSerializer.SerializeAsync(writeStream, hackedCatalog, Json.SerializerOptions);
            }

            // Post
            AssetPost assetPost = new()
            {
                Name = "Bad asset",
                Description = "There a file in the catalog with a bad md5 checksum",
                Url = assetUrl
            };
            var statusCode = await m_ProcessHelper.PostAssetWithStatusCode(assetPost);
            Assert.That(statusCode, Is.EqualTo(HttpStatusCode.BadRequest));

            var status = await m_ProcessHelper.GetStatus();
            Assert.That(status.StorageFolders.Count(), Is.EqualTo(1));
            Assert.That(status.StorageFolders.First().CurrentSize, Is.Zero);
        }

        [Test]
        public async Task Delete()
        {
            await m_ProcessHelper.Start(GetTestTempFolder());

            string assetUrl = await CreateAsset(GetTestTempFolder(), k_LaunchCatalog1, k_LaunchCatalog1FileLength);

            // Post
            AssetPost assetPost = new()
            {
                Name = "To be deleted",
                Description = "My new asset that is to be deleted",
                Url = assetUrl
            };
            var assetId = await m_ProcessHelper.PostAsset(assetPost);
            Assert.That(assetId, Is.Not.EqualTo(Guid.Empty));

            // Validate status was updated
            var status = await m_ProcessHelper.GetStatus();
            Assert.That(status.StorageFolders.Count(), Is.EqualTo(1));
            Assert.That(status.StorageFolders.First().CurrentSize, Is.GreaterThan(0));

            // Delete
            await m_ProcessHelper.DeleteAsset(assetId);

            // Validate status was updated
            status = await m_ProcessHelper.GetStatus();
            Assert.That(status.StorageFolders.Count(), Is.EqualTo(1));
            Assert.That(status.StorageFolders.First().CurrentSize, Is.Zero);
        }

        [Test]
        public async Task DeleteWrongId()
        {
            await m_ProcessHelper.Start(GetTestTempFolder());

            string assetUrl = await CreateAsset(GetTestTempFolder(), k_LaunchCatalog1, k_LaunchCatalog1FileLength);

            // Post
            AssetPost assetPost = new()
            {
                Name = "To be deleted",
                Description = "My new asset that is to be deleted",
                Url = assetUrl
            };
            var assetId = await m_ProcessHelper.PostAsset(assetPost);
            Assert.That(assetId, Is.Not.EqualTo(Guid.Empty));

            // Validate status was updated
            var status = await m_ProcessHelper.GetStatus();
            Assert.That(status.StorageFolders.Count(), Is.EqualTo(1));
            Assert.That(status.StorageFolders.First().CurrentSize, Is.GreaterThan(0));

            // Delete
            Assert.That(await m_ProcessHelper.DeleteAssetWithStatusCode(Guid.NewGuid()), Is.EqualTo(HttpStatusCode.NotFound));

            // Validate the asset still present
            status = await m_ProcessHelper.GetStatus();
            Assert.That(status.StorageFolders.Count(), Is.EqualTo(1));
            Assert.That(status.StorageFolders.First().CurrentSize, Is.GreaterThan(0));
        }

        [Test]
        public async Task DeleteReferencedAsset()
        {
            string configPath = GetTestTempFolder();
            await m_ProcessHelper.Start(configPath);

            string assetUrl = await CreateAsset(GetTestTempFolder(), k_LaunchCatalog1, k_LaunchCatalog1FileLength);

            // Create an asset
            AssetPost assetPost = new()
            {
                Name = "Test asset",
                Url = assetUrl
            };
            var assetId = await m_ProcessHelper.PostAsset(assetPost);
            Assert.That(assetId, Is.Not.EqualTo(Guid.Empty));

            // Set the current mission's launch configuration using that asset
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
            await m_ProcessHelper.PutLaunchConfiguration(launchConfigPut);

            // Save it
            SaveMissionCommand saveCommand = new()
            {
                Identifier = Guid.NewGuid(),
                Description = new() { Name = "Mission name" }
            };
            await m_ProcessHelper.PostCommand(saveCommand);

            // Clear the current launch configuration
            await m_ProcessHelper.PutLaunchConfiguration(new());

            // Delete, should fail because the asset is used by the saved mission
            var deleteResult = await m_ProcessHelper.DeleteAssetWithStatusCode(assetId);
            Assert.That(deleteResult, Is.EqualTo(HttpStatusCode.Conflict));

            // Delete saved mission
            await m_ProcessHelper.DeleteSavedMission(saveCommand.Identifier);
            await m_ProcessHelper.DeleteAsset(assetId);
        }

        [Test]
        public async Task DeleteWaitForFileBlobInUse()
        {
            await m_ProcessHelper.Start(GetTestTempFolder());

            string assetUrl = await CreateAsset(GetTestTempFolder(), k_LaunchCatalog2, k_LaunchCatalog2FileLength);

            // Post
            AssetPost assetPost = new()
            {
                Name = "Asset with big file",
                Description = "My asset with a big file that we can block the download of",
                Url = assetUrl
            };
            var assetId = await m_ProcessHelper.PostAsset(assetPost);
            Assert.That(assetId, Is.Not.EqualTo(Guid.Empty));

            // Test everything was added as expected
            await TestCatalog2(assetId, assetPost);

            // Get the file blob id
            var assetGet = await m_ProcessHelper.GetAsset(assetId);
            Assert.That(assetGet.Launchables.Count(), Is.EqualTo(1));
            var launchable = assetGet.Launchables.First();
            Assert.That(launchable.Payloads.Count(), Is.EqualTo(1));
            var payloadId = launchable.Payloads.First();
            var payload = await m_ProcessHelper.GetPayload(payloadId);
            Assert.That(payload.Files.Count(), Is.EqualTo(1));
            var fileBlobId = payload.Files.First().FileBlob;

            // Get the file blob, but don't read it's content yet to keep it locked.
            Task deleteTask;
            using (var httpResponse = await m_ProcessHelper.HttpClient.GetAsync($"fileBlobs/{fileBlobId}",
                HttpCompletionOption.ResponseHeadersRead))
            await using (await httpResponse.Content.ReadAsStreamAsync())
            {
                // Start a delete of the asset
                deleteTask = m_ProcessHelper.DeleteAsset(assetId);
                await Task.Delay(100); // So that it has the time to complete if blocking would fail
                Assert.That(deleteTask.IsCompleted, Is.False);

                // Exiting the using should dispose of the stream and the response, unlocking the file blob in mission
                // control allowing delete of the asset to proceed
            }

            // Delete
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            var finishedTask = await Task.WhenAny(deleteTask, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(deleteTask)); // Otherwise we timed out

            // Validate everything was cleared
            var status = await m_ProcessHelper.GetStatus();
            Assert.That(status.StorageFolders.Count(), Is.EqualTo(1));
            Assert.That(status.StorageFolders.First().CurrentSize, Is.Zero);
            Assert.That(status.StorageFolders.First().ZombiesSize, Is.Zero);
        }

        [Test]
        public async Task DeleteWaitForLaunchConfigurationBeingSet()
        {
            string configPath = GetTestTempFolder();
            await m_ProcessHelper.Start(configPath);

            string assetUrl = await CreateAsset(GetTestTempFolder(), k_LaunchCatalog1, k_LaunchCatalog1FileLength);

            // Create an asset
            AssetPost assetPost = new()
            {
                Name = "Test asset",
                Url = assetUrl
            };
            var assetId = await m_ProcessHelper.PostAsset(assetPost);
            Assert.That(assetId, Is.Not.EqualTo(Guid.Empty));

            // Ask for setting the current mission's launch configuration to stall so that we can test synchronization
            // is working fine.
            string stallSetLaunchConfigurationPath = Path.Combine(configPath, "stall_currentMissionLaunchConfiguration");
            await File.WriteAllTextAsync(stallSetLaunchConfigurationPath, "Stall currentMission/launchConfiguration");
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
            var putConfigurationTask = m_ProcessHelper.PutLaunchConfiguration(launchConfigPut);
            await Task.Delay(100);
            Assert.That(putConfigurationTask.IsCompleted, Is.False);

            // Delete, should stall waiting for setting launch configuration
            var deleteTask = m_ProcessHelper.DeleteAssetWithStatusCode(assetId);
            await Task.Delay(100);
            Assert.That(deleteTask.IsCompleted, Is.False);

            // Unblock everything
            File.Delete(stallSetLaunchConfigurationPath);

            // Wait for tasks to finish
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            var finishedTask = await Task.WhenAny(putConfigurationTask, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(putConfigurationTask)); // Otherwise we timed out
            finishedTask = await Task.WhenAny(deleteTask, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(deleteTask)); // Otherwise we timed out

            // Delete should have failed because setting the asset started to use it
            Assert.That(deleteTask.Result, Is.EqualTo(HttpStatusCode.Conflict));

            // However it should work if we clear the current configuration
            launchConfigPut.AssetId = Guid.Empty;
            await m_ProcessHelper.PutLaunchConfiguration(launchConfigPut);
            await m_ProcessHelper.DeleteAsset(assetId);
        }

        [Test]
        public async Task IncrementalCollection()
        {
            await m_ProcessHelper.Start(GetTestTempFolder());

            string assetUrl = await CreateAsset(GetTestTempFolder(), k_LaunchCatalog1, k_LaunchCatalog1FileLength);

            // Setup initial get, should block since there is never been anything in the asset collection yet
            var blockingGetTask = m_ProcessHelper.GetIncrementalCollectionsUpdate(new List<(string, ulong)> {
                (IncrementalCollectionsName.Assets, 1 ) });
            await Task.Delay(100); // So that it has the time to complete if blocking would fail
            Assert.That(blockingGetTask.IsCompleted, Is.False);

            // Post
            AssetPost assetPost = new()
            {
                Name = "My new asset",
                Description = "My new asset description",
                Url = assetUrl
            };
            var assetId = await m_ProcessHelper.PostAsset(assetPost);
            Assert.That(assetId, Is.Not.EqualTo(Guid.Empty));

            // Should unblock the incremental collection update
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            var finishedTask = await Task.WhenAny(blockingGetTask, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(blockingGetTask)); // Otherwise we timed out
            var collectionUpdate = GetCollectionUpdate<Asset>(blockingGetTask.Result, IncrementalCollectionsName.Assets);
            Assert.That(collectionUpdate.UpdatedObjects.Count, Is.EqualTo(1));
            Assert.That(collectionUpdate.RemovedObjects, Is.Empty);
            Assert.That(collectionUpdate.NextUpdate, Is.EqualTo(2));

            // Start another get that should get stuck until we perform the delete
            blockingGetTask = m_ProcessHelper.GetIncrementalCollectionsUpdate(new List<(string, ulong)> {
                (IncrementalCollectionsName.Assets, collectionUpdate.NextUpdate ) });
            await Task.Delay(100); // So that it has the time to complete if blocking would fail
            Assert.That(blockingGetTask.IsCompleted, Is.False);

            // Delete
            await m_ProcessHelper.DeleteAsset(assetId);

            // Should unblock the incremental collection update
            timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            finishedTask = await Task.WhenAny(blockingGetTask, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(blockingGetTask)); // Otherwise we timed out
            collectionUpdate = GetCollectionUpdate<Asset>(blockingGetTask.Result, IncrementalCollectionsName.Assets);
            Assert.That(collectionUpdate.UpdatedObjects, Is.Empty);
            Assert.That(collectionUpdate.RemovedObjects.Count, Is.EqualTo(1));
            Assert.That(collectionUpdate.NextUpdate, Is.EqualTo(3));
        }

        [Test]
        public async Task LoadSave()
        {
            string missionControlFolder = GetTestTempFolder();
            await m_ProcessHelper.Start(missionControlFolder);

            // Add first asset
            string asset1Url = await CreateAsset(GetTestTempFolder(), k_LaunchCatalog1, k_LaunchCatalog1FileLength);
            AssetPost asset1Post = new()
            {
                Name = "Asset 1",
                Description = "Asset 1 from k_LaunchCatalog1",
                Url = asset1Url
            };
            var asset1Id = await m_ProcessHelper.PostAsset(asset1Post);
            Assert.That(asset1Id, Is.Not.EqualTo(Guid.Empty));

            // Add second asset
            string asset2Url = await CreateAsset(GetTestTempFolder(), k_LaunchCatalog2, k_LaunchCatalog2FileLength);
            AssetPost asset2Post = new()
            {
                Name = "Asset 2",
                Description = "Asset 2 from k_LaunchCatalog2",
                Url = asset2Url
            };
            var asset2Id = await m_ProcessHelper.PostAsset(asset2Post);
            Assert.That(asset2Id, Is.Not.EqualTo(Guid.Empty));

            // Check everything ok before shutdown
            await TestCatalog1(asset1Id, asset1Post);
            await TestCatalog2(asset2Id, asset2Post);

            // Restart MissionControl
            m_ProcessHelper.Stop();
            await m_ProcessHelper.Start(missionControlFolder);

            // Check everything appear to be loaded back
            await TestCatalog1(asset1Id, asset1Post);
            await TestCatalog2(asset2Id, asset2Post);

            // Get the array of assets (should contain two)
            var assetsGet = await m_ProcessHelper.GetAssets();
            Assert.That(assetsGet.Count(), Is.EqualTo(2));

            // Delete one of the two assets
            await m_ProcessHelper.DeleteAsset(asset1Id);

            // Check everything ok before shutdown
            assetsGet = await m_ProcessHelper.GetAssets();
            Assert.That(assetsGet.Count(), Is.EqualTo(1));
            await TestCatalog2(asset2Id, asset2Post);

            // Restart MissionControl
            m_ProcessHelper.Stop();
            await m_ProcessHelper.Start(missionControlFolder);

            // Nothing should have changed
            assetsGet = await m_ProcessHelper.GetAssets();
            Assert.That(assetsGet.Count(), Is.EqualTo(1));
            await TestCatalog2(asset2Id, asset2Post);
        }

        static readonly LaunchCatalog.Catalog k_LaunchCatalog1 = new()
        {
            Payloads = new[] {
                new LaunchCatalog.Payload()
                {
                    Name = "Payload1",
                    Files = new []
                    {
                        new LaunchCatalog.PayloadFile() { Path = "file1" },
                        new LaunchCatalog.PayloadFile() { Path = "file2" }
                    }
                },
                new LaunchCatalog.Payload()
                {
                    Name = "Payload2",
                    Files = new []
                    {
                        new LaunchCatalog.PayloadFile() { Path = "file2" },
                        new LaunchCatalog.PayloadFile() { Path = "file3" }
                    }
                }
            },
            Launchables = new[] {
                new LaunchCatalog.Launchable()
                {
                    Name = "Cluster Node",
                    Type = "clusterNode",
                    Payloads = new [] { "Payload1", "Payload2" }
                }
            }
        };
        static readonly Dictionary<string, int> k_LaunchCatalog1FileLength =
            new() { { "file1", 42 }, { "file2", 28 }, { "file3", 24 } };

        static readonly LaunchCatalog.Catalog k_LaunchCatalog2 = new()
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
        static readonly Dictionary<string, int> k_LaunchCatalog2FileLength =
            new() { { "file", 8 * 1024 * 1024 } };

        async Task TestCatalog1(Guid assetId, AssetPost assetPost)
        {
            var assetGet = await m_ProcessHelper.GetAsset(assetId);

            // Check it is as expected
            Assert.That(assetGet.Id, Is.EqualTo(assetId));
            Assert.That(assetGet.Name, Is.EqualTo(assetPost.Name));
            Assert.That(assetGet.Description, Is.EqualTo(assetPost.Description));
            Assert.That(assetGet.Launchables.Count(), Is.EqualTo(1));
            var launchable = assetGet.Launchables.First();
            Assert.That(launchable.Name, Is.EqualTo("Cluster Node"));
            Assert.That(launchable.Type, Is.EqualTo("clusterNode"));
            Assert.That(launchable.Payloads.Count, Is.EqualTo(2));

            // Validate status was updated
            var status = await m_ProcessHelper.GetStatus();
            Assert.That(status.StorageFolders.Count(), Is.EqualTo(1));
            Assert.That(status.StorageFolders.First().CurrentSize, Is.GreaterThan(0));

            // Get the payloads
            var payload1 = await m_ProcessHelper.GetPayload(launchable.Payloads.ElementAt(0));
            Assert.That(payload1.Files.Count(), Is.EqualTo(2));
            Guid file1Id = payload1.Files.ElementAt(0).FileBlob;
            TestPayloadFile(payload1.Files.ElementAt(0), "file1", 42);
            Guid file2Id = payload1.Files.ElementAt(1).FileBlob;
            TestPayloadFile(payload1.Files.ElementAt(1), "file2", 28);

            var payload2 = await m_ProcessHelper.GetPayload(launchable.Payloads.ElementAt(1));
            Assert.That(payload2.Files.Count(), Is.EqualTo(2));
            Assert.That(payload2.Files.ElementAt(0).FileBlob, Is.EqualTo(file2Id));
            TestPayloadFile(payload2.Files.ElementAt(0), "file2", 28);
            Guid file3Id = payload2.Files.ElementAt(1).FileBlob;
            TestPayloadFile(payload2.Files.ElementAt(1), "file3", 24);

            // Test file blobs
            await TestFileBlob(file1Id, Path.Combine(assetPost.Url, "file1"));
            await TestFileBlob(file2Id, Path.Combine(assetPost.Url, "file2"));
            await TestFileBlob(file3Id, Path.Combine(assetPost.Url, "file3"));
        }

        async Task TestCatalog2(Guid assetId, AssetPost assetPost)
        {
            var assetGet = await m_ProcessHelper.GetAsset(assetId);

            // Check it is as expected
            Assert.That(assetGet.Id, Is.EqualTo(assetId));
            Assert.That(assetGet.Name, Is.EqualTo(assetPost.Name));
            Assert.That(assetGet.Description, Is.EqualTo(assetPost.Description));
            Assert.That(assetGet.Launchables.Count(), Is.EqualTo(1));
            var launchable = assetGet.Launchables.First();
            Assert.That(launchable.Name, Is.EqualTo("Cluster Node"));
            Assert.That(launchable.Type, Is.EqualTo("clusterNode"));
            Assert.That(launchable.Payloads.Count, Is.EqualTo(1));

            // Validate status was updated
            var status = await m_ProcessHelper.GetStatus();
            Assert.That(status.StorageFolders.Count(), Is.EqualTo(1));
            Assert.That(status.StorageFolders.First().CurrentSize, Is.GreaterThan(0));

            // Get the payload
            var payload = await m_ProcessHelper.GetPayload(launchable.Payloads.ElementAt(0));
            Assert.That(payload.Files.Count(), Is.EqualTo(1));
            Guid fileId = payload.Files.ElementAt(0).FileBlob;
            TestPayloadFile(payload.Files.ElementAt(0), "file", 8 * 1024 * 1024);

            // Test file blobs
            await TestFileBlob(fileId, Path.Combine(assetPost.Url, "file"));
        }

        static void TestPayloadFile(PayloadFile toTest, string path, long size)
        {
            Assert.That(toTest.Path, Is.EqualTo(path));
            // Remark: We do not test compressed size since it is hard to guarantee with randomly generated content.
            //Assert.That(toTest.CompressedSize, Is.EqualTo(compressedSize));
            Assert.That(toTest.Size, Is.EqualTo(size));
        }

        async Task TestFileBlob(Guid fileBlobId, string comparePath)
        {
            await using var compressedStream = await m_ProcessHelper.GetFileBlob(fileBlobId);
            await using GZipStream uncompressedStream = new(compressedStream, CompressionMode.Decompress);

            await using var fileStream = File.OpenRead(comparePath);

            StreamCompare streamCompare = new();
            bool areEqual = await streamCompare.AreEqualAsync(uncompressedStream, fileStream);
            Assert.That(areEqual, Is.True);
        }

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
