using System;
using System.Net.NetworkInformation;
using System.Net;
using System.Text.Json;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    public class ConfigControllerTests
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
        public async Task SetStorageFolders()
        {
            await m_ProcessHelper.Start(GetTestTempFolder());

            // Get the status through the blocking call
            var objectsUpdate1 = await m_ProcessHelper.GetObjectsUpdate(new[] { (k_StatusObjectName, 0ul) });
            Assert.That(objectsUpdate1.Count, Is.EqualTo(1));
            var statusUpdate1 = objectsUpdate1[k_StatusObjectName];
            var objectsUpdateTask = m_ProcessHelper.GetObjectsUpdate(
                new[] { (k_StatusObjectName, statusUpdate1.NextUpdate) });
            await Task.Delay(100); // So that objectsUpdateTask can have the time to complete if it wouldn't block
            Assert.That(objectsUpdateTask.IsCompleted, Is.False);

            // Change the config
            var newConfig = await m_ProcessHelper.GetConfig();
            var storageFolder0 =
                new StorageFolderConfig() { Path = GetTestTempFolder(), MaximumSize = 10L * 1024 * 1024 * 1024 };
            var storageFolder1 =
                new StorageFolderConfig() { Path = GetTestTempFolder(), MaximumSize = 20L * 1024 * 1024 * 1024 };
            newConfig.StorageFolders = new[] { storageFolder0, storageFolder1 };

            await m_ProcessHelper.PutConfig(newConfig);

            // Check the update was well received
            var updatedConfig = await m_ProcessHelper.GetConfig();
            Assert.That(newConfig.StorageFolders, Is.EqualTo(updatedConfig.StorageFolders));

            // Check that the status is updated
            var status = await m_ProcessHelper.GetStatus();
            var folderStatus0 = status.StorageFolders.FirstOrDefault(fs => fs.Path == storageFolder0.Path);
            Assert.That(folderStatus0, Is.EqualTo(new StorageFolderStatus() {
                Path = storageFolder0.Path, MaximumSize = storageFolder0.MaximumSize }));
            var folderStatus1 = status.StorageFolders.FirstOrDefault(fs => fs.Path == storageFolder1.Path);
            Assert.That(folderStatus1, Is.EqualTo(new StorageFolderStatus() {
                Path = storageFolder1.Path, MaximumSize = storageFolder1.MaximumSize }));

            // And that the blocking status update got unblocked
            var objectsUpdate2 = await objectsUpdateTask;
            Assert.That(objectsUpdate2.Count, Is.EqualTo(1));
            var statusUpdate2 = objectsUpdate2[k_StatusObjectName];
            Assert.That(statusUpdate2.NextUpdate, Is.GreaterThan(statusUpdate1.NextUpdate));
            Assert.That(statusUpdate2.Updated.Deserialize<Status>(Json.SerializerOptions), Is.EqualTo(status));
        }

        [Test]
        public async Task SetBadConfig()
        {
            await m_ProcessHelper.Start(GetTestTempFolder());

            // Try setting no storage folder at all
            var newConfig = await m_ProcessHelper.GetConfig();
            string originalPath = newConfig.StorageFolders.First().Path;
            newConfig.StorageFolders = new StorageFolderConfig[] { };

            var httpRet = await m_ProcessHelper.PutConfigWithResponse(newConfig);
            Assert.That(httpRet.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            string[] errorMessages = await m_ProcessHelper.GetErrorDetails(httpRet.Content);
            Assert.That(errorMessages.Length, Is.EqualTo(1));
            Assert.That(errorMessages[0].Contains("one storage"), Is.True);

            // Validate config still intact
            var currentConfig = await m_ProcessHelper.GetConfig();
            Assert.That(currentConfig.StorageFolders, Is.Not.Empty);
            var currentStatus = await m_ProcessHelper.GetStatus();
            Assert.That(currentStatus.StorageFolders, Is.Not.Empty);

            // Try adding a valid path and an invalid one (none should be considered because of the invalid one).
            newConfig.StorageFolders = new StorageFolderConfig[] { };
            var storageFolder0 =
                new StorageFolderConfig() { Path = GetTestTempFolder(), MaximumSize = 10L * 1024 * 1024 * 1024 };
            var storageFolder1 =
                new StorageFolderConfig() { Path = "something://this is not a valid path", MaximumSize = 20L * 1024 * 1024 * 1024 };
            newConfig.StorageFolders = new[] { storageFolder0, storageFolder1 };

            httpRet = await m_ProcessHelper.PutConfigWithResponse(newConfig);
            Assert.That(httpRet.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            errorMessages = await m_ProcessHelper.GetErrorDetails(httpRet.Content);
            Assert.That(errorMessages.Length, Is.EqualTo(1));
            Assert.That(errorMessages[0].Contains("Can't access or create"), Is.True);
            Assert.That(errorMessages[0].Contains("this is not a valid path"), Is.True);

            // Validate config still intact
            currentConfig = await m_ProcessHelper.GetConfig();
            Assert.That(currentConfig.StorageFolders.Count, Is.EqualTo(1));
            Assert.That(currentConfig.StorageFolders.First().Path, Is.EqualTo(originalPath));
            currentStatus = await m_ProcessHelper.GetStatus();
            Assert.That(currentStatus.StorageFolders.Count, Is.EqualTo(1));
            Assert.That(currentStatus.StorageFolders.First().Path, Is.EqualTo(originalPath));
        }

        [Test]
        public async Task ChangeEndpoints()
        {
            await m_ProcessHelper.Start(GetTestTempFolder());

            var newConfig = await m_ProcessHelper.GetConfig();
            var originalEndPoints = newConfig.ControlEndPoints.ToArray();

            // Get the status through the blocking call
            var objectsUpdate1 = await m_ProcessHelper.GetObjectsUpdate(new[] { (k_StatusObjectName, 0ul) });
            Assert.That(objectsUpdate1.Count, Is.EqualTo(1));
            var statusUpdate1 = objectsUpdate1[k_StatusObjectName];
            var objectsUpdateTask = m_ProcessHelper.GetObjectsUpdate(
                new[] { (k_StatusObjectName, statusUpdate1.NextUpdate) });
            await Task.Delay(100); // So that objectsUpdateTask can have the time to complete if it wouldn't block
            Assert.That(objectsUpdateTask.IsCompleted, Is.False);

            // Try not to change anything, shouldn't set PendingRestart
            await m_ProcessHelper.PutConfig(newConfig);

            var status = await m_ProcessHelper.GetStatus();
            Assert.That(status.PendingRestart, Is.False);

            await Task.Delay(100); // So that objectsUpdateTask can have the time to complete if it wouldn't block
            Assert.That(objectsUpdateTask.IsCompleted, Is.False);

            // Now do a real change
            newConfig.ControlEndPoints = new[] { "http://127.0.0.1:8200", "http://0.0.0.0:8300" };

            var httpRet = await m_ProcessHelper.PutConfigWithResponse(newConfig);
            Assert.That(httpRet.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));

            // Check the update was well received
            var updatedConfig = await m_ProcessHelper.GetConfig();
            Assert.That(updatedConfig.ControlEndPoints, Is.EqualTo(newConfig.ControlEndPoints));

            // Check that the status is updated
            status = await m_ProcessHelper.GetStatus();
            Assert.That(status.PendingRestart, Is.True);

            // And that the blocking call succeeded
            var objectsUpdate2 = await objectsUpdateTask;
            Assert.That(objectsUpdate2.Count, Is.EqualTo(1));
            var statusUpdate2 = objectsUpdate2[k_StatusObjectName];
            Assert.That(statusUpdate2.NextUpdate, Is.GreaterThan(statusUpdate1.NextUpdate));
            Assert.That(statusUpdate2.Updated.Deserialize<Status>(Json.SerializerOptions), Is.EqualTo(status));

            // Restoring to the original shouldn't impact PendingRestart (as other stuff might have set the
            // PendingRestart flag).
            newConfig.ControlEndPoints = originalEndPoints;
            httpRet = await m_ProcessHelper.PutConfigWithResponse(newConfig);
            Assert.That(httpRet.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));
            status = await m_ProcessHelper.GetStatus();
            Assert.That(status.PendingRestart, Is.True);
        }

        [Test]
        public async Task SetBadEndpoints()
        {
            await m_ProcessHelper.Start(GetTestTempFolder());

            var newConfig = await m_ProcessHelper.GetConfig();
            var originalEndPoints = newConfig.ControlEndPoints.ToArray();

            (string, string)[] tests = new[] {
                ("http://www.google.com:8200", "Failed to parse"),
                ("http://512.256.645.456:8300", "Failed to parse"),
                ("http://127.0.0.1:-1", "Invalid port specified"),
                ("http://127.0.0.1:65536", "Invalid port specified"),
                ("http://1.2.3.4:8100", "does not refer to a local IP address"),
                ("ftp://127.0.0.1:8100", "does not start with http")
            };
            foreach ((string endpoint, string errorMessage) in tests)
            {
                newConfig.ControlEndPoints = new[] { endpoint };
                var httpRet = await m_ProcessHelper.PutConfigWithResponse(newConfig);
                Assert.That(httpRet.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                string[] errorMessages = await m_ProcessHelper.GetErrorDetails(httpRet.Content);
                Assert.That(errorMessages.Count, Is.EqualTo(1));
                Assert.That(errorMessages[0].Contains(errorMessage), Is.True);
            }

            // Check the config is still intact
            var currentConfig = await m_ProcessHelper.GetConfig();
            Assert.That(currentConfig.ControlEndPoints, Is.EqualTo(originalEndPoints));

            // Check that there isn't a restart pending
            var status = await m_ProcessHelper.GetStatus();
            Assert.That(status.PendingRestart, Is.False);
        }

        [Test]
        public async Task SetGoodEndpoints()
        {
            await m_ProcessHelper.Start(GetTestTempFolder());

            var endpoints = new List<string>();
            endpoints.Add("http://*:8200");
            endpoints.Add("http://0.0.0.0:8300");
            endpoints.Add("http://[::]:8400");
            endpoints.Add("http://127.0.0.1:8500");
            endpoints.Add("http://[::1]:8600");
            int portNumber = 8700;
            foreach (NetworkInterface item in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (item.NetworkInterfaceType != NetworkInterfaceType.Loopback && item.OperationalStatus == OperationalStatus.Up)
                {
                    IPInterfaceProperties adapterProperties = item.GetIPProperties();
                    foreach (UnicastIPAddressInformation ip in adapterProperties.UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        {
                            endpoints.Add($"http://{ip.Address}:{portNumber}");
                        }
                        portNumber += 100;
                    }
                }
            }

            var newConfig = await m_ProcessHelper.GetConfig();
            newConfig.ControlEndPoints = endpoints;
            var httpRet = await m_ProcessHelper.PutConfigWithResponse(newConfig);
            Assert.That(httpRet.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));
        }

        [Test]
        public async Task SaveAndLoad()
        {
            // Start
            var startFolder = GetTestTempFolder();
            await m_ProcessHelper.Start(startFolder);

            // Modify the config
            var newConfig = await m_ProcessHelper.GetConfig();
            newConfig.ControlEndPoints = new[] { newConfig.ControlEndPoints.First(), "http://127.0.0.1:8200",
                "http://0.0.0.0:8300" };
            var storageFolder0 =
                new StorageFolderConfig() { Path = GetTestTempFolder(), MaximumSize = 10L * 1024 * 1024 * 1024 };
            var storageFolder1 =
                new StorageFolderConfig() { Path = GetTestTempFolder(), MaximumSize = 20L * 1024 * 1024 * 1024 };
            newConfig.StorageFolders = new[] { storageFolder0, storageFolder1 };

            var httpRet = await m_ProcessHelper.PutConfigWithResponse(newConfig);
            Assert.That(httpRet.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));

            // Stop
            m_ProcessHelper.Stop();

            // Restart
            await m_ProcessHelper.Start(startFolder, false);
            var reloadedConfig = await m_ProcessHelper.GetConfig();
            Assert.That(reloadedConfig, Is.EqualTo(newConfig));

            // Check that the pending restart has been cleared
            var status = await m_ProcessHelper.GetStatus();
            Assert.That(status.PendingRestart, Is.False);
        }

        [Test]
        public async Task ConfigIsSaved()
        {
            string hangarBayFolder = GetTestTempFolder();

            // Initial start of the server, set config and stop it
            await m_ProcessHelper.Start(hangarBayFolder);

            var newConfig = await m_ProcessHelper.GetConfig();
            var storageFolder0 =
                new StorageFolderConfig() { Path = GetTestTempFolder(), MaximumSize = 10L * 1024 * 1024 * 1024 };
            var storageFolder1 =
                new StorageFolderConfig() { Path = GetTestTempFolder(), MaximumSize = 20L * 1024 * 1024 * 1024 };
            newConfig.StorageFolders = new[] { storageFolder0, storageFolder1 };

            await m_ProcessHelper.PutConfig(newConfig);

            var updatedConfig = await m_ProcessHelper.GetConfig();
            Assert.That(newConfig.StorageFolders, Is.EqualTo(updatedConfig.StorageFolders));

            m_ProcessHelper.Stop();

            // Now let's start it back, it should have saved the config
            await m_ProcessHelper.Start(hangarBayFolder, false);
            updatedConfig = await m_ProcessHelper.GetConfig();
            Assert.That(newConfig.StorageFolders, Is.EqualTo(updatedConfig.StorageFolders));
        }

        [Test]
        public async Task ConcurrentChanges()
        {
            await m_ProcessHelper.Start(GetTestTempFolder());

            // Initial configuration
            var newConfig = await m_ProcessHelper.GetConfig();
            var stallFolderPath = Path.Combine(GetTestTempFolder(), "stall");
            var storageFolder0 = new StorageFolderConfig() {
                Path = stallFolderPath,
                MaximumSize = 10L * 1024 * 1024 * 1024 };
            newConfig.StorageFolders = new[] { storageFolder0 };

            var stalledPutConfig = m_ProcessHelper.PutConfigWithResponse(newConfig);
            await Task.Delay(100); // So that if for some reason it does not stall we can detect it.
            Assert.That(stalledPutConfig.IsCompleted, Is.False);

            // Now that we have a blocked set configuration, queue a second one that will have to wait for the first
            // one to be done.
            newConfig.ControlEndPoints = newConfig.ControlEndPoints.Append("http://0.0.0.0:8300");
            var waitingPutConfig = m_ProcessHelper.PutConfigWithResponse(newConfig);
            await Task.Delay(100); // So that if for some reason it does not stall we can detect it.
            Assert.That(waitingPutConfig.IsCompleted, Is.False);

            // Unblock the stalled PutConfig
            await File.WriteAllTextAsync(Path.Combine(stallFolderPath, "resume.txt"), "content.txt");

            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            var awaitTask = await Task.WhenAny(stalledPutConfig, timeoutTask);
            Assert.That(awaitTask, Is.SameAs(stalledPutConfig)); // Or else await timed out
            Assert.That(stalledPutConfig.Result.StatusCode,
                Is.EqualTo(HttpStatusCode.OK).Or.EqualTo(HttpStatusCode.Accepted));
            awaitTask = await Task.WhenAny(waitingPutConfig, timeoutTask);
            Assert.That(awaitTask, Is.SameAs(waitingPutConfig)); // Or else await timed out
            Assert.That(waitingPutConfig.Result.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));

            var updatedConfig = await m_ProcessHelper.GetConfig();
            Assert.That(updatedConfig, Is.EqualTo(newConfig));
        }

        [Test]
        public async Task DefaultConfig()
        {
            await m_ProcessHelper.Start(GetTestTempFolder(), false);

            var defaultConfig = await m_ProcessHelper.GetConfig();

            // No need to test endpoints, default is ok, otherwise the above call wouldn't have succeeded :)

            Assert.That(defaultConfig.StorageFolders.Count, Is.EqualTo(1));
            var storageFolder = defaultConfig.StorageFolders.First();
            Assert.That(storageFolder.Path.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)),
                Is.True);
            Assert.That(storageFolder.MaximumSize, Is.GreaterThan(0));

            Assert.That(defaultConfig.HealthMonitoringIntervalSec, Is.GreaterThan(0));
        }

        [Test]
        [TestCase(State.Idle, HttpStatusCode.OK)]
        [TestCase(State.Preparing, HttpStatusCode.Conflict)]
        [TestCase(State.Launched, HttpStatusCode.Conflict)]
        [TestCase(State.Failure, HttpStatusCode.Conflict)]
        public async Task Put(State missionControlState, HttpStatusCode expectedStatusCode)
        {
            await m_ProcessHelper.Start(GetTestTempFolder());

            var originalConfig = await m_ProcessHelper.GetConfig();
            var newConfig = originalConfig;
            newConfig.HealthMonitoringIntervalSec *= 2;

            using (m_ProcessHelper.ForceState(missionControlState))
            {
                var ret = await m_ProcessHelper.PutConfigWithResponse(newConfig);
                Assert.That(ret.StatusCode, Is.EqualTo(expectedStatusCode));

                Assert.That(await m_ProcessHelper.GetConfig(),
                    expectedStatusCode == HttpStatusCode.OK ? Is.EqualTo(newConfig) : Is.EqualTo(originalConfig));
            }
        }

        string GetTestTempFolder()
        {
            var folderPath = Path.Combine(Path.GetTempPath(), "ConfigControllerTests_" + Guid.NewGuid().ToString());
            m_TestTempFolders.Add(folderPath);
            return folderPath;
        }

        MissionControlProcessHelper m_ProcessHelper = new();
        List<string> m_TestTempFolders = new();

        const string k_StatusObjectName = "status";
    }
}
