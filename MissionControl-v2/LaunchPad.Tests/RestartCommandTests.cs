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
            Assert.That(originalStatus, Is.Not.Null);
            Assert.That(originalStatus!.State, Is.EqualTo(State.Idle));

            var restartCommand = new RestartCommand();
            var restartResponse = await m_ProcessHelper.PostCommand(restartCommand);
            Assert.That(restartResponse, Is.Not.Null);
            Assert.That(restartResponse.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));

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
            var postCommandRet = await m_ProcessHelper.PostCommand(prepareCommand);
            Assert.That(postCommandRet, Is.Not.Null);
            Assert.That(postCommandRet.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));

            // Wait for ready to launch
            await WaitForReadyToLaunch();
            var originalStatus = await m_ProcessHelper.GetStatus();
            Assert.That(originalStatus, Is.Not.Null);

            // Restart
            var restartCommand = new RestartCommand();
            var restartResponse = await m_ProcessHelper.PostCommand(restartCommand);
            Assert.That(restartResponse, Is.Not.Null);
            Assert.That(restartResponse.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));

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
            var postCommandRet = await m_ProcessHelper.PostCommand(prepareCommand);
            Assert.That(postCommandRet, Is.Not.Null);
            Assert.That(postCommandRet.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));

            // Wait for the launchpad to be "waiting for payload, so when payloadCheckpoint has been reached.
            await payloadCheckpoint.WaitingOnCheckpoint;
            var status = await m_ProcessHelper.GetStatus();
            Assert.That(status, Is.Not.Null);
            Assert.That(status!.State, Is.EqualTo(State.GettingPayload));

            // Try to restart, should fail because not in the right state
            var restartCommand = new RestartCommand();
            var restartResponse = await m_ProcessHelper.PostCommand(restartCommand);
            Assert.That(restartResponse, Is.Not.Null);
            Assert.That(restartResponse.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));

            // Unblock the launchpad by completing getting of the payload
            payloadCheckpoint.UnblockCheckpoint();
            await WaitForReadyToLaunch();
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
            var postCommandRet = await m_ProcessHelper.PostCommand(prepareCommand);
            Assert.That(postCommandRet, Is.Not.Null);
            Assert.That(postCommandRet.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));

            // Wait for the launchpad to be executing prelaunch
            string prelauchStartedPath = Path.Combine(m_ProcessHelper.LaunchFolder, "StartedPrelaunch.txt");
            var waitPrelaunch = Stopwatch.StartNew();
            while (waitPrelaunch.Elapsed < TimeSpan.FromSeconds(15) && !File.Exists(prelauchStartedPath))
            {
                await Task.Delay(100);
            }
            Assert.That(File.Exists(prelauchStartedPath), Is.True);
            var status = await m_ProcessHelper.GetStatus();
            Assert.That(status, Is.Not.Null);
            Assert.That(status!.State, Is.EqualTo(State.PreLaunch));

            // Try to restart, should fail because not in the right state
            var restartCommand = new RestartCommand();
            var restartResponse = await m_ProcessHelper.PostCommand(restartCommand);
            Assert.That(restartResponse, Is.Not.Null);
            Assert.That(restartResponse.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));

            // Unblock the launchpad by completing prelaunch
            await File.WriteAllTextAsync(Path.Combine(m_ProcessHelper.LaunchFolder, "ConcludePrelaunch.txt"), "Something");
            await WaitForReadyToLaunch();
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
            var postCommandRet = await m_ProcessHelper.PostCommand(prepareCommand);
            Assert.That(postCommandRet, Is.Not.Null);
            Assert.That(postCommandRet.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));

            LaunchCommand launchCommand = new();
            postCommandRet = await m_ProcessHelper.PostCommand(launchCommand);
            Assert.That(postCommandRet, Is.Not.Null);
            Assert.That(postCommandRet.StatusCode, Is.EqualTo(HttpStatusCode.Accepted).Or.EqualTo(HttpStatusCode.Accepted));

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
            Assert.That(status, Is.Not.Null);
            Assert.That(status!.State, Is.EqualTo(State.Launched));

            // Try to restart, should fail because not in the right state
            var restartCommand = new RestartCommand();
            var restartResponse = await m_ProcessHelper.PostCommand(restartCommand);
            Assert.That(restartResponse, Is.Not.Null);
            Assert.That(restartResponse.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
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

        async Task WaitForReadyToLaunch()
        {
            var waitReadyToLaunch = Stopwatch.StartNew();
            var originalStatus = await m_ProcessHelper.GetStatus();
            Assert.That(originalStatus, Is.Not.Null);
            while (waitReadyToLaunch.Elapsed < TimeSpan.FromSeconds(15) && originalStatus!.State != State.WaitingForLaunch)
            {
                originalStatus = await m_ProcessHelper.GetStatus();
                Assert.That(originalStatus, Is.Not.Null);
            }
            Assert.That(originalStatus!.State, Is.EqualTo(State.WaitingForLaunch));
        }

        LaunchPadProcessHelper m_ProcessHelper = new();
        List<string> m_TestTempFolders = new();
        HangarBayStub m_HangarBayStub = new();
    }
}
