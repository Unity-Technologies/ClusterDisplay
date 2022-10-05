
using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Reflection;

namespace Unity.ClusterDisplay.MissionControl.HangarBay.Tests
{
    public class RemoteManagementCommandsTests
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
        public async Task Restart()
        {
            await m_ProcessHelper.Start(GetTestTempFolder());

            var originalStatus = await m_ProcessHelper.GetStatus();
            Assert.That(originalStatus, Is.Not.Null);

            var restartCommand = new RestartCommand();
            var upgradeResponse = await m_ProcessHelper.PostCommand(restartCommand);
            Assert.That(upgradeResponse, Is.Not.Null);
            Assert.That(upgradeResponse.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));

            // The restart command sends the ok answer and then proceed with the restart, so we might have to wait a
            // little bit until the restart actually happens.
            var stopwatch = Stopwatch.StartNew();
            Status? newStatus = null;
            while (stopwatch.Elapsed < TimeSpan.FromSeconds(30))
            {
                try
                {
                    newStatus = await m_ProcessHelper.GetStatus();
                    if (newStatus != null)
                    {
                        if (newStatus.StartTime > originalStatus!.StartTime)
                        {
                            break;
                        }
                    }
                }
                catch
                {
                    // ignored
                }
                await Task.Delay(25);
            }

            Assert.That(newStatus, Is.Not.Null);
            Assert.That(newStatus!.StartTime > originalStatus!.StartTime);

