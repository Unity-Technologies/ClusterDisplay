using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Reflection;

namespace Unity.ClusterDisplay.MissionControl.LaunchPad.Tests
{
    public class UpgradeCommandTests
    {
        [SetUp]
        public void SetUp()
        {
            // Prepare a zip file from the compiled launchpad exe.
            m_TestFolder = GetTestTempFolder();
            Directory.CreateDirectory(m_TestFolder);
            var launchPadOriginals = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
            launchPadOriginals = launchPadOriginals.Replace("LaunchPad.Tests", "LaunchPad");
            const string newVersionZipFilename = "LaunchPad.zip";
            var launchPadZipFile = Path.Combine(m_TestFolder, newVersionZipFilename);
            ZipFile.CreateFromDirectory(launchPadOriginals, launchPadZipFile);
            m_RunFolder = Path.Combine(m_TestFolder, "bin");
            Directory.CreateDirectory(m_RunFolder);
            ZipFile.ExtractToDirectory(launchPadZipFile, m_RunFolder);
            // File to detect if old files have been deleted
            m_MarkerFilePath = Path.Combine(m_RunFolder, "marker.txt");
            File.WriteAllText(m_MarkerFilePath, "Marker");
            // Artificially change .exe time in the past to detect it was replaced
            m_PastLaunchPadFileTime = DateTime.Now - TimeSpan.FromDays(1);
            m_LaunchPadExePath = Path.Combine(m_RunFolder, "LaunchPad.exe");
            File.SetLastWriteTime(m_LaunchPadExePath, m_PastLaunchPadFileTime);
            Assert.That(File.GetLastWriteTime(m_LaunchPadExePath), Is.EqualTo(m_PastLaunchPadFileTime));

            // Create a default config using 127.0.0.1:8200 instead of the default 0.0.0.0:8200 so that we are not
            // prompted for a firewall permission since we are running from different folder each time.
            var newConfig = new Config();
            newConfig.ControlEndPoints = new[] { "http://127.0.0.1:8200" };
            File.WriteAllText(Path.Combine(m_TestFolder, "config.json"),
                              System.Text.Json.JsonSerializer.Serialize(newConfig, Json.SerializerOptions));

            // Prepare the sub to be able to provide the .zip as an answer when requested...
            m_HangarBayStub.FallbackHandler = (uri,  _, response) =>
            {
                if (uri == newVersionZipFilename)
                {
                    response.StatusCode = (int)HttpStatusCode.OK;
                    response.ContentType = "application/zip";
                    using (var zipFileStream = File.OpenRead(launchPadZipFile))
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
            m_HangarBayStub.Start();
            m_NewVersionVersionUrl = new Uri(new Uri(HangarBayStub.HttpListenerEndpoint), newVersionZipFilename).ToString();
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

            m_HangarBayStub.Stop();
        }

        [Test]
        public async Task UpgradeWhileIdle()
        {
            await m_ProcessHelper.Start(m_TestFolder, m_RunFolder);

            var originalStatus = await m_ProcessHelper.GetStatus();
            Assert.That(originalStatus.State, Is.EqualTo(State.Idle));

            var upgradeCommand = new UpgradeCommand();
            upgradeCommand.NewVersionUrl = m_NewVersionVersionUrl;
            await m_ProcessHelper.PostCommand(upgradeCommand, HttpStatusCode.Accepted);

            await CheckUpgraded(originalStatus);
        }

        [Test]
        public async Task UpgradeWhileOver()
        {
            await m_ProcessHelper.Start(m_TestFolder, m_RunFolder);

            var originalStatus = await m_ProcessHelper.GetStatus();
            Assert.That(originalStatus.State, Is.EqualTo(State.Idle));

            Guid payloadId = Guid.NewGuid();
            m_HangarBayStub.AddFile(payloadId, "launch.ps1",
                "$pid | Out-File \"pid.txt\"    \n" +
                "while ( $true )                \n" +
                "{                              \n" +
                "    Start-Sleep -Seconds 60    \n" +
                "}");

            PrepareCommand prepareCommand = new();
            prepareCommand.PayloadIds = new [] { payloadId };
            prepareCommand.LaunchPath = "launch.ps1";
            await m_ProcessHelper.PostCommand(prepareCommand, HttpStatusCode.Accepted);

            // Launch the payload
            LaunchCommand launchCommand = new();
            var postCommandRet = await m_ProcessHelper.PostCommandWithStatusCode(launchCommand);
            Assert.That(postCommandRet, Is.EqualTo(HttpStatusCode.Accepted).Or.EqualTo(HttpStatusCode.OK));

            // Wait for the process to be launched and get its pid
            string processIdFilename = Path.Combine(m_ProcessHelper.LaunchFolder, "pid.txt");
            var waitLaunched = Stopwatch.StartNew();
            Process? launchedProcess = null;
            while (waitLaunched.Elapsed < TimeSpan.FromSeconds(15) && launchedProcess == null)
            {
                try
                {
                    var pidText = await File.ReadAllTextAsync(processIdFilename);
                    launchedProcess = Process.GetProcessById(Convert.ToInt32(pidText));
                }
                catch (Exception)
                {
                    // ignored
                }
            }
            Assert.That(launchedProcess, Is.Not.Null);
            Assert.That(launchedProcess!.HasExited, Is.False);

            // Check the launchpad contains the expected files
            var preparedFiles = Directory.GetFiles(m_ProcessHelper.LaunchFolder);
            Assert.That(preparedFiles, Is.Not.Null);

            // Kill that process
            launchedProcess.Kill();
            Assert.That(launchedProcess.HasExited, Is.True);

            // Wait for the LaunchPad to detect the process was killed and change its state to over
            await m_ProcessHelper.WaitForState(State.Over);

            // Do the upgrade
            var upgradeCommand = new UpgradeCommand();
            upgradeCommand.NewVersionUrl = m_NewVersionVersionUrl;
            await m_ProcessHelper.PostCommand(upgradeCommand, HttpStatusCode.Accepted);

            await CheckUpgraded(originalStatus);
        }

        [Test]
        public async Task UpgradeWhileWaitingForLaunch()
        {
            await m_ProcessHelper.Start(m_TestFolder, m_RunFolder);

            Guid payloadId = Guid.NewGuid();
            m_HangarBayStub.AddFile(payloadId, "file.txt", "File content");

            PrepareCommand prepareCommand = new();
            prepareCommand.PayloadIds = new [] { payloadId };
            prepareCommand.LaunchPath = "nodepad.exe";
            await m_ProcessHelper.PostCommand(prepareCommand, HttpStatusCode.Accepted);

            // Wait for ready to launch
            await m_ProcessHelper.WaitForState(State.WaitingForLaunch);
            var originalStatus = await m_ProcessHelper.GetStatus();

            // Upgrade
            var upgradeCommand = new UpgradeCommand();
            upgradeCommand.NewVersionUrl = m_NewVersionVersionUrl;
            await m_ProcessHelper.PostCommand(upgradeCommand, HttpStatusCode.Accepted);

            await CheckUpgraded(originalStatus);
        }

        [Test]
        public async Task UpgradeWhileGettingPayload()
        {
            await m_ProcessHelper.Start(GetTestTempFolder());

            Guid payloadId = Guid.NewGuid();
            m_HangarBayStub.AddFile(payloadId, "file.txt", "File content");
            HangarBayStubCheckpoint payloadCheckpoint = new();
            m_HangarBayStub.AddPayloadCheckpoint(payloadId, payloadCheckpoint);

            PrepareCommand prepareCommand = new();
            prepareCommand.PayloadIds = new [] { payloadId };
            prepareCommand.LaunchPath = "nodepad.exe";
            await m_ProcessHelper.PostCommand(prepareCommand, HttpStatusCode.Accepted);

            // Wait for the launchpad to be "waiting for payload, so when payloadCheckpoint has been reached.
            await payloadCheckpoint.WaitingOnCheckpoint;
            var status = await m_ProcessHelper.GetStatus();
            Assert.That(status.State, Is.EqualTo(State.GettingPayload));

            // Try to upgrade, should fail because not in the right state
            var upgradeCommand = new UpgradeCommand();
            upgradeCommand.NewVersionUrl = m_NewVersionVersionUrl;
            await m_ProcessHelper.PostCommand(upgradeCommand, HttpStatusCode.Conflict);

            // Unblock the launchpad by completing getting of the payload
            payloadCheckpoint.UnblockCheckpoint();
            await m_ProcessHelper.WaitForState(State.WaitingForLaunch);
        }

        [Test]
        public async Task UpgradeWhileDoingPrelaunch()
        {
            await m_ProcessHelper.Start(GetTestTempFolder());

            Guid payloadId = Guid.NewGuid();
            m_HangarBayStub.AddFile(payloadId, "prelaunch.ps1",
                "\"SomeContent\" | Out-File -FilePath \"StartedPrelaunch.txt\"  \n" +
                "while (-not (Test-Path \"ConcludePrelaunch.txt\"))             \n" +
                "{                                                              \n" +
                "    Start-Sleep -Milliseconds 100                              \n" +
                "}");

            PrepareCommand prepareCommand = new();
            prepareCommand.PayloadIds = new [] { payloadId };
            prepareCommand.PreLaunchPath = "prelaunch.ps1";
            prepareCommand.LaunchPath = "nodepad.exe";
            await m_ProcessHelper.PostCommand(prepareCommand, HttpStatusCode.Accepted);

            // Wait for the launchpad to be executing prelaunch
            string prelauchStartedPath = Path.Combine(m_ProcessHelper.LaunchFolder, "StartedPrelaunch.txt");
            var waitPrelaunch = Stopwatch.StartNew();
            while (waitPrelaunch.Elapsed < TimeSpan.FromSeconds(15) && !File.Exists(prelauchStartedPath))
            {
                await Task.Delay(100);
            }
            Assert.That(File.Exists(prelauchStartedPath), Is.True);
            var status = await m_ProcessHelper.GetStatus();
            Assert.That(status.State, Is.EqualTo(State.PreLaunch));

            // Try to upgrade, should fail because not in the right state
            var upgradeCommand = new UpgradeCommand();
            upgradeCommand.NewVersionUrl = m_NewVersionVersionUrl;
            await m_ProcessHelper.PostCommand(upgradeCommand, HttpStatusCode.Conflict);

            // Unblock the launchpad by completing prelaunch
            await File.WriteAllTextAsync(Path.Combine(m_ProcessHelper.LaunchFolder, "ConcludePrelaunch.txt"), "Something");
            await m_ProcessHelper.WaitForState(State.WaitingForLaunch);
        }

        [Test]
        public async Task UpgradeWhileLaunched()
        {
            await m_ProcessHelper.Start(GetTestTempFolder());

            Guid payloadId = Guid.NewGuid();
            m_HangarBayStub.AddFile(payloadId, "launch.ps1",
                "$pid | Out-File \"pid.txt\"    \n" +
                "while ( $true )                \n" +
                "{                              \n" +
                "    Start-Sleep -Seconds 60    \n" +
                "}");

            PrepareCommand prepareCommand = new();
            prepareCommand.PayloadIds = new [] { payloadId };
            prepareCommand.LaunchPath = "launch.ps1";
            await m_ProcessHelper.PostCommand(prepareCommand, HttpStatusCode.Accepted);

            LaunchCommand launchCommand = new();
            var postCommandRet = await m_ProcessHelper.PostCommandWithStatusCode(launchCommand);
            Assert.That(postCommandRet, Is.EqualTo(HttpStatusCode.Accepted).Or.EqualTo(HttpStatusCode.Accepted));

            // Wait for the process to be launched and get its pid
            string processIdFilename = Path.Combine(m_ProcessHelper.LaunchFolder, "pid.txt");
            var waitLaunched = Stopwatch.StartNew();
            Process? launchedProcess = null;
            while (waitLaunched.Elapsed < TimeSpan.FromSeconds(15) && launchedProcess == null)
            {
                try
                {
                    var pidText = await File.ReadAllTextAsync(processIdFilename);
                    launchedProcess = Process.GetProcessById(Convert.ToInt32(pidText));
                }
                catch (Exception)
                {
                    // ignored
                }
            }
            Assert.That(launchedProcess, Is.Not.Null);
            Assert.That(launchedProcess!.HasExited, Is.False);

            var status = await m_ProcessHelper.GetStatus();
            Assert.That(status.State, Is.EqualTo(State.Launched));

            // Try to upgrade, should fail because not in the right state
            var upgradeCommand = new UpgradeCommand();
            upgradeCommand.NewVersionUrl = m_NewVersionVersionUrl;
            await m_ProcessHelper.PostCommand(upgradeCommand, HttpStatusCode.Conflict);
        }

        string GetTestTempFolder()
        {
            var folderPath = Path.Combine(Path.GetTempPath(), "UpgradeCommandTests_" + Guid.NewGuid().ToString());
            m_TestTempFolders.Add(folderPath);
            return folderPath;
        }

        async Task CheckUpgraded(Status originalStatus)
        {
            // The upgrade command sends the accepted answer and then proceed with the restart, so we might have to wait
            // a little bit until the restart actually happens.
            var stopwatch = Stopwatch.StartNew();
            Status newStatus = new Status();
            while (stopwatch.Elapsed < TimeSpan.FromSeconds(30))
            {
                try
                {
                    newStatus = await m_ProcessHelper.GetStatus();
                    if (newStatus.StartTime > originalStatus.StartTime)
                    {
                        break;
                    }
                }
                catch
                {
                    // ignored
                }
                await Task.Delay(25);
            }

            Assert.That(newStatus.StartTime > originalStatus.StartTime);

            // Verify that an update was really done
            // 1. Folder with installation files gone
            Assert.That(Directory.Exists(Path.Combine(m_TestFolder, "install")), Is.False);
            // 2. Extra file gone
            Assert.That(File.Exists(m_MarkerFilePath), Is.False);
            // 3. New files copied
            Assert.That(File.GetLastWriteTime(m_LaunchPadExePath), Is.Not.EqualTo(m_PastLaunchPadFileTime));
        }

        string m_TestFolder = "";
        string m_RunFolder = "";
        string m_MarkerFilePath = "";
        DateTime m_PastLaunchPadFileTime;
        string m_LaunchPadExePath = "";
        string m_NewVersionVersionUrl = "";
        LaunchPadProcessHelper m_ProcessHelper = new();
        List<string> m_TestTempFolders = new();
        HangarBayStub m_HangarBayStub = new();
    }
}
