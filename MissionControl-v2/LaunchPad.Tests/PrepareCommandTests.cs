using System;
using System.Net;
using System.Diagnostics;

namespace Unity.ClusterDisplay.MissionControl.LaunchPad.Tests
{
    public class PrepareCommandTests
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
        public async Task Simple()
        {
            await m_ProcessHelper.Start(GetTestTempFolder());

            Guid payloadId = Guid.NewGuid();
            m_HangarBayStub.AddFile(payloadId, "SomeFile.txt", "SomeContent");
            m_HangarBayStub.AddFile(payloadId, "prelaunch.ps1",
                "\"SomeContent\" | Out-File -FilePath \"StartedPrelaunch.txt\"  \n" +
                "while (-not (Test-Path \"ConcludePrelaunch.txt\"))             \n" +
                "{                                                              \n" +
                "    Start-Sleep -Milliseconds 100                              \n" +
                "}");
            HangarBayStubCheckpoint payloadCheckpoint = new();
            m_HangarBayStub.AddPayloadCheckpoint(payloadId, payloadCheckpoint);

            PrepareCommand prepareCommand = new();
            prepareCommand.PayloadIds = new [] { payloadId };
            prepareCommand.PreLaunchPath = "prelaunch.ps1";
            prepareCommand.LaunchPath = "notepad.exe";
            var postCommandRet = await m_ProcessHelper.PostCommand(prepareCommand);
            Assert.That(postCommandRet, Is.Not.Null);
            Assert.That(postCommandRet.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));

            // Wait for the HangarBay stub checkpoint to be reached
            await payloadCheckpoint.WaitingOnCheckpoint;

            // HangarBay stub is still preparing the files, so state should still be getting payload...
            var status = await m_ProcessHelper.GetStatus();
            Assert.That(status, Is.Not.Null);
            Assert.That(status!.State, Is.EqualTo(State.GettingPayload));

            // Conclude preparation of files
            payloadCheckpoint.UnblockCheckpoint();

            // Wait for prelaunch script to start
            string prelauchStartedPath = Path.Combine(m_ProcessHelper.LaunchFolder, "StartedPrelaunch.txt");
            var waitPrelaunch = Stopwatch.StartNew();
            while (waitPrelaunch.Elapsed < TimeSpan.FromSeconds(15) && !File.Exists(prelauchStartedPath))
            {
                await Task.Delay(100);
            }
            Assert.That(File.Exists(prelauchStartedPath), Is.True);

            // The launchpad should be preparing the launchpad
            status = await m_ProcessHelper.GetStatus();
            Assert.That(status, Is.Not.Null);
            Assert.That(status!.State, Is.EqualTo(State.PreLaunch));

            // Indicate to the prelaunch script it can proceed
            await File.WriteAllTextAsync(Path.Combine(m_ProcessHelper.LaunchFolder, "ConcludePrelaunch.txt"), "Something");

