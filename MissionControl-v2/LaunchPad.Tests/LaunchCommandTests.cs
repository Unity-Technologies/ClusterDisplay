using System;
using System.Diagnostics;
using System.Net;

namespace Unity.ClusterDisplay.MissionControl.LaunchPad.Tests
{
    public class LaunchCommandTests
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
        public async Task KillOnExit()
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
            Assert.That(postCommandRet.StatusCode, Is.EqualTo(HttpStatusCode.Accepted).Or.EqualTo(HttpStatusCode.OK));

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

            // Terminate the launchpad
            m_ProcessHelper.Dispose();

            // The launched process should have been killed
            Assert.That(launchedProcess.HasExited, Is.True);
        }

        [Test]
        public async Task AbortLaunched()
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
            Assert.That(postCommandRet.StatusCode, Is.EqualTo(HttpStatusCode.Accepted).Or.EqualTo(HttpStatusCode.OK));

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

            AbortCommand abortCommand = new();
            postCommandRet = await m_ProcessHelper.PostCommand(abortCommand);
            Assert.That(postCommandRet, Is.Not.Null);
            Assert.That(postCommandRet.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            // The launched process should have been killed
            Assert.That(launchedProcess.HasExited, Is.True);

            // And so launchpad idle
            var waitIdle = Stopwatch.StartNew();
            var status = await m_ProcessHelper.GetStatus();
            Assert.That(status, Is.Not.Null);
            while (waitIdle.Elapsed < TimeSpan.FromSeconds(15) && status!.State != State.Idle)
            {
                status = await m_ProcessHelper.GetStatus();
                Assert.That(status, Is.Not.Null);
            }
            Assert.That(status!.State, Is.EqualTo(State.Idle));
        }

        [Test]
        public async Task DoubleLaunch()
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
            Assert.That(postCommandRet.StatusCode, Is.EqualTo(HttpStatusCode.Accepted).Or.EqualTo(HttpStatusCode.OK));

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

            // Try to launch again
            postCommandRet = await m_ProcessHelper.PostCommand(launchCommand);
            Assert.That(postCommandRet, Is.Not.Null);
            Assert.That(postCommandRet.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));

            // The process should still be running
            Assert.That(launchedProcess.HasExited, Is.False);
        }

        string GetTestTempFolder()
        {
            var folderPath = Path.Combine(Path.GetTempPath(), "LaunchCommandTests_" + Guid.NewGuid().ToString());
            m_TestTempFolders.Add(folderPath);
            return folderPath;
        }

        LaunchPadProcessHelper m_ProcessHelper = new();
        List<string> m_TestTempFolders = new();
        HangarBayStub m_HangarBayStub = new();
    }
}
