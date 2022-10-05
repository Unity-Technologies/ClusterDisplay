using System.Diagnostics;
using System.Net;

namespace Unity.ClusterDisplay.MissionControl.LaunchPad.Tests
{
    public class RestartCommandTests
    {
        [SetUp]
        public void SetUp()
        {
            m_HangarBayStub.Start();
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
        public async Task RestartWhileIdle()
        {
            await m_ProcessHelper.Start(GetTestTempFolder());

            var originalStatus = await m_ProcessHelper.GetStatus();
            Assert.That(originalStatus.State, Is.EqualTo(State.Idle));

            var restartCommand = new RestartCommand();
            await m_ProcessHelper.PostCommand(restartCommand, HttpStatusCode.Accepted);

            await CheckLaunchPadRestart(originalStatus);
        }

        [Test]
        public async Task RestartWhileOver()
        {
            await m_ProcessHelper.Start(GetTestTempFolder());

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

            var restartCommand = new RestartCommand();
            await m_ProcessHelper.PostCommand(restartCommand, HttpStatusCode.Accepted);

            await CheckLaunchPadRestart(originalStatus);
        }

        [Test]
        public async Task RestartWhileWaitingForLaunch()
        {
            await m_ProcessHelper.Start(GetTestTempFolder());

            Guid payloadId = Guid.NewGuid();
            m_HangarBayStub.AddFile(payloadId, "file.txt", "File content");

            PrepareCommand prepareCommand = new();
            prepareCommand.PayloadIds = new [] { payloadId };
            prepareCommand.LaunchPath = "nodepad.exe";
            await m_ProcessHelper.PostCommand(prepareCommand, HttpStatusCode.Accepted);

            // Wait for ready to launch
            await m_ProcessHelper.WaitForState(State.WaitingForLaunch);
            var originalStatus = await m_ProcessHelper.GetStatus();

            // Restart
            var restartCommand = new RestartCommand();
            await m_ProcessHelper.PostCommand(restartCommand, HttpStatusCode.Accepted);

            await CheckLaunchPadRestart(originalStatus!);
        }

        [Test]
        public async Task RestartWhileGettingPayload()
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

            // Try to restart, should fail because not in the right state
            var restartCommand = new RestartCommand();
            await m_ProcessHelper.PostCommand(restartCommand, HttpStatusCode.Conflict);

            // Unblock the launchpad by completing getting of the payload
            payloadCheckpoint.UnblockCheckpoint();
            await m_ProcessHelper.WaitForState(State.WaitingForLaunch);
        }

        [Test]
        public async Task RestartWhileDoingPrelaunch()
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
            await m_ProcessHelper.WaitForState(State.PreLaunch);

            // Try to restart, should fail because not in the right state
            var restartCommand = new RestartCommand();
            await m_ProcessHelper.PostCommand(restartCommand, HttpStatusCode.Conflict);

            // Unblock the launchpad by completing prelaunch
            await File.WriteAllTextAsync(Path.Combine(m_ProcessHelper.LaunchFolder, "ConcludePrelaunch.txt"), "Something");
            await m_ProcessHelper.WaitForState(State.WaitingForLaunch);
        }

        [Test]
        public async Task RestartWhileLaunched()
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

            // Try to restart, should fail because not in the right state
            var restartCommand = new RestartCommand();
            await m_ProcessHelper.PostCommand(restartCommand, HttpStatusCode.Conflict);
        }

        string GetTestTempFolder()
        {
            var folderPath = Path.Combine(Path.GetTempPath(), "RestartCommandTests_" + Guid.NewGuid().ToString());
            m_TestTempFolders.Add(folderPath);
            return folderPath;
        }

        async Task CheckLaunchPadRestart(Status originalStatus)
        {
            // The restart command sends the ok answer and then proceed with the restart, so we might have to wait a
            // little bit until the restart actually happens.
            var stopwatch = Stopwatch.StartNew();
            Status? newStatus = null;
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

            Assert.That(newStatus, Is.Not.Null);
            Assert.That(newStatus!.StartTime > originalStatus.StartTime);
        }

        LaunchPadProcessHelper m_ProcessHelper = new();
        List<string> m_TestTempFolders = new();
        HangarBayStub m_HangarBayStub = new();
    }
}
