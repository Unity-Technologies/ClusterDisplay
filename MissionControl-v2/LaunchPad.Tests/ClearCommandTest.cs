using System;
using System.Diagnostics;
using System.Net;

namespace Unity.ClusterDisplay.MissionControl.LaunchPad.Tests
{
    public class ClearCommandTest
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
        public async Task ClearWhileIdle()
        {
            await m_ProcessHelper.Start(GetTestTempFolder());

            Guid payloadId = Guid.NewGuid();
            m_HangarBayStub.AddFile(payloadId, "file1.txt", "File1 content");
            m_HangarBayStub.AddFile(payloadId, "file2.txt", "File2 content");
            m_HangarBayStub.AddFile(payloadId, "file3.txt", "File3 content");
            m_HangarBayStub.AddFile(payloadId, "file4.txt", "File4 content");
            m_HangarBayStub.AddFile(payloadId, "file5.txt", "File5 content");

            PrepareCommand prepareCommand = new();
            prepareCommand.PayloadIds = new [] { payloadId };
            prepareCommand.LaunchPath = "notepad.exe";
            await m_ProcessHelper.PostCommand(prepareCommand, HttpStatusCode.Accepted);

            // Wait for ready to launch
            await m_ProcessHelper.WaitForState(State.WaitingForLaunch);

            // Abort to restore the state to idle
            AbortCommand abortCommand = new();
            await m_ProcessHelper.PostCommand(abortCommand);

            // Check the launchpad contains the expected files
            var preparedFiles = Directory.GetFiles(m_ProcessHelper.LaunchFolder);
            Assert.That(preparedFiles, Is.Not.Null);
            Assert.That(preparedFiles.Length, Is.EqualTo(5));

            // Clear the launchpad
            ClearCommand clearCommand = new();
            await m_ProcessHelper.PostCommand(clearCommand);

            // Launchpad should now be empty
            preparedFiles = Directory.GetFiles(m_ProcessHelper.LaunchFolder);
            Assert.That(preparedFiles, Is.Not.Null);
            Assert.That(preparedFiles, Is.Empty);

            // And state idle

        }

        [Test]
        public async Task ClearWhileOver()
        {
            await m_ProcessHelper.Start(GetTestTempFolder());

            Guid payloadId = Guid.NewGuid();
            m_HangarBayStub.AddFile(payloadId, "file1.txt", "File1 content");
            m_HangarBayStub.AddFile(payloadId, "file2.txt", "File2 content");
            m_HangarBayStub.AddFile(payloadId, "file3.txt", "File3 content");
            m_HangarBayStub.AddFile(payloadId, "file4.txt", "File4 content");
            m_HangarBayStub.AddFile(payloadId, "file5.txt", "File5 content");
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
            Assert.That(preparedFiles.Length, Is.EqualTo(7)); // The 5 files, Launch.ps1 and pid.txt

            // Kill that process
            launchedProcess.Kill();
            Assert.That(launchedProcess.HasExited, Is.True);

            // Wait for the LaunchPad to detect the process and exit and change its state to over
            await m_ProcessHelper.WaitForState(State.Over);

            // Clear the launchpad
            ClearCommand clearCommand = new();
            await m_ProcessHelper.PostCommand(clearCommand);

            // Launchpad should now be empty
            preparedFiles = Directory.GetFiles(m_ProcessHelper.LaunchFolder);
            Assert.That(preparedFiles, Is.Not.Null);
            Assert.That(preparedFiles, Is.Empty);

            // State should now be idle
            await m_ProcessHelper.WaitForState(State.Idle);
        }

        [Test]
        public async Task ClearWhileWaitingForLaunch()
        {
            await m_ProcessHelper.Start(GetTestTempFolder());

            Guid payloadId = Guid.NewGuid();
            m_HangarBayStub.AddFile(payloadId, "file1.txt", "File1 content");
            m_HangarBayStub.AddFile(payloadId, "file2.txt", "File2 content");
            m_HangarBayStub.AddFile(payloadId, "file3.txt", "File3 content");
            m_HangarBayStub.AddFile(payloadId, "file4.txt", "File4 content");
            m_HangarBayStub.AddFile(payloadId, "file5.txt", "File5 content");

            PrepareCommand prepareCommand = new();
            prepareCommand.PayloadIds = new [] { payloadId };
            prepareCommand.LaunchPath = "notepad.exe";
            await m_ProcessHelper.PostCommand(prepareCommand, HttpStatusCode.Accepted);

            // Wait for ready to launch
            await m_ProcessHelper.WaitForState(State.WaitingForLaunch);

            // Check the launchpad contains the expected files
            var preparedFiles = Directory.GetFiles(m_ProcessHelper.LaunchFolder);
            Assert.That(preparedFiles, Is.Not.Null);
            Assert.That(preparedFiles.Length, Is.EqualTo(5));

            // Clear the launchpad
            ClearCommand clearCommand = new();
            await m_ProcessHelper.PostCommand(clearCommand);

            // Launchpad should now be empty
            preparedFiles = Directory.GetFiles(m_ProcessHelper.LaunchFolder);
            Assert.That(preparedFiles, Is.Not.Null);
            Assert.That(preparedFiles, Is.Empty);
        }