            // State should soon change to ready to launch
            var waitReadyToLaunch = Stopwatch.StartNew();
            while (waitReadyToLaunch.Elapsed < TimeSpan.FromSeconds(15) && status!.State != State.WaitingForLaunch)
            {
                status = await m_ProcessHelper.GetStatus();
                Assert.That(status, Is.Not.Null);
            }
            Assert.That(status!.State, Is.EqualTo(State.WaitingForLaunch));
        }

        [Test]
        public async Task BadRequest()
        {
            await m_ProcessHelper.Start(GetTestTempFolder());

            // Test missing payload
            PrepareCommand prepareCommand = new();
            prepareCommand.PayloadIds = new Guid [] {};
            prepareCommand.LaunchPath = "calc.exe";
            var postCommandRet = await m_ProcessHelper.PostCommand(prepareCommand);
            Assert.That(postCommandRet, Is.Not.Null);
            Assert.That(postCommandRet.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            var message = await postCommandRet.Content.ReadAsStringAsync();
            Assert.That(message, Is.EqualTo("Must specify at least one payload to launch."));

            // Test missing executable to launch
            prepareCommand.PayloadIds = new [] { Guid.NewGuid() };
            prepareCommand.LaunchPath = "";
            postCommandRet = await m_ProcessHelper.PostCommand(prepareCommand);
            Assert.That(postCommandRet, Is.Not.Null);
            Assert.That(postCommandRet.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            message = await postCommandRet.Content.ReadAsStringAsync();
            Assert.That(message, Is.EqualTo("Must specify the path of the executable to launch."));
        }

        [Test]
        public async Task Conflict()
        {
            await m_ProcessHelper.Start(GetTestTempFolder());

            // Prepare and launch a payload
            Guid payloadId = Guid.NewGuid();
            m_HangarBayStub.AddFile(payloadId, "SomeFile.txt", "SomeContent");
            PrepareCommand prepareCommand = new();
            prepareCommand.PayloadIds = new [] { payloadId };
            prepareCommand.LaunchPath = "notepad.exe";
            var postCommandRet = await m_ProcessHelper.PostCommand(prepareCommand);
            Assert.That(postCommandRet, Is.Not.Null);
            Assert.That(postCommandRet.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));

            LaunchCommand launchCommand = new();
            postCommandRet = await m_ProcessHelper.PostCommand(launchCommand);
            Assert.That(postCommandRet, Is.Not.Null);
            Assert.That(postCommandRet.StatusCode, Is.EqualTo(HttpStatusCode.Accepted).Or.EqualTo(HttpStatusCode.OK));

            var waitLaunched = Stopwatch.StartNew();
            var status = await m_ProcessHelper.GetStatus();
            Assert.That(status, Is.Not.Null);
            while (waitLaunched.Elapsed < TimeSpan.FromSeconds(15) && status!.State != State.Launched)
            {
                status = await m_ProcessHelper.GetStatus();
                Assert.That(status, Is.Not.Null);
            }
            Assert.That(status!.State, Is.EqualTo(State.Launched));

            // Try to prepare something while there is already a payload launched
            postCommandRet = await m_ProcessHelper.PostCommand(prepareCommand);
            Assert.That(postCommandRet, Is.Not.Null);
            Assert.That(postCommandRet.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
            var message = await postCommandRet.Content.ReadAsStringAsync();
            Assert.That(message, Is.EqualTo("There is already a payload launched, first abort it."));
        }

        [Test]
        public async Task AbortWhileCopyingFiles()
        {
            await m_ProcessHelper.Start(GetTestTempFolder());

            Guid payloadId = Guid.NewGuid();
            m_HangarBayStub.AddFile(payloadId, "SomeFile.txt", "SomeContent");
            HangarBayStubCheckpoint payloadCheckpoint = new();
            m_HangarBayStub.AddPayloadCheckpoint(payloadId, payloadCheckpoint);

            PrepareCommand prepareCommand = new();
            prepareCommand.PayloadIds = new [] { payloadId };
            prepareCommand.LaunchPath = "notepad.exe";
            var postCommandRet = await m_ProcessHelper.PostCommand(prepareCommand);
            Assert.That(postCommandRet, Is.Not.Null);
            Assert.That(postCommandRet.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));

            // Wait for the HangarBay stub checkpoint to be reached
            await payloadCheckpoint.WaitingOnCheckpoint;

            // HangarBay stub is still preparing the files, so state should still be getting payload...
            var status = await m_ProcessHelper.GetStatus();
            Assert.That(status, Is.Not.Null);
            Assert.That(status!.State, Is.EqualTo(State.GettingPayload));

            // Send the abort
            AbortCommand abortCommand = new();
            postCommandRet = await m_ProcessHelper.PostCommand(abortCommand);
            Assert.That(postCommandRet, Is.Not.Null);
            Assert.That(postCommandRet.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            status = await m_ProcessHelper.GetStatus();
            Assert.That(status, Is.Not.Null);
            Assert.That(status!.State, Is.EqualTo(State.Idle));

            // Unblock concluding the payload preparation
            payloadCheckpoint.UnblockCheckpoint();

            // We should still be able to prepare we post a new prepare command
            postCommandRet = await m_ProcessHelper.PostCommand(prepareCommand);
            Assert.That(postCommandRet, Is.Not.Null);
            Assert.That(postCommandRet.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));

            var waitPrepared = Stopwatch.StartNew();
            while (waitPrepared.Elapsed < TimeSpan.FromSeconds(15) && status!.State != State.WaitingForLaunch)
            {
                status = await m_ProcessHelper.GetStatus();
                Assert.That(status, Is.Not.Null);
            }
            Assert.That(status!.State, Is.EqualTo(State.WaitingForLaunch));
        }

        [Test]
        public async Task AbortDuringPreLaunch()
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
            prepareCommand.LaunchPath = "notepad.exe";
            var postCommandRet = await m_ProcessHelper.PostCommand(prepareCommand);
            Assert.That(postCommandRet, Is.Not.Null);
            Assert.That(postCommandRet.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));

            // Wait for prelaunch script to start
            string prelauchStartedPath = Path.Combine(m_ProcessHelper.LaunchFolder, "StartedPrelaunch.txt");
            var waitPrelaunch = Stopwatch.StartNew();
            while (waitPrelaunch.Elapsed < TimeSpan.FromSeconds(15) && !File.Exists(prelauchStartedPath))
            {
                await Task.Delay(100);
            }
            Assert.That(File.Exists(prelauchStartedPath), Is.True);

            // Still running the prelaunch, so state should still be in the prelaunch sequence...
            var status = await m_ProcessHelper.GetStatus();
            Assert.That(status, Is.Not.Null);
            Assert.That(status!.State, Is.EqualTo(State.PreLaunch));

            // Send the abort
            AbortCommand abortCommand = new();
            var abortTask = m_ProcessHelper.PostCommand(abortCommand);
            Assert.That(abortTask, Is.Not.Null);

            // Wait a little bit
            await Task.Delay(500);

            // Aborting during the prelaunch does not cancel anything (as it could leave the system in a strange state).
            // So abort will wait for prelauch to complete.
            Assert.That(abortTask.IsCompleted, Is.False);

            // Indicate to the prelaunch it can conclude, this should unblock everything else.
            await File.WriteAllTextAsync(Path.Combine(m_ProcessHelper.LaunchFolder, "ConcludePrelaunch.txt"), "Something");

            await abortTask;
            Assert.That(abortTask.Result.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            status = await m_ProcessHelper.GetStatus();
            Assert.That(status, Is.Not.Null);
            Assert.That(status!.State, Is.EqualTo(State.Idle));

            // We should still be able to prepare we post a new prepare command
            m_HangarBayStub.AddFile(payloadId, "ConcludePrelaunch.txt", "Something");
            postCommandRet = await m_ProcessHelper.PostCommand(prepareCommand);
            Assert.That(postCommandRet, Is.Not.Null);
            Assert.That(postCommandRet.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));

            var waitPrepared = Stopwatch.StartNew();
            while (waitPrepared.Elapsed < TimeSpan.FromSeconds(15) && status!.State != State.WaitingForLaunch)
            {
                status = await m_ProcessHelper.GetStatus();
                Assert.That(status, Is.Not.Null);
            }
            Assert.That(status!.State, Is.EqualTo(State.WaitingForLaunch));
        }

        [Test]
        public async Task AbortWhileWaitingForLaunch()
        {
            await m_ProcessHelper.Start(GetTestTempFolder());

            Guid payloadId = Guid.NewGuid();
            m_HangarBayStub.AddFile(payloadId, "SomeFile.txt", "SomeContent");

            PrepareCommand prepareCommand = new();
            prepareCommand.PayloadIds = new [] { payloadId };
            prepareCommand.LaunchPath = "notepad.exe";
            var postCommandRet = await m_ProcessHelper.PostCommand(prepareCommand);
            Assert.That(postCommandRet, Is.Not.Null);
            Assert.That(postCommandRet.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));

            // Wait until the launchpad is ready for launch
            var waitReady = Stopwatch.StartNew();
            var status = await m_ProcessHelper.GetStatus();
            Assert.That(status, Is.Not.Null);
            while (waitReady.Elapsed < TimeSpan.FromSeconds(15) && status!.State != State.WaitingForLaunch)
            {
                status = await m_ProcessHelper.GetStatus();
                Assert.That(status, Is.Not.Null);
            }
            Assert.That(status!.State, Is.EqualTo(State.WaitingForLaunch));

            // Send the abort
            AbortCommand abortCommand = new();
            postCommandRet = await m_ProcessHelper.PostCommand(abortCommand);
            Assert.That(postCommandRet, Is.Not.Null);
            Assert.That(postCommandRet.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            status = await m_ProcessHelper.GetStatus();
            Assert.That(status, Is.Not.Null);
            Assert.That(status!.State, Is.EqualTo(State.Idle));

            // We should still be able to prepare we post a new prepare command
            postCommandRet = await m_ProcessHelper.PostCommand(prepareCommand);
            Assert.That(postCommandRet, Is.Not.Null);
            Assert.That(postCommandRet.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));

            var waitPrepared = Stopwatch.StartNew();
            status = await m_ProcessHelper.GetStatus();
            Assert.That(status, Is.Not.Null);
            while (waitPrepared.Elapsed < TimeSpan.FromSeconds(15) && status!.State != State.WaitingForLaunch)
            {
                status = await m_ProcessHelper.GetStatus();
                Assert.That(status, Is.Not.Null);
            }
            Assert.That(status!.State, Is.EqualTo(State.WaitingForLaunch));
        }

        // Test that sending a prepare while a prepare is running will cancel the first prepare before starting the
        // second prepare.  The easiest way to test is to have that second prepare wait
        [Test]
        public async Task SecondPrepareAborts()
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
            prepareCommand.LaunchPath = "notepad.exe";
            var postCommandRet = await m_ProcessHelper.PostCommand(prepareCommand);
            Assert.That(postCommandRet, Is.Not.Null);
            Assert.That(postCommandRet.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));

            // Wait for prelaunch script to start
            string prelauchStartedPath = Path.Combine(m_ProcessHelper.LaunchFolder, "StartedPrelaunch.txt");
            var waitPrelaunch = Stopwatch.StartNew();
            while (waitPrelaunch.Elapsed < TimeSpan.FromSeconds(15) && !File.Exists(prelauchStartedPath))
            {
                await Task.Delay(100);
            }
            Assert.That(File.Exists(prelauchStartedPath), Is.True);

            // Still running the prelaunch, so state should still be in the prelaunch sequence...
            var status = await m_ProcessHelper.GetStatus();
            Assert.That(status, Is.Not.Null);
            Assert.That(status!.State, Is.EqualTo(State.PreLaunch));

            // Send the second prepare
            m_HangarBayStub.AddFile(payloadId, "SecondPrepareCommand.txt", "Some content");
            var secondPrepareTask = m_ProcessHelper.PostCommand(prepareCommand);
            Assert.That(secondPrepareTask, Is.Not.Null);

            // Wait a little bit
            await Task.Delay(500);

            // Aborting during the prelaunch does not cancel anything (as it could leave the system in a strange state).
            // So abort will wait for prelauch to complete.
            Assert.That(secondPrepareTask.IsCompleted, Is.False);

            // Indicate to the prelaunch it can conclude, this should unblock everything else.
            var concludePrelaunchPath = Path.Combine(m_ProcessHelper.LaunchFolder, "ConcludePrelaunch.txt");
            await File.WriteAllTextAsync(concludePrelaunchPath, "Something");

            await secondPrepareTask;
            Assert.That(secondPrepareTask.Result.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));

            // Wait for prelaunch script to start (again)
            waitPrelaunch = Stopwatch.StartNew();
            var secondPrepareNewFile = Path.Combine(m_ProcessHelper.LaunchFolder, "SecondPrepareCommand.txt");
            while (waitPrelaunch.Elapsed < TimeSpan.FromSeconds(15) && !File.Exists(secondPrepareNewFile))
            {
                await Task.Delay(100);
            }
            Assert.That(File.Exists(secondPrepareNewFile), Is.True);
            while (waitPrelaunch.Elapsed < TimeSpan.FromSeconds(15) && !File.Exists(prelauchStartedPath))
            {
                await Task.Delay(100);
            }
            Assert.That(File.Exists(prelauchStartedPath), Is.True);

            // Still running the second prelaunch, so state should still be in the prelaunch sequence...
            status = await m_ProcessHelper.GetStatus();
            Assert.That(status, Is.Not.Null);
            Assert.That(status!.State, Is.EqualTo(State.PreLaunch));

            // Indicate to the second prelaunch it can conclude, this should unblock everything else.
            await File.WriteAllTextAsync(concludePrelaunchPath, "Something");

            // Wait until we are ready to launch
            var waitPrepared = Stopwatch.StartNew();
            while (waitPrepared.Elapsed < TimeSpan.FromSeconds(15) && status!.State != State.WaitingForLaunch)
            {
                status = await m_ProcessHelper.GetStatus();
                Assert.That(status, Is.Not.Null);
            }
            Assert.That(status!.State, Is.EqualTo(State.WaitingForLaunch));
        }

        [Test]
        public async Task InvalidPayloadId()
        {
            await m_ProcessHelper.Start(GetTestTempFolder());

            Guid payloadId = Guid.NewGuid();
            m_HangarBayStub.AddFile(payloadId, "SomeFile.txt", "SomeContent");

            PrepareCommand prepareCommand = new();
            prepareCommand.PayloadIds = new [] { Guid.NewGuid() };
            prepareCommand.LaunchPath = "notepad.exe";
            var postCommandRet = await m_ProcessHelper.PostCommand(prepareCommand);
            Assert.That(postCommandRet, Is.Not.Null);
            Assert.That(postCommandRet.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));

            // HangarBay stub will eventually be queried with that invalid payload and will return an error and so the
            // launchpad will become idle.
            var waitIdle = Stopwatch.StartNew();
            var status = await m_ProcessHelper.GetStatus();
            Assert.That(status, Is.Not.Null);
            while (waitIdle.Elapsed < TimeSpan.FromSeconds(15) && status!.State != State.Idle)
            {
                status = await m_ProcessHelper.GetStatus();
                Assert.That(status, Is.Not.Null);
            }
            Assert.That(status!.State, Is.EqualTo(State.Idle));

            // Ok, let's try again with the real payload id
            prepareCommand.PayloadIds = new [] { payloadId };
            postCommandRet = await m_ProcessHelper.PostCommand(prepareCommand);
            Assert.That(postCommandRet, Is.Not.Null);
            Assert.That(postCommandRet.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));

            // State should soon change to ready to launch
            var waitReadyToLaunch = Stopwatch.StartNew();
            while (waitReadyToLaunch.Elapsed < TimeSpan.FromSeconds(15) && status!.State != State.WaitingForLaunch)
            {
                status = await m_ProcessHelper.GetStatus();
                Assert.That(status, Is.Not.Null);
            }
            Assert.That(status!.State, Is.EqualTo(State.WaitingForLaunch));
        }

        string GetTestTempFolder()
        {
            var folderPath = Path.Combine(Path.GetTempPath(), "PrepareCommandTests_" + Guid.NewGuid().ToString());
            m_TestTempFolders.Add(folderPath);
            return folderPath;
        }

        LaunchPadProcessHelper m_ProcessHelper = new();
        List<string> m_TestTempFolders = new();
        HangarBayStub m_HangarBayStub = new();
    }
}
