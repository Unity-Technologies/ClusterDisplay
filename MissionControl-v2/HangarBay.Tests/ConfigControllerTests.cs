using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using static System.Net.WebRequestMethods;

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
                catch { }
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
            var folderStatus0 = status.StorageFolders.Where(fs => fs.Path == storageFolder0.Path).FirstOrDefault();
            Assert.That(folderStatus0, Is.EqualTo(new StorageFolderStatus() {
                Path = storageFolder0.Path, MaximumSize = storageFolder0.MaximumSize }));
            var folderStatus1 = status.StorageFolders.Where(fs => fs.Path == storageFolder1.Path).FirstOrDefault();
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
            Assert.That(currentStatus.StorageFolders, Is.Not.Empty);

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
            Assert.That(currentStatus.StorageFolders.Count, Is.EqualTo(1));
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
            Assert.That(status.PendingRestart, Is.False);

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
            Assert.That(status.PendingRestart, Is.True);

            // Restoring to the original shouldn't impact PendingRestart (as other stuff might have set the
            // PendingRestart flag).
            newConfig.ControlEndPoints = originalEndPoints;
            httpRet = await m_ProcessHelper.PutConfig(newConfig);
            Assert.That(httpRet, Is.Not.Null);
            Assert.That(httpRet.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));
            status = await m_ProcessHelper.GetStatus();
            Assert.That(status, Is.Not.Null);
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
            Assert.That(status.PendingRestart, Is.False);
        }

        // FSTL TODO: Tester les changements de config simultané (enlever un storage folder pendant qu'un fichier est en train de se faire fetcher)

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