        [Test]
        public async Task ClearWhileGettingPayload()
        {
            await m_ProcessHelper.Start(GetTestTempFolder());

            Guid payloadId = Guid.NewGuid();
            m_HangarBayStub.AddFile(payloadId, "file1.txt", "File1 content");
            m_HangarBayStub.AddFile(payloadId, "file2.txt", "File2 content");
            m_HangarBayStub.AddFile(payloadId, "file3.txt", "File3 content");
            m_HangarBayStub.AddFile(payloadId, "file4.txt", "File4 content");
            m_HangarBayStub.AddFile(payloadId, "file5.txt", "File5 content");
            HangarBayStubCheckpoint payloadCheckpoint = new();
            m_HangarBayStub.AddPayloadCheckpoint(payloadId, payloadCheckpoint);

            PrepareCommand prepareCommand = new();
            prepareCommand.PayloadIds = new [] { payloadId };
            prepareCommand.LaunchPath = "notepad.exe";
            await m_ProcessHelper.PostCommand(prepareCommand, HttpStatusCode.Accepted);

            // Wait for the launchpad to be "waiting for payload, so when payloadCheckpoint has been reached.
            await payloadCheckpoint.WaitingOnCheckpoint;
            var status = await m_ProcessHelper.GetStatus();
            Assert.That(status.State, Is.EqualTo(State.GettingPayload));

            // Try to clear, should fail because not in the right state
            ClearCommand clearCommand = new();
            await m_ProcessHelper.PostCommand(clearCommand, HttpStatusCode.Conflict);

            // Launchpad should still have 5 files
            var preparedFiles = Directory.GetFiles(m_ProcessHelper.LaunchFolder);
            Assert.That(preparedFiles, Is.Not.Null);
            Assert.That(preparedFiles.Length, Is.EqualTo(5));

            // Unblock the launchpad by completing getting of the payload
            payloadCheckpoint.UnblockCheckpoint();
        }

        [Test]
        public async Task ClearWhileDoingPrelaunch()
        {
            await m_ProcessHelper.Start(GetTestTempFolder());

            Guid payloadId = Guid.NewGuid();
            m_HangarBayStub.AddFile(payloadId, "file1.txt", "File1 content");
            m_HangarBayStub.AddFile(payloadId, "file2.txt", "File2 content");
            m_HangarBayStub.AddFile(payloadId, "file3.txt", "File3 content");
            m_HangarBayStub.AddFile(payloadId, "file4.txt", "File4 content");
            m_HangarBayStub.AddFile(payloadId, "file5.txt", "File5 content");
            m_HangarBayStub.AddFile(payloadId, "prelaunch.ps1",
                "\"SomeContent\" | Out-File -FilePath \"StartedPrelaunch.txt\"  \n" +
                "while (-not (Test-Path \"ConcludePrelaunch.txt\"))             \n" +
                "{                                                              \n" +
                "    Start-Sleep -Milliseconds 100                              \n" +
                "}");

            PrepareCommand prepareCommand = new();
            prepareCommand.PayloadIds = new [] { payloadId };
            prepareCommand.PreLaunchPath = "prelaunch.ps1";
            prepareCommand.LaunchPath = "notepad.exe";
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

            // Try to clear, should fail because not in the right state
            ClearCommand clearCommand = new();
            await m_ProcessHelper.PostCommand(clearCommand, HttpStatusCode.Conflict);

            // Launchpad should still have 7 files (5 + prelaunch.ps1 + StartedPrelaunch.txt)
            var preparedFiles = Directory.GetFiles(m_ProcessHelper.LaunchFolder);
            Assert.That(preparedFiles, Is.Not.Null);
            Assert.That(preparedFiles.Length, Is.EqualTo(7));

            // Unblock the launchpad by completing prelaunch
            await File.WriteAllTextAsync(Path.Combine(m_ProcessHelper.LaunchFolder, "ConcludePrelaunch.txt"), "Something");
        }

        [Test]
        public async Task ClearWhileLaunched()
        {
            await m_ProcessHelper.Start(GetTestTempFolder());

            Guid payloadId = Guid.NewGuid();
            m_HangarBayStub.AddFile(payloadId, "file1.txt", "File1 content");
            m_HangarBayStub.AddFile(payloadId, "file2.txt", "File2 content");
            m_HangarBayStub.AddFile(payloadId, "file3.txt", "File3 content");
            m_HangarBayStub.AddFile(payloadId, "file4.txt", "File4 content");
            m_HangarBayStub.AddFile(payloadId, "file5.txt", "File5 content");
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

            // Try to clear, should fail because not in the right state
            ClearCommand clearCommand = new();
            await m_ProcessHelper.PostCommand(clearCommand, HttpStatusCode.Conflict);

            // Launchpad should still have 7 files (5 + launch.ps1 + pid.txt)
            var preparedFiles = Directory.GetFiles(m_ProcessHelper.LaunchFolder);
            Assert.That(preparedFiles, Is.Not.Null);
            Assert.That(preparedFiles.Length, Is.EqualTo(7));
        }

        string GetTestTempFolder()
        {
            var folderPath = Path.Combine(Path.GetTempPath(), "ClearCommandTests_" + Guid.NewGuid().ToString());
            m_TestTempFolders.Add(folderPath);
            return folderPath;
        }

        LaunchPadProcessHelper m_ProcessHelper = new();
        List<string> m_TestTempFolders = new();
        HangarBayStub m_HangarBayStub = new();
    }
}
