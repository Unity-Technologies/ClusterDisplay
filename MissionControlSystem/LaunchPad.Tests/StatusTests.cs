using System;
using System.Net;

namespace Unity.ClusterDisplay.MissionControl.LaunchPad
{
    public class StatusTests
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
        public async Task BlockingCall()
        {
            await m_ProcessHelper.Start(GetTestTempFolder());

            var initialStatus = await m_ProcessHelper.GetStatus();
            Assert.That(initialStatus.State, Is.EqualTo(State.Idle));

            var blockedStatusTask = m_ProcessHelper.GetStatus(initialStatus.StatusNumber + 1);
            Assert.That(blockedStatusTask, Is.Not.Null);
            Assert.That(blockedStatusTask.IsCompleted, Is.False);

            // Wait a little bit so that if for whatever reason the call does not block it has the time to return...
            await Task.Delay(250);
            Assert.That(blockedStatusTask.IsCompleted, Is.False);

            // Cause a change that will conclude the blocking call
            var newConfig = await m_ProcessHelper.GetConfig();
            newConfig.ControlEndPoints = new[] { "http://localhost:8200" };
            var httpRet = await m_ProcessHelper.PutConfig(newConfig);
            Assert.That(httpRet, Is.Not.Null);
            Assert.That(httpRet.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));

            // This should have unblocked the waiting call
            await Task.WhenAny(blockedStatusTask, Task.Delay(TimeSpan.FromSeconds(30)));
            Assert.That(blockedStatusTask.IsCompleted, Is.True);
            var blockedStatus = blockedStatusTask.Result;
            Assert.That(blockedStatus, Is.Not.Null);
            Assert.That(blockedStatus!.PendingRestart, Is.True);

            // Asking again with the same status number should return immediately
            await m_ProcessHelper.GetStatus(initialStatus.StatusNumber + 1);

            // But asking for the next one should block again
            blockedStatusTask = m_ProcessHelper.GetStatus(blockedStatus.StatusNumber + 1);
            Assert.That(blockedStatusTask, Is.Not.Null);
            Assert.That(blockedStatusTask.IsCompleted, Is.False);
            await Task.Delay(250);
            Assert.That(blockedStatusTask.IsCompleted, Is.False);

            // Unblock it again by changing state
            Guid payloadId = Guid.NewGuid();
            m_HangarBayStub.AddFile(payloadId, "SomeFile.txt", "SomeContent");
            HangarBayStubCheckpoint payloadCheckpoint = new();
            m_HangarBayStub.AddPayloadCheckpoint(payloadId, payloadCheckpoint);

            PrepareCommand prepareCommand = new();
            prepareCommand.PayloadIds = new [] { payloadId };
            prepareCommand.LaunchPath = "notepad.exe";
            await m_ProcessHelper.PostCommand(prepareCommand, HttpStatusCode.Accepted);

            await Task.WhenAny(blockedStatusTask, Task.Delay(TimeSpan.FromSeconds(30)));
            Assert.That(blockedStatusTask.IsCompleted, Is.True);
            blockedStatus = blockedStatusTask.Result;
            Assert.That(blockedStatus, Is.Not.Null);
            Assert.That(blockedStatus!.State, Is.Not.EqualTo(State.Idle));
        }

        [Test]
        public async Task ShutdownWhileBlocked()
        {
            await m_ProcessHelper.Start(GetTestTempFolder());

            var initialStatus = await m_ProcessHelper.GetStatus();
            Assert.That(initialStatus.State, Is.EqualTo(State.Idle));

            var blockedStatusTask = m_ProcessHelper.GetStatus(initialStatus.StatusNumber + 1);
            Assert.That(blockedStatusTask, Is.Not.Null);
            Assert.That(blockedStatusTask.IsCompleted, Is.False);

            // Wait a little bit so that if for whatever reason the call does not block it has the time to return...
            await Task.Delay(250);
            Assert.That(blockedStatusTask.IsCompleted, Is.False);

            // Let everything shutdown
        }

        [Test]
        public async Task LongBlocking()
        {
            await m_ProcessHelper.Start(GetTestTempFolder(), blockingCallMax: TimeSpan.FromSeconds(5));

            var initialStatus = await m_ProcessHelper.GetStatus();
            Assert.That(initialStatus.State, Is.EqualTo(State.Idle));

            var blockedStatusTask = m_ProcessHelper.GetStatus(initialStatus.StatusNumber + 1);
            Assert.That(blockedStatusTask, Is.Not.Null);
            Assert.That(blockedStatusTask.IsCompleted, Is.False);

            // Wait a little bit so that if for whatever reason the call does not block it has the time to return...
            await Task.Delay(250);
            Assert.That(blockedStatusTask.IsCompleted, Is.False);

            // Wait longer, and the LaunchPad should return us a NoContent.
            await Task.WhenAny(blockedStatusTask, Task.Delay(30000));

            Assert.That(blockedStatusTask.IsCompleted, Is.True);
            Assert.That(blockedStatusTask.Result, Is.Null);
        }

        string GetTestTempFolder()
        {
            var folderPath = Path.Combine(Path.GetTempPath(), "StatusTests_" + Guid.NewGuid().ToString());
            m_TestTempFolders.Add(folderPath);
            return folderPath;
        }

        LaunchPadProcessHelper m_ProcessHelper = new();
        List<string> m_TestTempFolders = new();
        HangarBayStub m_HangarBayStub = new();
    }
}
