using System;
using System.Net;
using System.Net.NetworkInformation;

namespace Unity.ClusterDisplay.MissionControl.HangarBay.Tests
{
    public class ConfigControllerTests
    {
        [SetUp]
        public void SetUp()
        {
        }

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

            var newConfig = await m_ProcessHelper.GetConfig();
            var storageFolder0 =
                new StorageFolderConfig() { Path = GetTestTempFolder(), MaximumSize = 10L * 1024 * 1024 * 1024 };
            var storageFolder1 =
                new StorageFolderConfig() { Path = GetTestTempFolder(), MaximumSize = 20L * 1024 * 1024 * 1024 };
            newConfig.StorageFolders = new[] { storageFolder0, storageFolder1 };

            var httpRet = await m_ProcessHelper.PutConfig(newConfig);
            Assert.That(httpRet, Is.Not.Null);
            Assert.That(httpRet.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            // Check the update was well received
            var updatedConfig = await m_ProcessHelper.GetConfig();
            Assert.That(newConfig.StorageFolders, Is.EqualTo(updatedConfig.StorageFolders));

            // Check that the status is updated
            var status = await m_ProcessHelper.GetStatus();
            Assert.That(status, Is.Not.Null);
            var folderStatus0 = status!.StorageFolders.FirstOrDefault(fs => fs.Path == storageFolder0.Path);
            Assert.That(folderStatus0, Is.EqualTo(new StorageFolderStatus() {
                Path = storageFolder0.Path, MaximumSize = storageFolder0.MaximumSize }));
            var folderStatus1 = status.StorageFolders.FirstOrDefault(fs => fs.Path == storageFolder1.Path);
            Assert.That(folderStatus1, Is.EqualTo(new StorageFolderStatus() {
                Path = storageFolder1.Path, MaximumSize = storageFolder1.MaximumSize }));
        }

        [Test]
        public async Task SetBadConfig()
        {
            await m_ProcessHelper.Start(GetTestTempFolder());

            // Try setting no config at all
            var newConfig = await m_ProcessHelper.GetConfig();
            string originalPath = newConfig.StorageFolders.First().Path;
            newConfig.StorageFolders = new StorageFolderConfig[] { };

            var httpRet = await m_ProcessHelper.PutConfig(newConfig);
            Assert.That(httpRet, Is.Not.Null);
            Assert.That(httpRet.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            string[] errorMessages = await m_ProcessHelper.GetErrorDetails(httpRet.Content);
            Assert.That(errorMessages.Length, Is.EqualTo(1));
            Assert.That(errorMessages[0].Contains("one storage"), Is.True);

            // Validate config still intact
            var currentConfig = await m_ProcessHelper.GetConfig();
            Assert.That(currentConfig.StorageFolders, Is.Not.Empty);
            var currentStatus = await m_ProcessHelper.GetStatus();
            Assert.That(currentStatus, Is.Not.Null);
            Assert.That(currentStatus!.StorageFolders, Is.Not.Empty);

            // Try adding a valid path and an invalid one (none should be considered because of the invalid one).
            newConfig.StorageFolders = new StorageFolderConfig[] { };
            var storageFolder0 =
                new StorageFolderConfig() { Path = GetTestTempFolder(), MaximumSize = 10L * 1024 * 1024 * 1024 };
            var storageFolder1 =
                new StorageFolderConfig() { Path = "something://this is not a valid path", MaximumSize = 20L * 1024 * 1024 * 1024 };
            newConfig.StorageFolders = new[] { storageFolder0, storageFolder1 };

            httpRet = await m_ProcessHelper.PutConfig(newConfig);
            Assert.That(httpRet, Is.Not.Null);
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
            Assert.That(currentStatus, Is.Not.Null);
            Assert.That(currentStatus!.StorageFolders.Count, Is.EqualTo(1));
            Assert.That(currentStatus.StorageFolders.First().Path, Is.EqualTo(originalPath));
        }

        [Test]
        public async Task ChangeEndpoints()
        {
            await m_ProcessHelper.Start(GetTestTempFolder());

            var newConfig = await m_ProcessHelper.GetConfig();
            var originalEndPoints = newConfig.ControlEndPoints.ToArray();

            // Try not to change anything, shouldn't set PendingRestart
            var httpRet = await m_ProcessHelper.PutConfig(newConfig);
            Assert.That(httpRet, Is.Not.Null);
            Assert.That(httpRet.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var status = await m_ProcessHelper.GetStatus();
            Assert.That(status, Is.Not.Null);
            Assert.That(status!.PendingRestart, Is.False);

            // Now do a real change
            newConfig.ControlEndPoints = new[] { "http://127.0.0.1:8200", "http://0.0.0.0:8300" };

            httpRet = await m_ProcessHelper.PutConfig(newConfig);
            Assert.That(httpRet, Is.Not.Null);
            Assert.That(httpRet.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));

            // Check the update was well received
            var updatedConfig = await m_ProcessHelper.GetConfig();
            Assert.That(updatedConfig.ControlEndPoints, Is.EqualTo(newConfig.ControlEndPoints));

            // Check that the status is updated
            status = await m_ProcessHelper.GetStatus();
            Assert.That(status, Is.Not.Null);
            Assert.That(status!.PendingRestart, Is.True);

            // Restoring to the original shouldn't impact PendingRestart (as other stuff might have set the
            // PendingRestart flag).
            newConfig.ControlEndPoints = originalEndPoints;
            httpRet = await m_ProcessHelper.PutConfig(newConfig);
            Assert.That(httpRet, Is.Not.Null);
            Assert.That(httpRet.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));
            status = await m_ProcessHelper.GetStatus();
            Assert.That(status, Is.Not.Null);
            Assert.That(status!.PendingRestart, Is.True);
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
                var httpRet = await m_ProcessHelper.PutConfig(newConfig);
                Assert.That(httpRet, Is.Not.Null);
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
            Assert.That(status, Is.Not.Null);
            Assert.That(status!.PendingRestart, Is.False);
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
            var httpRet = await m_ProcessHelper.PutConfig(newConfig);
            Assert.That(httpRet, Is.Not.Null);
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

            var httpRet = await m_ProcessHelper.PutConfig(newConfig);
            Assert.That(httpRet, Is.Not.Null);
            Assert.That(httpRet.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));

