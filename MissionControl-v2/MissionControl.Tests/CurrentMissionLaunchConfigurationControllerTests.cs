using System;
using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Unity.ClusterDisplay.MissionControl.LaunchCatalog;
using static Unity.ClusterDisplay.MissionControl.MissionControl.Helpers;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
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

            var objectsUpdate = await m_ProcessHelper.GetObjectsUpdate(
                new[] { (ObservableObjectsName.CurrentMissionLaunchConfiguration, 0ul) });
            var launchConfigUpdate1 = objectsUpdate[ObservableObjectsName.CurrentMissionLaunchConfiguration];
            var objectsUpdateTask = m_ProcessHelper.GetObjectsUpdate(
                new[] { (ObservableObjectsName.CurrentMissionLaunchConfiguration, launchConfigUpdate1.NextUpdate) });
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
            var launchConfigUpdate2 = objectsUpdateTask.Result[ObservableObjectsName.CurrentMissionLaunchConfiguration];
            Assert.That(JsonSerializer.Deserialize<LaunchConfiguration>(launchConfigUpdate2.Updated, Json.SerializerOptions),
                Is.EqualTo(launchConfigPut));

            // Start another blocking get
            objectsUpdateTask = m_ProcessHelper.GetObjectsUpdate(
                new[] { (ObservableObjectsName.CurrentMissionLaunchConfiguration, launchConfigUpdate2.NextUpdate) });

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
            await File.WriteAllTextAsync(stallDeletePath, "Stall delete of asset");

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

        [Test]
        public async Task Capcom()
        {
            var configPath = GetTestTempFolder();
            await m_ProcessHelper.Start(configPath);

            // Create assets
            var assetId = await PostAsset(m_ProcessHelper, GetTestTempFolder(), k_CatalogCapcom1, new(),
                k_CatalogCapcom1FilesContent);

            // Put first configuration
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
            Assert.That(await m_ProcessHelper.GetLaunchConfiguration(), Is.EqualTo(launchConfigPut));

            // Validate prelaunch has executed
            var capcomPath = Path.Combine(configPath, k_CapcomFolder, k_CatalogCapcom1.Launchables.First().Name);
            Assert.That((await File.ReadAllTextAsync(Path.Combine(capcomPath, "pre_launchable1.json"))).Trim(),
                        Is.EqualTo(JsonSerializer.Serialize(k_CatalogCapcom1.Launchables.First().Data, Json.SerializerOptions)));
            Assert.That(DeserializeFile<Config>(Path.Combine(capcomPath, "pre_config1.json")),
                        Is.EqualTo(await m_ProcessHelper.GetConfig()));

            // Validate the capcom process is running
            var capcomProcess = await GetProcessFromPid(Path.Combine(capcomPath, "launch1_pid.txt"));
            Assert.That(capcomProcess.HasExited, Is.False);
            Assert.That((await File.ReadAllTextAsync(Path.Combine(capcomPath, "launchable1.json"))).Trim(),
                        Is.EqualTo(JsonSerializer.Serialize(k_CatalogCapcom1.Launchables.First().Data, Json.SerializerOptions)));
            Assert.That(DeserializeFile<Config>(Path.Combine(capcomPath, "config1.json")),
                        Is.EqualTo(await m_ProcessHelper.GetConfig()));

            // Validate it will stop when setting asset to empty.
            launchConfigPut.AssetId = Guid.Empty;
            await m_ProcessHelper.PutLaunchConfiguration(launchConfigPut);

            Assert.That(capcomProcess.HasExited, Is.True);
            Assert.That(Directory.Exists(Path.Combine(configPath, k_CapcomFolder)), Is.False);
        }

        [Test]
        public async Task CapcomAtShutdownAndLoad()
        {
            var configPath = GetTestTempFolder();
            await m_ProcessHelper.Start(configPath);

            // Create assets
            var assetId = await PostAsset(m_ProcessHelper, GetTestTempFolder(), k_CatalogCapcom1, new(),
                k_CatalogCapcom1FilesContent);

            // Put first configuration
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
            Assert.That(await m_ProcessHelper.GetLaunchConfiguration(), Is.EqualTo(launchConfigPut));

            // Validate the capcom process is running
            var capcomPath = Path.Combine(configPath, k_CapcomFolder, k_CatalogCapcom1.Launchables.First().Name);
            var launch1PidPath = Path.Combine(capcomPath, "launch1_pid.txt");
            var capcomProcess = await GetProcessFromPid(launch1PidPath);
            Assert.That(capcomProcess.HasExited, Is.False);

            // Stop mission control
            m_ProcessHelper.Stop();

            // Validate stopping mission control stopped the capcom process.
            Assert.That(capcomProcess.HasExited, Is.True);
            Assert.That(Directory.Exists(Path.Combine(configPath, k_CapcomFolder)), Is.False);

            // And load back to see if it is launched back (without having to set the launch configuration)
            await m_ProcessHelper.Start(configPath);
            capcomProcess = await GetProcessFromPid(launch1PidPath);
            Assert.That(capcomProcess.HasExited, Is.False);
        }

        [Test]
        public async Task CapcomChangeAsset()
        {
            var configPath = GetTestTempFolder();
            await m_ProcessHelper.Start(configPath);

            // Create assets
            var asset1Id = await PostAsset(m_ProcessHelper, GetTestTempFolder(), k_CatalogCapcom1, new(),
                k_CatalogCapcom1FilesContent);
            var asset2Id = await PostAsset(m_ProcessHelper, GetTestTempFolder(), k_CatalogCapcom2, new(),
                k_CatalogCapcom2FilesContent);

            // Put first configuration
            LaunchConfiguration launchConfigPut = new()
            {
                AssetId = asset1Id,
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

            // Validate the capcom process is running
            var capcom1Path = Path.Combine(configPath, k_CapcomFolder, k_CatalogCapcom1.Launchables.First().Name);
            var capcomProcess = await GetProcessFromPid(Path.Combine(capcom1Path, "launch1_pid.txt"));
            Assert.That(capcomProcess.HasExited, Is.False);

            // Change asset
            launchConfigPut.AssetId = asset2Id;
            await m_ProcessHelper.PutLaunchConfiguration(launchConfigPut);

            // Previous capcom should be stopped (and deleted)
            Assert.That(capcomProcess.HasExited, Is.True);
            Assert.That(Directory.Exists(capcom1Path), Is.False);

            // And the new one started
            var capcom2Path = Path.Combine(configPath, k_CapcomFolder, k_CatalogCapcom2.Launchables.First().Name);
            capcomProcess = await GetProcessFromPid(Path.Combine(capcom2Path, "launch2_pid.txt"));
            Assert.That(capcomProcess.HasExited, Is.False);
        }

        [Test]
        public async Task FailDeleteOldCapComFolder()
        {
            var configPath = GetTestTempFolder();
            await m_ProcessHelper.Start(configPath);

            // Create assets
            var asset1Id = await PostAsset(m_ProcessHelper, GetTestTempFolder(), k_CatalogCapcom1, new(),
                k_CatalogCapcom1FilesContent);
            var asset2Id = await PostAsset(m_ProcessHelper, GetTestTempFolder(), k_CatalogCapcom2, new(),
                k_CatalogCapcom2FilesContent);

            // Put first configuration
            LaunchConfiguration launchConfigPut = new()
            {
                AssetId = asset1Id,
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

            var capcom1Path = Path.Combine(configPath, k_CapcomFolder, k_CatalogCapcom1.Launchables.First().Name);
            var capcom2Path = Path.Combine(configPath, k_CapcomFolder, k_CatalogCapcom2.Launchables.First().Name);

            // Validate the capcom process is running
            var capcomProcess = await GetProcessFromPid(Path.Combine(capcom1Path, "launch1_pid.txt"));
            Assert.That(capcomProcess.HasExited, Is.False);

            // Make the directory containing the capcom executable impossible to delete by creating a file in it that
            // we keep open.
            await using (File.OpenWrite(Path.Combine(capcom1Path, "lock.txt")))
            {
                // Change asset
                launchConfigPut.AssetId = asset2Id;
                await m_ProcessHelper.PutLaunchConfiguration(launchConfigPut);
                Assert.That(await m_ProcessHelper.GetLaunchConfiguration(), Is.EqualTo(launchConfigPut));

                // Previous capcom should be stopped
                Assert.That(capcomProcess.HasExited, Is.True);

                // But deleting the folder should fail because of lock.txt
                Assert.That(Directory.Exists(capcom1Path), Is.True);

                // And this should have prevented starting the new capcom
                Assert.That(Directory.Exists(capcom2Path), Is.False);
            }

            // But now that lock.txt is not locked anymore everything should work.
            launchConfigPut.LaunchComplexes.First().Identifier = Guid.NewGuid(); // So that something changes
            await m_ProcessHelper.PutLaunchConfiguration(launchConfigPut);
            Assert.That(await m_ProcessHelper.GetLaunchConfiguration(), Is.EqualTo(launchConfigPut));

            Assert.That(Directory.Exists(capcom1Path), Is.False);
            Assert.That(Directory.Exists(capcom2Path), Is.True);
            capcomProcess = await GetProcessFromPid(Path.Combine(capcom2Path, "launch2_pid.txt"));
            Assert.That(capcomProcess.HasExited, Is.False);
        }

        [Test]
        public async Task CapcomMultiple()
        {
            var configPath = GetTestTempFolder();
            await m_ProcessHelper.Start(configPath);

            // Create assets
            var assetId = await PostAsset(m_ProcessHelper, GetTestTempFolder(), k_CatalogCapcom3, new(),
                k_CatalogCapcom3FilesContent);

            // Put first configuration
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
            Assert.That(await m_ProcessHelper.GetLaunchConfiguration(), Is.EqualTo(launchConfigPut));

            var capcom1Path = Path.Combine(configPath, k_CapcomFolder, k_CatalogCapcom3.Launchables.ElementAt(0).Name);
            var capcom2Path = Path.Combine(configPath, k_CapcomFolder, k_CatalogCapcom3.Launchables.ElementAt(1).Name);

            // Validate prelaunch has executed
            Assert.That((await File.ReadAllTextAsync(Path.Combine(capcom1Path, "pre_launchable1.json"))).Trim(),
                        Is.EqualTo(JsonSerializer.Serialize(k_CatalogCapcom3.Launchables.ElementAt(0).Data, Json.SerializerOptions)));
            Assert.That(DeserializeFile<Config>(Path.Combine(capcom1Path, "pre_config1.json")),
                        Is.EqualTo(await m_ProcessHelper.GetConfig()));
            Assert.That((await File.ReadAllTextAsync(Path.Combine(capcom2Path, "pre_launchable2.json"))).Trim(),
                        Is.EqualTo(JsonSerializer.Serialize(k_CatalogCapcom3.Launchables.ElementAt(1).Data, Json.SerializerOptions)));
            Assert.That(DeserializeFile<Config>(Path.Combine(capcom2Path, "pre_config2.json")),
                        Is.EqualTo(await m_ProcessHelper.GetConfig()));

            // Validate the capcom process is running
            var capcom1Process = await GetProcessFromPid(Path.Combine(capcom1Path, "launch1_pid.txt"));
            Assert.That(capcom1Process.HasExited, Is.False);
            Assert.That((await File.ReadAllTextAsync(Path.Combine(capcom1Path, "launchable1.json"))).Trim(),
                        Is.EqualTo(JsonSerializer.Serialize(k_CatalogCapcom3.Launchables.ElementAt(0).Data, Json.SerializerOptions)));
            Assert.That(DeserializeFile<Config>(Path.Combine(capcom1Path, "config1.json")),
                        Is.EqualTo(await m_ProcessHelper.GetConfig()));
            var capcom2Process = await GetProcessFromPid(Path.Combine(capcom2Path, "launch2_pid.txt"));
            Assert.That(capcom2Process.HasExited, Is.False);
            Assert.That((await File.ReadAllTextAsync(Path.Combine(capcom2Path, "launchable2.json"))).Trim(),
                        Is.EqualTo(JsonSerializer.Serialize(k_CatalogCapcom3.Launchables.ElementAt(1).Data, Json.SerializerOptions)));
            Assert.That(DeserializeFile<Config>(Path.Combine(capcom2Path, "config2.json")),
                        Is.EqualTo(await m_ProcessHelper.GetConfig()));

            // Validate it will stop when setting asset to empty.
            launchConfigPut.AssetId = Guid.Empty;
            await m_ProcessHelper.PutLaunchConfiguration(launchConfigPut);

            Assert.That(capcom1Process.HasExited, Is.True);
            Assert.That(capcom2Process.HasExited, Is.True);
            Assert.That(Directory.Exists(Path.Combine(configPath, k_CapcomFolder)), Is.False);
        }

        [Test]
        public async Task CapcomBadPrelaunchDoesNotPreventLaunch()
        {
            var configPath = GetTestTempFolder();
            await m_ProcessHelper.Start(configPath);

            // Create assets
            var badCatalog = JsonSerializer.Deserialize<Catalog>(
                JsonSerializer.Serialize(k_CatalogCapcom3, Json.SerializerOptions), Json.SerializerOptions)!;
            var badFileList = badCatalog.Payloads.First().Files.ToList();
            badFileList.RemoveAt(0);
            badCatalog.Payloads.First().Files = badFileList;
            var assetId = await PostAsset(m_ProcessHelper, GetTestTempFolder(), badCatalog, new(),
                k_CatalogCapcom3FilesContent);

            // Put first configuration
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
            Assert.That(await m_ProcessHelper.GetLaunchConfiguration(), Is.EqualTo(launchConfigPut));

            var capcom1Path = Path.Combine(configPath, k_CapcomFolder, badCatalog.Launchables.ElementAt(0).Name);
            var capcom2Path = Path.Combine(configPath, k_CapcomFolder, badCatalog.Launchables.ElementAt(1).Name);

            // Validate that capcom1 and capcom2 processes are running
            var capcom1Process = await GetProcessFromPid(Path.Combine(capcom1Path, "launch1_pid.txt"));
            Assert.That(capcom1Process.HasExited, Is.False);
            var capcom2Process = await GetProcessFromPid(Path.Combine(capcom2Path, "launch2_pid.txt"));
            Assert.That(capcom2Process.HasExited, Is.False);

            // However only prelaunch2 should have succeeded
            Assert.That(File.Exists(Path.Combine(capcom1Path, "pre_config1.json")), Is.False);
            Assert.That(File.Exists(Path.Combine(capcom2Path, "pre_config2.json")), Is.True);

            // Validate it will still stop everything when changing to an empty asset
            launchConfigPut.AssetId = Guid.Empty;
            await m_ProcessHelper.PutLaunchConfiguration(launchConfigPut);

            Assert.That(capcom1Process.HasExited, Is.True);
            Assert.That(capcom2Process.HasExited, Is.True);
            Assert.That(Directory.Exists(Path.Combine(configPath, k_CapcomFolder)), Is.False);
        }

        [Test]
        public async Task CapcomBadEverythingDoesNotPreventOtherLaunch()
        {
            var configPath = GetTestTempFolder();
            await m_ProcessHelper.Start(configPath);

            // Create assets
            var badCatalog = JsonSerializer.Deserialize<Catalog>(
                JsonSerializer.Serialize(k_CatalogCapcom3, Json.SerializerOptions), Json.SerializerOptions)!;
            badCatalog.Payloads.First().Files = Enumerable.Empty<LaunchCatalog.PayloadFile>();
            var assetId = await PostAsset(m_ProcessHelper, GetTestTempFolder(), badCatalog, new(),
                k_CatalogCapcom3FilesContent);

            // Put first configuration
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
            Assert.That(await m_ProcessHelper.GetLaunchConfiguration(), Is.EqualTo(launchConfigPut));

            var capcom1Path = Path.Combine(configPath, k_CapcomFolder, badCatalog.Launchables.ElementAt(0).Name);
            var capcom2Path = Path.Combine(configPath, k_CapcomFolder, badCatalog.Launchables.ElementAt(1).Name);

            // Validate that capcom2 process is running
            var capcom2Process = await GetProcessFromPid(Path.Combine(capcom2Path, "launch2_pid.txt"));
            Assert.That(capcom2Process.HasExited, Is.False);
            Assert.That(File.Exists(Path.Combine(capcom2Path, "pre_config2.json")), Is.True);

            // However there should be no trace of capcom1 execution
            Assert.That(File.Exists(Path.Combine(capcom1Path, "launch1_pid.txt")), Is.False);
            Assert.That(File.Exists(Path.Combine(capcom1Path, "pre_config1.json")), Is.False);

            // Validate it will still stop everything when changing to an empty asset
            launchConfigPut.AssetId = Guid.Empty;
            await m_ProcessHelper.PutLaunchConfiguration(launchConfigPut);

            Assert.That(capcom2Process.HasExited, Is.True);
            Assert.That(Directory.Exists(Path.Combine(configPath, k_CapcomFolder)), Is.False);
        }

        [Test]
        public async Task CapcomGracefulStop()
        {
            var configPath = GetTestTempFolder();
            await m_ProcessHelper.Start(configPath);

            // Create assets
            var assetId = await PostAsset(m_ProcessHelper, GetTestTempFolder(), k_CatalogCapcom4, new(),
                k_CatalogCapcom4FilesContent);

            // Put first configuration
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
            Assert.That(await m_ProcessHelper.GetLaunchConfiguration(), Is.EqualTo(launchConfigPut));

            // Validate the capcom process is running
            var capcomPath = Path.Combine(configPath, k_CapcomFolder, k_CatalogCapcom4.Launchables.First().Name);
            var launchOverPath = Path.GetFullPath(Path.Combine(capcomPath, "..", "..", "launch4_over.txt"));
            var capcomProcess = await GetProcessFromPid(Path.Combine(capcomPath, "launch4_pid.txt"));
            Assert.That(capcomProcess.HasExited, Is.False);
            Assert.That(File.Exists(launchOverPath), Is.False);

            // Validate it will stop when setting asset to empty.
            launchConfigPut.AssetId = Guid.Empty;
            await m_ProcessHelper.PutLaunchConfiguration(launchConfigPut);

            Assert.That(capcomProcess.HasExited, Is.True);
            Assert.That(Directory.Exists(Path.Combine(configPath, k_CapcomFolder)), Is.False);

            // Validate that it stopped gracefully (not because killed)
            Assert.That(File.Exists(launchOverPath), Is.True);
        }

        static T DeserializeFile<T>(string path)
        {
            using var fileStream = File.OpenRead(path);
            var ret = JsonSerializer.Deserialize<T>(fileStream, Json.SerializerOptions);
            Assert.That(ret, Is.Not.Null);
            return ret!;
        }

        static async Task<Process> GetProcessFromPid(string path)
        {
            var waitLaunched = Stopwatch.StartNew();
            Process? process = null;
            while (waitLaunched.Elapsed < TimeSpan.FromSeconds(15) && process == null)
            {
                try
                {
                    var pidText = await File.ReadAllTextAsync(path);
                    process = Process.GetProcessById(Convert.ToInt32(pidText));
                }
                catch (Exception)
                {
                    // ignored
                }
            }
            Assert.That(process, Is.Not.Null);
            Assert.That(process!.HasExited, Is.False);
            return process;
        }

        string GetTestTempFolder()
        {
            var folderPath = Path.Combine(Path.GetTempPath(), "CurrentMissionLaunchConfigurationControllerTests_" + Guid.NewGuid().ToString());
            m_TestTempFolders.Add(folderPath);
            return folderPath;
        }

        static readonly Catalog k_SimpleLaunchCatalog = new()
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

        static readonly Catalog k_CatalogCapcom1 = new()
        {
            Payloads = new[] {
                new LaunchCatalog.Payload()
                {
                    Name = "Payload",
                    Files = new []
                    {
                        new LaunchCatalog.PayloadFile() { Path = "prelaunch.ps1" },
                        new LaunchCatalog.PayloadFile() { Path = "launch.ps1" }
                    }
                }
            },
            Launchables = new[] {
                new LaunchCatalog.Launchable()
                {
                    Name = "Capcom1",
                    Type = LaunchableBase.CapcomLaunchableType,
                    Payloads = new [] { "Payload" },
                    Data = JsonNode.Parse("{'LaunchData': 42}".Replace('\'', '\"')),
                    PreLaunchPath = "prelaunch.ps1",
                    LaunchPath = "launch.ps1"
                }
            }
        };
        static readonly Dictionary<string, MemoryStream> k_CatalogCapcom1FilesContent = new() {
            { "prelaunch.ps1", MemoryStreamFromString(
                "$env:LAUNCHABLE_DATA       | Out-File \"pre_launchable1.json\" -Encoding ascii\n" +
                "$env:MISSIONCONTROL_CONFIG | Out-File \"pre_config1.json\"     -Encoding ascii  ") },
            { "launch.ps1", MemoryStreamFromString(
                "$env:LAUNCHABLE_DATA       | Out-File \"launchable1.json\" -Encoding ascii\n" +
                "$env:MISSIONCONTROL_CONFIG | Out-File \"config1.json\"     -Encoding ascii\n" +
                "$pid                       | Out-File \"launch1_pid.txt\"                 \n" +
                "while ( $true )                                                           \n" +
                "{                                                                         \n" +
                "    Start-Sleep -Seconds 60                                               \n" +
                "}") }
        };

        static readonly Catalog k_CatalogCapcom2 = new()
        {
            Payloads = new[] {
                new LaunchCatalog.Payload()
                {
                    Name = "Payload",
                    Files = new []
                    {
                        new LaunchCatalog.PayloadFile() { Path = "prelaunch.ps1" },
                        new LaunchCatalog.PayloadFile() { Path = "launch.ps1" }
                    }
                }
            },
            Launchables = new[] {
                new LaunchCatalog.Launchable()
                {
                    Name = "Capcom2",
                    Type = LaunchableBase.CapcomLaunchableType,
                    Payloads = new [] { "Payload" },
                    Data = JsonNode.Parse("{'LaunchData': 28}".Replace('\'', '\"')),
                    PreLaunchPath = "prelaunch.ps1",
                    LaunchPath = "launch.ps1"
                }
            }
        };
        static readonly Dictionary<string, MemoryStream> k_CatalogCapcom2FilesContent = new() {
            { "prelaunch.ps1", MemoryStreamFromString(
                "$env:LAUNCHABLE_DATA       | Out-File \"pre_launchable2.json\" -Encoding ascii\n" +
                "$env:MISSIONCONTROL_CONFIG | Out-File \"pre_config2.json\"     -Encoding ascii  ") },
            { "launch.ps1", MemoryStreamFromString(
                "$env:LAUNCHABLE_DATA       | Out-File \"launchable2.json\" -Encoding ascii\n" +
                "$env:MISSIONCONTROL_CONFIG | Out-File \"config2.json\"     -Encoding ascii\n" +
                "$pid                       | Out-File \"launch2_pid.txt\"                 \n" +
                "while ( $true )                                                           \n" +
                "{                                                                         \n" +
                "    Start-Sleep -Seconds 60                                               \n" +
                "}") }
        };

        static readonly Catalog k_CatalogCapcom3 = new()
        {
            Payloads = new[] {
                new LaunchCatalog.Payload()
                {
                    Name = "Payload1",
                    Files = new []
                    {
                        new LaunchCatalog.PayloadFile() { Path = "prelaunch1.ps1" },
                        new LaunchCatalog.PayloadFile() { Path = "launch1.ps1" }
                    }
                },
                new LaunchCatalog.Payload()
                {
                    Name = "Payload2",
                    Files = new []
                    {
                        new LaunchCatalog.PayloadFile() { Path = "prelaunch2.ps1" },
                        new LaunchCatalog.PayloadFile() { Path = "launch2.ps1" }
                    }
                }
            },
            Launchables = new[] {
                new LaunchCatalog.Launchable()
                {
                    Name = "Capcom1",
                    Type = LaunchableBase.CapcomLaunchableType,
                    Payloads = new [] { "Payload1" },
                    Data = JsonNode.Parse("{'LaunchData': 42}".Replace('\'', '\"')),
                    PreLaunchPath = "prelaunch1.ps1",
                    LaunchPath = "launch1.ps1"
                },
                new LaunchCatalog.Launchable()
                {
                    Name = "Capcom2",
                    Type = LaunchableBase.CapcomLaunchableType,
                    Payloads = new [] { "Payload2" },
                    Data = JsonNode.Parse("{'LaunchData': 28}".Replace('\'', '\"')),
                    PreLaunchPath = "prelaunch2.ps1",
                    LaunchPath = "launch2.ps1"
                }
            }
        };
        static readonly Dictionary<string, MemoryStream> k_CatalogCapcom3FilesContent = new() {
            { "prelaunch1.ps1", MemoryStreamFromString(
                "$env:LAUNCHABLE_DATA       | Out-File \"pre_launchable1.json\" -Encoding ascii\n" +
                "$env:MISSIONCONTROL_CONFIG | Out-File \"pre_config1.json\"     -Encoding ascii  ") },
            { "launch1.ps1", MemoryStreamFromString(
                "$env:LAUNCHABLE_DATA       | Out-File \"launchable1.json\" -Encoding ascii\n" +
                "$env:MISSIONCONTROL_CONFIG | Out-File \"config1.json\"     -Encoding ascii\n" +
                "$pid                       | Out-File \"launch1_pid.txt\"                 \n" +
                "while ( $true )                                                           \n" +
                "{                                                                         \n" +
                "    Start-Sleep -Seconds 60                                               \n" +
                "}") },
            { "prelaunch2.ps1", MemoryStreamFromString(
                "$env:LAUNCHABLE_DATA       | Out-File \"pre_launchable2.json\" -Encoding ascii\n" +
                "$env:MISSIONCONTROL_CONFIG | Out-File \"pre_config2.json\"     -Encoding ascii  ") },
            { "launch2.ps1", MemoryStreamFromString(
                "$env:LAUNCHABLE_DATA       | Out-File \"launchable2.json\" -Encoding ascii\n" +
                "$env:MISSIONCONTROL_CONFIG | Out-File \"config2.json\"     -Encoding ascii\n" +
                "$pid                       | Out-File \"launch2_pid.txt\"                 \n" +
                "while ( $true )                                                           \n" +
                "{                                                                         \n" +
                "    Start-Sleep -Seconds 60                                               \n" +
                "}") }
        };

        static readonly Catalog k_CatalogCapcom4 = new()
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
                    Name = "Capcom1",
                    Type = LaunchableBase.CapcomLaunchableType,
                    Payloads = new [] { "Payload" },
                    Data = JsonNode.Parse("{'LaunchData': 42}".Replace('\'', '\"')),
                    PreLaunchPath = "prelaunch.ps1",
                    LaunchPath = "launch.ps1",
                    LandingTimeSec = 5
                }
            }
        };
        static readonly Dictionary<string, MemoryStream> k_CatalogCapcom4FilesContent = new() {
            { "launch.ps1", MemoryStreamFromString(
                "$missionControlConfig = ConvertFrom-Json -InputObject $env:MISSIONCONTROL_CONFIG \n" +
                "$pid | Out-File \"launch4_pid.txt\"                                              \n" +
                "$keepRunning = $true                                                             \n" +
                "while ($keepRunning)                                                             \n" +
                "{                                                                                \n" +
                "    $uplink = Invoke-RestMethod -Uri \"$($missionControlConfig.localEntry)/api/v1/capcomUplink\"\n" +
                "    $keepRunning = $uplink.isRunning                                             \n" +
                "    if ($keepRunning)                                                            \n" +
                "    {                                                                            \n" +
                "        Start-Sleep -Milliseconds 5                                              \n" +
                "    }                                                                            \n" +
                "}                                                                                \n" +
                "Out-File \"../../launch4_over.txt\" -InputObject \"Over\"") }
        };

        const string k_CapcomFolder = "capcom";

        MissionControlProcessHelper m_ProcessHelper = new();
        List<string> m_TestTempFolders = new();
    }
}