            // Restart again (exercise the code cleaning old remote management command line arguments).
            originalStatus = newStatus;
            upgradeResponse = await m_ProcessHelper.PostCommand(restartCommand);
            Assert.That(upgradeResponse, Is.Not.Null);
            Assert.That(upgradeResponse.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));

            // The restart command sends the ok answer and then proceed with the restart, so we might have to wait a
            // little bit until the restart actually happens.
            stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < TimeSpan.FromSeconds(30))
            {
                try
                {
                    newStatus = await m_ProcessHelper.GetStatus();
                    if (newStatus != null)
                    {
                        if (newStatus.StartTime > originalStatus.StartTime)
                        {
                            break;
                        }
                    }
                }
                catch
                {
                    // ignored
                }
                await Task.Delay(25);
            }

            Assert.That(newStatus, Is.Not.Null);
            Assert.That(newStatus!.StartTime > originalStatus.StartTime);
        }

        [Test]
        public async Task KillIfRestartTimesOut()
        {
            await m_ProcessHelper.Start(GetTestTempFolder());

            var missionControlStub = new MissionControlStub();
            var payloadId = Guid.NewGuid();
            var fileBlobId = Guid.NewGuid();
            missionControlStub.AddFile(payloadId, "File.txt", fileBlobId, "Some content");
            var payloadCheckpoint = new MissionControlStubCheckpoint();
            missionControlStub.AddPayloadCheckpoint(payloadId, payloadCheckpoint);
            missionControlStub.Start();

            try
            {
                // Start preparing a folder that will never complete and so block restart.
                var prepareCommand = new PrepareCommand()
                {
                    Path = GetTestTempFolder(),
                    PayloadIds = new[] { payloadId },
                    PayloadSource = MissionControlStub.HttpListenerEndpoint
                };
                Task getPayloadTask = m_ProcessHelper.PostCommand(prepareCommand);

                await payloadCheckpoint.WaitingOnCheckpoint;

                // Send the restart, with a short timeout, should trigger a kill command
                var originalStatus = await m_ProcessHelper.GetStatus();
                Assert.That(originalStatus, Is.Not.Null);

                var restartCommand = new RestartCommand();
                restartCommand.TimeoutSec = 2;
                var stopwatch = Stopwatch.StartNew();
                await m_ProcessHelper.PostCommand(restartCommand);

                // The restart command sends the ok answer and then proceed with the restart, so we might have to wait a
                // little bit until the restart actually happens.
                Status? newStatus = null;
                while (stopwatch.Elapsed < TimeSpan.FromSeconds(15))
                {
                    try
                    {
                        newStatus = await m_ProcessHelper.GetStatus();
                        if (newStatus != null)
                        {
                            if (newStatus.StartTime > originalStatus!.StartTime)
                            {
                                break;
                            }
                        }
                    }
                    catch
                    {
                        // ignored
                    }
                    await Task.Delay(25);
                }

                Assert.That(newStatus, Is.Not.Null);
                Assert.That(newStatus!.StartTime > originalStatus!.StartTime);                    // Or otherwise the status is still from the same instance
                Assert.That(stopwatch.Elapsed, Is.LessThanOrEqualTo(TimeSpan.FromSeconds(15)));   // Or otherwise we timed out waiting for the kill
                Assert.That(stopwatch.Elapsed, Is.GreaterThanOrEqualTo(TimeSpan.FromSeconds(2))); // Or otherwise it finished before the kill

                // Unblock everything and wait for interrupted calls to finish
                payloadCheckpoint.UnblockCheckpoint();
                Assert.That(async () => await getPayloadTask, Throws.TypeOf<HttpRequestException>());
            }
            finally
            {
                missionControlStub.Stop();
            }
        }

        [Test]
        public async Task Upgrade()
        {
            // Prepare a zip file from the compiled hangar bay exe.
            var workFolder = GetTestTempFolder();
            Directory.CreateDirectory(workFolder);
            var hangarBayOriginals = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
            hangarBayOriginals = hangarBayOriginals.Replace("HangarBay.Tests", "HangarBay");
            const string newVersionZipFilename = "NewHangarBay.zip";
            var hangarBayZipFile = Path.Combine(workFolder, newVersionZipFilename);
            ZipFile.CreateFromDirectory(hangarBayOriginals, hangarBayZipFile);
            var hangarBayRunFolder = Path.Combine(workFolder, "bin");
            Directory.CreateDirectory(hangarBayRunFolder);
            ZipFile.ExtractToDirectory(hangarBayZipFile, hangarBayRunFolder);
            // File to detect if old files have been deleted
            string markerFilePath = Path.Combine(hangarBayRunFolder, "marker.txt");
            await File.WriteAllTextAsync(markerFilePath, "Marker");
            // Artificially change .exe time in the past to detect it was replaced
            var pastHangarBayFileTime = DateTime.Now - TimeSpan.FromDays(1);
            string hangerBayExePath = Path.Combine(hangarBayRunFolder, "HangarBay.exe");
            File.SetLastWriteTime(hangerBayExePath, pastHangarBayFileTime);
            Assert.That(File.GetLastWriteTime(hangerBayExePath), Is.EqualTo(pastHangarBayFileTime));

            // Create a default config using 127.0.0.1:8100 instead of the default 0.0.0.0:8100 so that we are not
            // prompted for a firewall permission since we are running from different folder each time.
            var newConfig = new Config();
            newConfig.ControlEndPoints = new[] { "http://127.0.0.1:8100" };
            await File.WriteAllTextAsync(Path.Combine(workFolder, "config.json"),
            System.Text.Json.JsonSerializer.Serialize(newConfig, Json.SerializerOptions));

            await m_ProcessHelper.Start(workFolder, hangarBayRunFolder);

            var originalStatus = await m_ProcessHelper.GetStatus();
            Assert.That(originalStatus, Is.Not.Null);

            var missionControlStub = new MissionControlStub();
            missionControlStub.FallbackHandler = (uri, _, response) =>
            {
                if (uri == newVersionZipFilename)
                {
                    response.StatusCode = (int)HttpStatusCode.OK;
                    response.ContentType = "application/zip";
                    using (var zipFileStream = File.OpenRead(hangarBayZipFile))
                    {
                        zipFileStream.CopyTo(response.OutputStream);
                    }
                    response.Close();
                }
                else
                {
                    response.StatusCode = (int)HttpStatusCode.NotFound;
                    response.Close();
                }
            };
            missionControlStub.Start();

            var upgradeCommand = new UpgradeCommand();
            upgradeCommand.NewVersionUrl =
                new Uri(MissionControlStub.HttpListenerEndpoint, newVersionZipFilename).ToString();
            var upgradeResponse = await m_ProcessHelper.PostCommand(upgradeCommand);
            Assert.That(upgradeResponse.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));

            // The upgrade command sends the accepted answer and then proceed with the restart, so we might have to wait
            // a little bit until the restart actually happens.
            var stopwatch = Stopwatch.StartNew();
            Status? newStatus = null;
            while (stopwatch.Elapsed < TimeSpan.FromSeconds(30))
            {
                try
                {
                    newStatus = await m_ProcessHelper.GetStatus();
                    if (newStatus != null)
                    {
                        if (newStatus.StartTime > originalStatus!.StartTime)
                        {
                            break;
                        }
                    }
                }
                catch
                {
                    // ignored
                }
                await Task.Delay(25);
            }

            Assert.That(newStatus, Is.Not.Null);
            Assert.That(newStatus!.StartTime > originalStatus!.StartTime);

            // Verify that an update was really done
            // 1. Folder with installation files gone
            Assert.That(Directory.Exists(Path.Combine(workFolder, "install")), Is.False);
            // 2. Extra file gone
            Assert.That(File.Exists(markerFilePath), Is.False);
            // 3. New files copied
            Assert.That(File.GetLastWriteTime(hangerBayExePath), Is.Not.EqualTo(pastHangarBayFileTime));
        }

        string GetTestTempFolder()
        {
            var folderPath = Path.Combine(Path.GetTempPath(), "RemoteManagementCommandsTests_" + Guid.NewGuid().ToString());
            m_TestTempFolders.Add(folderPath);
            return folderPath;
        }

        HangarBayProcessHelper m_ProcessHelper = new();
        List<string> m_TestTempFolders = new();
    }
}