            // Stop
            m_ProcessHelper.Stop();

            // Restart
            await m_ProcessHelper.Start(startFolder);
            var reloadedConfig = await m_ProcessHelper.GetConfig();
            Assert.That(reloadedConfig, Is.EqualTo(newConfig));

            // Check that the pending restart has been cleared
            var status = await m_ProcessHelper.GetStatus();
            Assert.That(status, Is.Not.Null);
            Assert.That(status!.PendingRestart, Is.False);
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

            var httpRet = await m_ProcessHelper.PutConfig(newConfig);
            Assert.That(httpRet, Is.Not.Null);
            Assert.That(httpRet.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var updatedConfig = await m_ProcessHelper.GetConfig();
            Assert.That(newConfig.StorageFolders, Is.EqualTo(updatedConfig.StorageFolders));

            m_ProcessHelper.Stop();

            // Now let's start it back, it should have saved the config
            await m_ProcessHelper.Start(hangarBayFolder);
            updatedConfig = await m_ProcessHelper.GetConfig();
            Assert.That(newConfig.StorageFolders, Is.EqualTo(updatedConfig.StorageFolders));
        }

        [Test]
        public async Task ConcurrentChanges()
        {
            await m_ProcessHelper.Start(GetTestTempFolder());

            // Initial config
            var newConfig = await m_ProcessHelper.GetConfig();
            var storageFolder0 =
                new StorageFolderConfig() { Path = GetTestTempFolder(), MaximumSize = 10L * 1024 * 1024 * 1024 };
            newConfig.StorageFolders = new[] { storageFolder0 };

            var httpRet = await m_ProcessHelper.PutConfig(newConfig);
            Assert.That(httpRet, Is.Not.Null);
            Assert.That(httpRet.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            // Start a mission control stub so that we can request some files and have that request block so that we can
            // test concurrent setting of config.
            var missionControlStub = new MissionControlStub();
            missionControlStub.Start();
            try
            {
                var payload1 = Guid.NewGuid();
                var fileBlob1 = Guid.NewGuid();
                missionControlStub.AddFile(payload1, "file1.txt", fileBlob1, "File1 content");

                var fetchFileCheckpoint = new MissionControlStubCheckpoint();
                missionControlStub.AddFileCheckpoint(fileBlob1, fetchFileCheckpoint);

                var prepareCommand = new PrepareCommand()
                {
                    Path = Path.Combine(GetTestTempFolder(), "PrepareInto"),
                    PayloadIds = new[] { payload1 },
                    PayloadSource = MissionControlStub.HttpListenerEndpoint
                };
                var asyncPrepare = m_ProcessHelper.PostCommand(prepareCommand);
                await fetchFileCheckpoint.WaitingOnCheckpoint;

                // Now that we have a blocked prepare, some things could block setting config, but we should still be
                // able to change endpoints.
                newConfig.ControlEndPoints = newConfig.ControlEndPoints.Append("http://0.0.0.0:8300");
                httpRet = await m_ProcessHelper.PutConfig(newConfig);
                Assert.That(httpRet, Is.Not.Null);
                Assert.That(httpRet.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));

                // However trying to remove the storage folder used by the blocked prepare should block.
                var storageFolder1 =
                    new StorageFolderConfig() { Path = GetTestTempFolder(), MaximumSize = 10L * 1024 * 1024 * 1024 };
                newConfig.StorageFolders = new[] { storageFolder1 };
                var blockPutConfig1 = m_ProcessHelper.PutConfig(newConfig);

                // And changing endpoints should now block because we want to keep things in order.
                newConfig.ControlEndPoints = newConfig.ControlEndPoints.Append("http://[::]:8400");
                var blockPutConfig2 = m_ProcessHelper.PutConfig(newConfig);

                // Wait a little bit to be sure our diagnostic of blocking is good
                await Task.Delay(100);

                Assert.That(asyncPrepare.IsCompleted, Is.False);
                Assert.That(blockPutConfig1.IsCompleted, Is.False);
                Assert.That(blockPutConfig2.IsCompleted, Is.False);

                // Unblock everything
                fetchFileCheckpoint.UnblockCheckpoint();

                // Wait for things to complete
                await asyncPrepare;
                await blockPutConfig1;
                await blockPutConfig2;

                // Get the effective finale configuration
                var effectiveConfig = await m_ProcessHelper.GetConfig();
                Assert.That(effectiveConfig, Is.EqualTo(newConfig));
            }
            finally
            {
                missionControlStub.Stop();
            }
        }

        string GetTestTempFolder()
        {
            var folderPath = Path.Combine(Path.GetTempPath(), "ConfigControllerTests_" + Guid.NewGuid().ToString());
            m_TestTempFolders.Add(folderPath);
            return folderPath;
        }

        HangarBayProcessHelper m_ProcessHelper = new();
        List<string> m_TestTempFolders = new();
    }
}
