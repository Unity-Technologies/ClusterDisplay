using System;
using System.Diagnostics;
using System.Net;
using System.Reflection;

namespace Unity.ClusterDisplay.MissionControl.LaunchPad
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
        public async Task SimpleLaunch()
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

            // Kill that process
            launchedProcess.Kill();
            Assert.That(launchedProcess.HasExited, Is.True);

            // Launchpad status should reflect that and become "over"
            await m_ProcessHelper.WaitForState(State.Over);
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
            await m_ProcessHelper.PostCommand(prepareCommand, HttpStatusCode.Accepted);

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
            await m_ProcessHelper.PostCommand(prepareCommand, HttpStatusCode.Accepted);

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

            AbortCommand abortCommand = new();
            await m_ProcessHelper.PostCommand(abortCommand);

            // The launched process should have been killed
            Assert.That(launchedProcess.HasExited, Is.True);

            // And so launchpad idle
            await m_ProcessHelper.WaitForState(State.Idle);

            await Task.Delay(100);
            var status = await m_ProcessHelper.GetStatus();
            Assert.That(status.State, Is.EqualTo(State.Idle));
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
            await m_ProcessHelper.PostCommand(prepareCommand, HttpStatusCode.Accepted);

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

            // Try to launch again
            await m_ProcessHelper.PostCommand(launchCommand, HttpStatusCode.Conflict);

            // The process should still be running
            Assert.That(launchedProcess.HasExited, Is.False);
        }

        [Test]
        public async Task MissingExecutable()
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
            prepareCommand.LaunchPath = "patate.n'estixte.pas";
            await m_ProcessHelper.PostCommand(prepareCommand, HttpStatusCode.Accepted);

            await m_ProcessHelper.WaitForState(State.WaitingForLaunch);

            LaunchCommand launchCommand = new();
            await m_ProcessHelper.PostCommand(launchCommand, HttpStatusCode.BadRequest);
        }

        public static void PrelaunchMethod(string arg)
        {
            File.WriteAllText("prelaunch.txt", arg);
        }

        public static void LaunchMethod(string arg)
        {
            File.WriteAllText("launch.txt", arg);
        }

        [Test]
        public async Task AssemblyRun()
        {
            await m_ProcessHelper.Start(GetTestTempFolder());

            Guid payloadId = Guid.NewGuid();

            // Add all the files required to load this unit test assembly as the payload
            var codeAssembly = Assembly.GetAssembly(typeof(LaunchCommandTests))!;
            var filesPath = Path.GetDirectoryName(codeAssembly.Location)!;
            var files = Directory.GetFiles(filesPath, "*.dll");
            foreach (var file in files)
            {
                var relativePath = Path.GetRelativePath(filesPath, file);
                m_HangarBayStub.AddFileToCopy(payloadId, relativePath, file);
            }

            // Prepare everything
            var thisClassFullName = typeof(LaunchCommandTests).FullName;
            PrepareCommand prepareCommand = new();
            prepareCommand.PayloadIds = new [] { payloadId };
            prepareCommand.PreLaunchPath = $"assemblyrun://LaunchPad.Tests.dll/{thisClassFullName}/{nameof(PrelaunchMethod)}/Prelaunch%20argument";
            prepareCommand.LaunchPath = $"assemblyrun://LaunchPad.Tests.dll/{thisClassFullName}/{nameof(LaunchMethod)}/Launch%20argument";
            await m_ProcessHelper.PostCommand(prepareCommand, HttpStatusCode.Accepted);

            LaunchCommand launchCommand = new();
            var postCommandRet = await m_ProcessHelper.PostCommandWithStatusCode(launchCommand);
            Assert.That(postCommandRet, Is.EqualTo(HttpStatusCode.Accepted).Or.EqualTo(HttpStatusCode.OK));

            string prelaunchFilename = Path.Combine(m_ProcessHelper.LaunchFolder, "prelaunch.txt");
            string launchFilename = Path.Combine(m_ProcessHelper.LaunchFolder, "launch.txt");

            // Wait for the process to create a launch.txt file
            var waitLaunched = Stopwatch.StartNew();
            while (waitLaunched.Elapsed < TimeSpan.FromSeconds(15))
            {
                if (File.Exists(launchFilename))
                {
                    break;
                }
                await Task.Delay(25);
            }

            // We know the launched process will immediately end so wait for the state to be over
            await m_ProcessHelper.WaitForState(State.Over);

            string prelaunchContent = File.ReadAllText(prelaunchFilename);
            Assert.That(prelaunchContent, Is.EqualTo("Prelaunch argument"));
            string launchContent = File.ReadAllText(launchFilename);
            Assert.That(launchContent, Is.EqualTo("Launch argument"));
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
