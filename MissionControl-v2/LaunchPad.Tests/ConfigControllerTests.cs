using System;
using System.Net.NetworkInformation;
using System.Net;

namespace Unity.ClusterDisplay.MissionControl.LaunchPad
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
        public async Task SetClusterNetworkNic()
        {
            await m_ProcessHelper.Start(GetTestTempFolder());

            var newConfig = await m_ProcessHelper.GetConfig();

            // Test with name
            var clusterNetwork = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up)
                    .OrderBy(n => n.Speed)
                    .FirstOrDefault();
            Assert.That(clusterNetwork, Is.Not.Null);
            newConfig.ClusterNetworkNic = clusterNetwork!.Name;

            var httpRet = await m_ProcessHelper.PutConfig(newConfig);
            Assert.That(httpRet, Is.Not.Null);
            Assert.That(httpRet.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            // Check the update was well received
            var updatedConfig = await m_ProcessHelper.GetConfig();
            Assert.That(updatedConfig.ClusterNetworkNic, Is.EqualTo(newConfig.ClusterNetworkNic));

            // Test with NIC ip
            foreach (var unicastAddress in clusterNetwork.GetIPProperties().UnicastAddresses)
            {
                if (unicastAddress.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    newConfig.ClusterNetworkNic = unicastAddress.Address.ToString();
                    break;
                }
            }
            httpRet = await m_ProcessHelper.PutConfig(newConfig);
            Assert.That(httpRet, Is.Not.Null);
            Assert.That(httpRet.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            // Check the update was well received
            updatedConfig = await m_ProcessHelper.GetConfig();
            Assert.That(updatedConfig.ClusterNetworkNic, Is.EqualTo(newConfig.ClusterNetworkNic));
        }

        [Test]
        public async Task SetBadClusterNetworkNic()
        {
            await m_ProcessHelper.Start(GetTestTempFolder());

            var originalConfig = await m_ProcessHelper.GetConfig();

            // Test with a fictive NIC name
            var newConfig = await m_ProcessHelper.GetConfig();
            newConfig.ClusterNetworkNic = "Fictive NIC Name";

            var httpRet = await m_ProcessHelper.PutConfig(newConfig);
            Assert.That(httpRet, Is.Not.Null);
            Assert.That(httpRet.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

            // Check config remained unchanged
            var updatedConfig = await m_ProcessHelper.GetConfig();
            Assert.That(updatedConfig, Is.EqualTo(originalConfig));

            // Test with a fictive NIC ip
            newConfig.ClusterNetworkNic = "172.217.13.110"; // Google.com, no one should have their local IP set to Google's IP :)
            httpRet = await m_ProcessHelper.PutConfig(newConfig);
            Assert.That(httpRet, Is.Not.Null);
            Assert.That(httpRet.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

            // Check the update was well received
            updatedConfig = await m_ProcessHelper.GetConfig();
            Assert.That(updatedConfig, Is.EqualTo(originalConfig));
        }

        [Test]
        public async Task SetHangarBayEndPoint()
        {
            await m_ProcessHelper.Start(GetTestTempFolder());

            // Test with name
            var newConfig = await m_ProcessHelper.GetConfig();
            newConfig.HangarBayEndPoint = "http://localhost:8000";

            var httpRet = await m_ProcessHelper.PutConfig(newConfig);
            Assert.That(httpRet, Is.Not.Null);
            Assert.That(httpRet.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            // Check the update was well received
            var updatedConfig = await m_ProcessHelper.GetConfig();
            Assert.That(updatedConfig.HangarBayEndPoint, Is.EqualTo(newConfig.HangarBayEndPoint));

            // Test settings it to something obviously invalid
            var beforeBadConfig = await m_ProcessHelper.GetConfig();
            newConfig.HangarBayEndPoint = "This is obviously invalid!";
            httpRet = await m_ProcessHelper.PutConfig(newConfig);
            Assert.That(httpRet, Is.Not.Null);
            Assert.That(httpRet.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

            // Check config is still intact
            updatedConfig = await m_ProcessHelper.GetConfig();
            Assert.That(updatedConfig, Is.EqualTo(beforeBadConfig));
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
            Assert.That(status.PendingRestart, Is.False);

            // Now do a real change
            newConfig.ControlEndPoints = new[] { "http://localhost:8100", "http://127.0.0.1:8200", "http://0.0.0.0:8300" };

            httpRet = await m_ProcessHelper.PutConfig(newConfig);
            Assert.That(httpRet, Is.Not.Null);
            Assert.That(httpRet.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));

            // Check the update was well received
            var updatedConfig = await m_ProcessHelper.GetConfig();
            Assert.That(updatedConfig.ControlEndPoints, Is.EqualTo(newConfig.ControlEndPoints));

            // Check that the status is updated
            status = await m_ProcessHelper.GetStatus();
            Assert.That(status.PendingRestart, Is.True);

            // Restoring to the original shouldn't impact PendingRestart (as other stuff might have set the
            // PendingRestart flag).
            newConfig.ControlEndPoints = originalEndPoints;
            httpRet = await m_ProcessHelper.PutConfig(newConfig);
            Assert.That(httpRet, Is.Not.Null);
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
            newConfig.HangarBayEndPoint = "http://my.new.hangarbay:8100";

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
            Assert.That(status.PendingRestart, Is.False);
        }

        [Test]
        public async Task ConfigIsSaved()
        {
            string hangarBayFolder = GetTestTempFolder();

            // Initial start of the server, set config and stop it
            await m_ProcessHelper.Start(hangarBayFolder);

            var newConfig = await m_ProcessHelper.GetConfig();
            newConfig.HangarBayEndPoint = "http://my.new.hangarbay:8100";

            var httpRet = await m_ProcessHelper.PutConfig(newConfig);
            Assert.That(httpRet, Is.Not.Null);
            Assert.That(httpRet.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var updatedConfig = await m_ProcessHelper.GetConfig();
            Assert.That(newConfig.HangarBayEndPoint, Is.EqualTo(updatedConfig.HangarBayEndPoint));

            m_ProcessHelper.Stop();

            // Now let's start it back, it should have saved the config
            await m_ProcessHelper.Start(hangarBayFolder);
            updatedConfig = await m_ProcessHelper.GetConfig();
            Assert.That(newConfig.HangarBayEndPoint, Is.EqualTo(updatedConfig.HangarBayEndPoint));
        }

        [Test]
        public async Task Identifier()
        {
            await m_ProcessHelper.Start(GetTestTempFolder());

            var newConfig = await m_ProcessHelper.GetConfig();
            Assert.That(newConfig.Identifier, Is.Not.EqualTo(Guid.Empty));

            newConfig.Identifier = Guid.NewGuid();
            var httpRet = await m_ProcessHelper.PutConfig(newConfig);
            Assert.That(httpRet, Is.Not.Null);
            Assert.That(httpRet.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        string GetTestTempFolder()
        {
            var folderPath = Path.Combine(Path.GetTempPath(), "ConfigControllerTests_" + Guid.NewGuid().ToString());
            m_TestTempFolders.Add(folderPath);
            return folderPath;
        }

        LaunchPadProcessHelper m_ProcessHelper = new();
        List<string> m_TestTempFolders = new();
    }
}
