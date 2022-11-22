using System.Net;
using Microsoft.Extensions.Logging;
using Moq;
using Unity.ClusterDisplay.MissionControl.MissionControl.Library;

using LaunchPadCommand = Unity.ClusterDisplay.MissionControl.LaunchPad.Command;
using LaunchPadPrepareCommand = Unity.ClusterDisplay.MissionControl.LaunchPad.PrepareCommand;
using LaunchPadLaunchCommand = Unity.ClusterDisplay.MissionControl.LaunchPad.LaunchCommand;
using LaunchPadAbortCommand = Unity.ClusterDisplay.MissionControl.LaunchPad.AbortCommand;
using LaunchPadState = Unity.ClusterDisplay.MissionControl.LaunchPad.State;
using System.Diagnostics;

namespace Unity.ClusterDisplay.MissionControl.MissionControl.Tests
{
    public class LaunchManagerTests
    {
        [SetUp]
        public void Setup()
        {
            m_LoggerMock.Reset();
            m_Manager = new(m_LoggerMock.Object, m_HttpClient);
        }

        [TearDown]
        public async Task TearDown()
        {
            if (m_Manager is {NeedsConcludeCall: true})
            {
                foreach (var launchPad in m_LaunchPads)
                {
                    launchPad.CommandHandler = (launchPadCommand) =>
                        TypeCommandHandler<LaunchPadAbortCommand>(launchPadCommand, launchPad.Id, LaunchPadState.Idle,
                            () => { });
                }

                Assert.That(m_LaunchTask, Is.Not.Null);
                m_Manager.Stop();
                lock (m_LaunchPadsStatusLock)
                {
                    foreach (var status in m_LaunchPadsStatus.Values)
                    {
                        status.State = LaunchPadState.Over;
                        status.SignalChanges(m_LaunchPadsStatus);
                    }
                }
                if (m_LaunchTask != null)
                {
                    await m_LaunchTask;
                }
                lock (m_LaunchPadsStatusLock)
                {
                    m_Manager.Conclude(m_LaunchPadsStatus);
                }
            }
            m_Manager = null;
            m_LaunchTask = null;

            foreach (var launchPad in m_LaunchPads)
            {
                launchPad.Stop();
            }
            m_LaunchPads.Clear();

            m_LoggerMock.VerifyNoOtherCalls();
            m_LaunchPadsStatus.Clear();
        }

        [Test]
        [TestCase(1)]
        [TestCase(2)]
        public async Task SimpleLaunch(int nbrLaunch)
        {
            var playground = PreparePlayground(1);
            playground.PrepareLaunchPadsStatus(m_LaunchPadsStatus);

            var launchPadStub = playground.LaunchPadStubs.First();
            var launchAsset = playground.LaunchedAsset;

            for (int launchIdx = 0; launchIdx < nbrLaunch; ++launchIdx)
            {
                // Default launched task when nothing is launched is always completed.
                Assert.That(m_Manager!.Launched.IsCompleted, Is.True);

                // Prepare launchpad
                TaskCompletionSource processLaunched = new(TaskCreationOptions.RunContinuationsAsynchronously);

                HttpStatusCode ProcessLaunchCommand(LaunchPadCommand launchPadCommand) =>
                    TypeCommandHandler<LaunchPadLaunchCommand>(launchPadCommand, launchPadStub.Id, LaunchPadState.Launched, () =>
                    {
                        launchPadStub.CommandHandler = null;
                        processLaunched.TrySetResult();
                    });

                HttpStatusCode ProcessPrepareCommand(LaunchPadCommand launchPadCommand) =>
                    PrepareCommandHandler(launchPadCommand, launchAsset.Launchables.First(), launchPadStub.Id,
                        LaunchPadState.WaitingForLaunch, () => { launchPadStub.CommandHandler = ProcessLaunchCommand; });

                launchPadStub.CommandHandler = ProcessPrepareCommand;

                lock (m_LaunchPadsStatusLock)
                {
                    m_LaunchTask = m_Manager.LaunchAsync(playground.Manifest);
                }

                Assert.That(m_Manager.LaunchPadsCount, Is.EqualTo(1));
                Assert.That(m_Manager.RunningLaunchPads, Is.EqualTo(1));

                // Wait for the LaunchPad to be requested to launch
                Task timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
                var finishedTask = await Task.WhenAny(processLaunched.Task, timeoutTask);
                Assert.That(finishedTask, Is.SameAs(processLaunched.Task)); // Otherwise timeout

                Assert.That(m_Manager.Launched.IsCompleted, Is.True);

                // LaunchPad launched, but the launch task from LaunchAsync should still be running
                Assert.That(m_LaunchTask.IsCompleted, Is.False);

                // Prepare for stopping the launchpad
                TaskCompletionSource abortReceivedByLaunchPad = new(TaskCreationOptions.RunContinuationsAsynchronously);
                launchPadStub.CommandHandler = (launchPadCommand) =>
                    TypeCommandHandler<LaunchPadAbortCommand>(launchPadCommand, launchPadStub.Id, LaunchPadState.Idle,
                    () => {
                        abortReceivedByLaunchPad.TrySetResult();
                    });

                Assert.That(m_Manager.LaunchPadsCount, Is.EqualTo(1));
                Assert.That(m_Manager.RunningLaunchPads, Is.EqualTo(1));

                // Stop it and wait for the launch task to conclude
                m_Manager.Stop();

                timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
                finishedTask = await Task.WhenAny(m_LaunchTask, timeoutTask);
                Assert.That(finishedTask, Is.SameAs(m_LaunchTask)); // Otherwise timeout
                finishedTask = await Task.WhenAny(abortReceivedByLaunchPad.Task, timeoutTask);
                Assert.That(finishedTask, Is.SameAs(abortReceivedByLaunchPad.Task)); // Otherwise timeout

                Assert.That(m_Manager.LaunchPadsCount, Is.EqualTo(1));
                Assert.That(m_Manager.RunningLaunchPads, Is.EqualTo(0));

                // Conclude everything
                lock (m_LaunchPadsStatusLock)
                {
                    m_Manager.Conclude(m_LaunchPadsStatus);
                }

                Assert.That(m_Manager.LaunchPadsCount, Is.EqualTo(0));
                Assert.That(m_Manager.RunningLaunchPads, Is.EqualTo(0));
            }
        }

        [Test]
        public async Task FailedPrepare()
        {
            var playground = PreparePlayground(1);
            playground.PrepareLaunchPadsStatus(m_LaunchPadsStatus);

            var launchPadStub = playground.LaunchPadStubs.First();
            var launchAsset = playground.LaunchedAsset;

            launchPadStub.CommandHandler = (launchPadCommand) =>
                PrepareCommandHandler(launchPadCommand, launchAsset.Launchables.First(), launchPadStub.Id,
                    LaunchPadState.Over, () => {
                        launchPadStub.CommandHandler = null;
                    });

            lock (m_LaunchPadsStatusLock)
            {
                m_LaunchTask = m_Manager!.LaunchAsync(playground.Manifest);
            }

            // Wait for the complete launch to complete pretty quickly
            Task timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            var finishedTask = await Task.WhenAny(m_LaunchTask, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(m_LaunchTask)); // Otherwise timeout

            // Verify message about failed launch
            m_LoggerMock.VerifyLog(l => l.LogError("No launchpad succeeded in preparing for launch, launch failed"));
        }

        [Test]
        public async Task OneFailedPrepareOutOfTwo()
        {
            var playground = PreparePlayground(2);
            playground.PrepareLaunchPadsStatus(m_LaunchPadsStatus);

            var launchPad1Stub = playground.LaunchPadStubs.ElementAt(0);
            var launchPad2Stub = playground.LaunchPadStubs.ElementAt(1);
            var launchAsset = playground.LaunchedAsset;

            // Setup command processor for launchpad 1 that will succeed
            TaskCompletionSource processLaunched1 = new(TaskCreationOptions.RunContinuationsAsynchronously);

            HttpStatusCode ProcessLaunch1Command(LaunchPadCommand launchPadCommand) =>
                TypeCommandHandler<LaunchPadLaunchCommand>(launchPadCommand, launchPad1Stub.Id, LaunchPadState.Launched, () =>
                {
                    launchPad1Stub.CommandHandler = null;
                    processLaunched1.TrySetResult();
                });

            HttpStatusCode ProcessPrepare1Command(LaunchPadCommand launchPadCommand) =>
                PrepareCommandHandler(launchPadCommand, launchAsset.Launchables.First(), launchPad1Stub.Id,
                    LaunchPadState.WaitingForLaunch, () => { launchPad1Stub.CommandHandler = ProcessLaunch1Command; });

            launchPad1Stub.CommandHandler = ProcessPrepare1Command;

            // Setup command processor for launchpad 2 that will fail
            bool launchPad2LaunchCalled = false;

            HttpStatusCode ProcessLaunch2Command(LaunchPadCommand launchPadCommand) =>
                TypeCommandHandler<LaunchPadLaunchCommand>(launchPadCommand, launchPad2Stub.Id, LaunchPadState.Launched, () =>
                {
                    launchPad2Stub.CommandHandler = null;
                    launchPad2LaunchCalled = true;
                });

            HttpStatusCode ProcessPrepare2Command(LaunchPadCommand launchPadCommand) =>
                PrepareCommandHandler(launchPadCommand, launchAsset.Launchables.First(), launchPad2Stub.Id,
                    LaunchPadState.Over, () => { launchPad2Stub.CommandHandler = ProcessLaunch2Command; });

            launchPad2Stub.CommandHandler = ProcessPrepare2Command;

            // Perform launch
            lock (m_LaunchPadsStatusLock)
            {
                m_LaunchTask = m_Manager!.LaunchAsync(playground.Manifest);
            }

            // Wait for the LaunchPad to be requested to launch
            Task timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            var finishedTask = await Task.WhenAny(processLaunched1.Task, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(processLaunched1.Task)); // Otherwise timeout

            Assert.That(m_Manager.LaunchPadsCount, Is.EqualTo(2));
            Assert.That(m_Manager.RunningLaunchPads, Is.EqualTo(1));

            // LaunchPad launched, but the launch task from LaunchAsync should still be running
            Assert.That(m_LaunchTask.IsCompleted, Is.False);

            // Prepare for stopping the launchpad that successfully started
            TaskCompletionSource abortReceivedByLaunchPad = new(TaskCreationOptions.RunContinuationsAsynchronously);
            launchPad1Stub.CommandHandler = (launchPadCommand) =>
                TypeCommandHandler<LaunchPadAbortCommand>(launchPadCommand, launchPad1Stub.Id, LaunchPadState.Idle,
                () => {
                    abortReceivedByLaunchPad.TrySetResult();
                });

            // Stop it and wait for the launch task to conclude
            m_Manager.Stop();

            timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            finishedTask = await Task.WhenAny(m_LaunchTask, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(m_LaunchTask)); // Otherwise timeout
            finishedTask = await Task.WhenAny(abortReceivedByLaunchPad.Task, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(abortReceivedByLaunchPad.Task)); // Otherwise timeout

            // LaunchPad2 should never have been asked to launch (since it failed during prepare)
            Assert.That(launchPad2LaunchCalled, Is.False);

            Assert.That(m_Manager.LaunchPadsCount, Is.EqualTo(2));
            Assert.That(m_Manager.RunningLaunchPads, Is.EqualTo(0));
        }

        [Test]
        public async Task RefuseDoubleLaunch()
        {
            var playground = PreparePlayground(1);
            playground.PrepareLaunchPadsStatus(m_LaunchPadsStatus);

            var launchPadStub = playground.LaunchPadStubs.First();
            var launchAsset = playground.LaunchedAsset;

            TaskCompletionSource processLaunched = new(TaskCreationOptions.RunContinuationsAsynchronously);

            HttpStatusCode ProcessLaunchCommand(LaunchPadCommand launchPadCommand) =>
                TypeCommandHandler<LaunchPadLaunchCommand>(launchPadCommand, launchPadStub.Id, LaunchPadState.Launched, () =>
                {
                    launchPadStub.CommandHandler = null;
                    processLaunched.TrySetResult();
                });

            HttpStatusCode ProcessPrepareCommand(LaunchPadCommand launchPadCommand) =>
                PrepareCommandHandler(launchPadCommand, launchAsset.Launchables.First(), launchPadStub.Id,
                    LaunchPadState.WaitingForLaunch, () => { launchPadStub.CommandHandler = ProcessLaunchCommand; });

            launchPadStub.CommandHandler = ProcessPrepareCommand;

            lock (m_LaunchPadsStatusLock)
            {
                m_LaunchTask = m_Manager!.LaunchAsync(playground.Manifest);
            }

            // Wait for the LaunchPad to be requested to launch
            Task timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            var finishedTask = await Task.WhenAny(processLaunched.Task, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(processLaunched.Task)); // Otherwise timeout

            // LaunchPad launched, but the launch task from LaunchAsync should still be running
            Assert.That(m_LaunchTask.IsCompleted, Is.False);

            // Try to launch again while running should fail
            lock (m_LaunchPadsStatusLock)
            {
                Assert.ThrowsAsync<InvalidOperationException>(() => m_Manager!.LaunchAsync(playground.Manifest));
            }
        }

        [Test]
        public async Task BadLaunchSetup()
        {
            var playground = PreparePlayground(1);
            playground.PrepareLaunchPadsStatus(m_LaunchPadsStatus);

            var launchPadStub = playground.LaunchPadStubs.First();
            var launchAsset = playground.LaunchedAsset;

            // Setup command handler so that we can do a successful launch if everything is valid.
            TaskCompletionSource processLaunched = new(TaskCreationOptions.RunContinuationsAsynchronously);

            HttpStatusCode ProcessLaunchCommand(LaunchPadCommand launchPadCommand) =>
                TypeCommandHandler<LaunchPadLaunchCommand>(launchPadCommand, launchPadStub.Id, LaunchPadState.Launched, () =>
                {
                    launchPadStub.CommandHandler = null;
                    processLaunched.TrySetResult();
                });

            HttpStatusCode ProcessPrepareCommand(LaunchPadCommand launchPadCommand) =>
                PrepareCommandHandler(launchPadCommand, launchAsset.Launchables.First(), launchPadStub.Id,
                    LaunchPadState.WaitingForLaunch, () => { launchPadStub.CommandHandler = ProcessLaunchCommand; });

            launchPadStub.CommandHandler = ProcessPrepareCommand;

            // Reference a missing launch complex
            var complexIdentifier = playground.LaunchConfiguration.LaunchComplexes.First().Identifier;
            var wrongIdentifier = Guid.NewGuid();
            playground.LaunchConfiguration.LaunchComplexes.First().Identifier = wrongIdentifier;

            lock (m_LaunchPadsStatusLock)
            {
                m_LaunchTask = m_Manager!.LaunchAsync(playground.Manifest);
            }

            await m_LaunchTask;
            m_LoggerMock.VerifyLog(l => l.LogError("Cannot find LaunchComplex {Id} in the list of current " +
                "LaunchComplexes", wrongIdentifier));
            Assert.That(m_Manager.LaunchPadsCount, Is.EqualTo(1));
            Assert.That(m_Manager.RunningLaunchPads, Is.EqualTo(0));

            playground.LaunchConfiguration.LaunchComplexes.First().Identifier = complexIdentifier;
            lock (m_LaunchPadsStatusLock)
            {
                m_Manager.Conclude(m_LaunchPadsStatus);
            }
            Assert.That(m_Manager.LaunchPadsCount, Is.EqualTo(0));

            // Reference a missing launchpad
            var padIdentifier = playground.LaunchConfiguration.LaunchComplexes.First().LaunchPads.First().Identifier;
            playground.LaunchConfiguration.LaunchComplexes.First().LaunchPads.First().Identifier = wrongIdentifier;

            lock (m_LaunchPadsStatusLock)
            {
                m_LaunchTask = m_Manager.LaunchAsync(playground.Manifest);
            }

            await m_LaunchTask;
            m_LoggerMock.VerifyLog(l => l.LogError("Cannot find LaunchPad {PadId} in the LaunchComplex {ComplexId}",
                wrongIdentifier, complexIdentifier));
            Assert.That(m_Manager.LaunchPadsCount, Is.EqualTo(1));
            Assert.That(m_Manager.RunningLaunchPads, Is.EqualTo(0));

            playground.LaunchConfiguration.LaunchComplexes.First().LaunchPads.First().Identifier = padIdentifier;
            lock (m_LaunchPadsStatusLock)
            {
                m_Manager.Conclude(m_LaunchPadsStatus);
            }
            Assert.That(m_Manager.LaunchPadsCount, Is.EqualTo(0));

            // Missing launchpad status
            LaunchPadStatus rightStatus;
            lock (m_LaunchPadsStatusLock)
            {
                rightStatus = m_LaunchPadsStatus[padIdentifier];
                m_LaunchPadsStatus.Remove(rightStatus.Id);
                m_LaunchPadsStatus.Add(new LaunchPadStatus(wrongIdentifier));

                m_LaunchTask = m_Manager.LaunchAsync(playground.Manifest);
            }

            await m_LaunchTask;
            m_LoggerMock.VerifyLog(l => l.LogError("Cannot find LaunchPad {PadId} status", padIdentifier));
            Assert.That(m_Manager.LaunchPadsCount, Is.EqualTo(1));
            Assert.That(m_Manager.RunningLaunchPads, Is.EqualTo(0));

            lock (m_LaunchPadsStatusLock)
            {
                m_Manager.Conclude(m_LaunchPadsStatus);

                m_LaunchPadsStatus.Remove(wrongIdentifier);
                m_LaunchPadsStatus.Add(rightStatus);
            }
            Assert.That(m_Manager.LaunchPadsCount, Is.EqualTo(0));

            // Bad launchable name
            var orgLaunchableName =
                playground.LaunchConfiguration.LaunchComplexes.First().LaunchPads.First().LaunchableName;
            playground.LaunchConfiguration.LaunchComplexes.First().LaunchPads.First().LaunchableName = "Something else";

            lock (m_LaunchPadsStatusLock)
            {
                m_LaunchTask = m_Manager.LaunchAsync(playground.Manifest);
            }

            await m_LaunchTask;
            m_LoggerMock.VerifyLog(l => l.LogError("Cannot find any launchable to launch on {PadId}", padIdentifier));
            Assert.That(m_Manager.LaunchPadsCount, Is.EqualTo(1));
            Assert.That(m_Manager.RunningLaunchPads, Is.EqualTo(0));

            playground.LaunchConfiguration.LaunchComplexes.First().LaunchPads.First().LaunchableName = orgLaunchableName;
            lock (m_LaunchPadsStatusLock)
            {
                m_Manager.Conclude(m_LaunchPadsStatus);
            }
            Assert.That(m_Manager.LaunchPadsCount, Is.EqualTo(0));

            // No compatible launchable to launch on node
            var orgSuitableFor = playground.LaunchComplexes.First().LaunchPads.First().SuitableFor;
            playground.LaunchComplexes.First().LaunchPads.First().SuitableFor = new[] { "Fictive" };

            lock (m_LaunchPadsStatusLock)
            {
                m_LaunchTask = m_Manager.LaunchAsync(playground.Manifest);
            }

            await m_LaunchTask;
            m_LoggerMock.VerifyLog(l => l.LogError("Cannot find any launchable to launch on {PadId}", padIdentifier));
            Assert.That(m_Manager.LaunchPadsCount, Is.EqualTo(1));
            Assert.That(m_Manager.RunningLaunchPads, Is.EqualTo(0));

            playground.LaunchComplexes.First().LaunchPads.First().SuitableFor = orgSuitableFor;
            lock (m_LaunchPadsStatusLock)
            {
                m_Manager.Conclude(m_LaunchPadsStatus);
            }
            Assert.That(m_Manager.LaunchPadsCount, Is.EqualTo(0));

            // Ok, now let's check that after all those wrong configurations the manager is still in a good state and
            // can still launch.
            lock (m_LaunchPadsStatusLock)
            {
                m_LaunchTask = m_Manager.LaunchAsync(playground.Manifest);
            }

            Task timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            var finishedTask = await Task.WhenAny(processLaunched.Task, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(processLaunched.Task)); // Otherwise timeout

            Assert.That(m_Manager.LaunchPadsCount, Is.EqualTo(1));
            Assert.That(m_Manager.RunningLaunchPads, Is.EqualTo(1));

            // While at it, try to conclude something that is still launched, should fail
            Assert.That(m_LaunchTask.IsCompleted, Is.False);
            lock (m_LaunchPadsStatusLock)
            {
                Assert.Throws<InvalidOperationException>(() => m_Manager.Conclude(m_LaunchPadsStatus));
            }

            // Let's finalize everything
            TaskCompletionSource abortReceivedByLaunchPad = new(TaskCreationOptions.RunContinuationsAsynchronously);
            launchPadStub.CommandHandler = (launchPadCommand) =>
                TypeCommandHandler<LaunchPadAbortCommand>(launchPadCommand, launchPadStub.Id, LaunchPadState.Idle,
                () => {
                    abortReceivedByLaunchPad.TrySetResult();
                });

            m_Manager.Stop();

            timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            finishedTask = await Task.WhenAny(m_LaunchTask, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(m_LaunchTask)); // Otherwise timeout
            finishedTask = await Task.WhenAny(abortReceivedByLaunchPad.Task, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(abortReceivedByLaunchPad.Task)); // Otherwise timeout

            Assert.That(m_Manager.LaunchPadsCount, Is.EqualTo(1));
            Assert.That(m_Manager.RunningLaunchPads, Is.EqualTo(0));

            lock (m_LaunchPadsStatusLock)
            {
                m_Manager.Conclude(m_LaunchPadsStatus);
            }
            Assert.That(m_Manager.LaunchPadsCount, Is.EqualTo(0));
        }

        [Test]
        public async Task RecoverUndefinedStatus()
        {
            var playground = PreparePlayground(1);
            playground.PrepareLaunchPadsStatus(m_LaunchPadsStatus);

            var launchPadStub = playground.LaunchPadStubs.First();
            var launchAsset = playground.LaunchedAsset;

            TaskCompletionSource processLaunched = new(TaskCreationOptions.RunContinuationsAsynchronously);

            HttpStatusCode ProcessLaunchCommand(LaunchPadCommand launchPadCommand) =>
                TypeCommandHandler<LaunchPadLaunchCommand>(launchPadCommand, launchPadStub.Id, LaunchPadState.Launched, () =>
                {
                    launchPadStub.CommandHandler = null;
                    processLaunched.TrySetResult();
                });

            TaskCompletionSource gettingPayloadTask = new(TaskCreationOptions.RunContinuationsAsynchronously);

            HttpStatusCode ProcessPrepareCommand(LaunchPadCommand launchPadCommand) =>
                PrepareCommandHandler(launchPadCommand, launchAsset.Launchables.First(), launchPadStub.Id,
                    LaunchPadState.GettingPayload, () =>
                {
                    launchPadStub.CommandHandler = ProcessLaunchCommand;
                    gettingPayloadTask.TrySetResult();
                });

            launchPadStub.CommandHandler = ProcessPrepareCommand;

            lock (m_LaunchPadsStatusLock)
            {
                m_LaunchTask = m_Manager!.LaunchAsync(playground.Manifest);
            }

            // Wait for the launchpad to enter GettingPayload state.
            Task timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            var finishedTask = await Task.WhenAny(gettingPayloadTask.Task, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(gettingPayloadTask.Task)); // Otherwise timeout

            // Set the state of the launchpad as undefined for 1 second
            LaunchPadStatus savedStatus;
            lock (m_LaunchPadsStatusLock)
            {
                savedStatus = m_LaunchPadsStatus[launchPadStub.Id].DeepClone();
                m_LaunchPadsStatus[launchPadStub.Id].IsDefined = false;
                m_LaunchPadsStatus[launchPadStub.Id].UpdateError = "Testing undefined state";
                m_LaunchPadsStatus[launchPadStub.Id].State = LaunchPadState.Over;
                m_LaunchPadsStatus[launchPadStub.Id].SignalChanges(m_LaunchPadsStatus);
            }

            await Task.Delay(TimeSpan.FromSeconds(1));

            lock (m_LaunchPadsStatusLock)
            {
                m_LaunchPadsStatus[launchPadStub.Id].DeepCopyFrom(savedStatus);
                m_LaunchPadsStatus[launchPadStub.Id].State = LaunchPadState.WaitingForLaunch;
                m_LaunchPadsStatus[launchPadStub.Id].SignalChanges(m_LaunchPadsStatus);
            }

            // Verify we can continue the launch
            timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            finishedTask = await Task.WhenAny(processLaunched.Task, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(processLaunched.Task)); // Otherwise timeout
        }

        [Test]
        public async Task UndefinedStatus()
        {
            var playground = PreparePlayground(2 /*launchpads*/, 5 /*launchpad timeout sec*/);
            playground.PrepareLaunchPadsStatus(m_LaunchPadsStatus);

            var launchPad1Stub = playground.LaunchPadStubs.ElementAt(0);
            var launchPad2Stub = playground.LaunchPadStubs.ElementAt(1);
            var launchAsset = playground.LaunchedAsset;

            // Setup launchpad 1, this one will succeed it launch
            TaskCompletionSource launchPad1Launched = new(TaskCreationOptions.RunContinuationsAsynchronously);

            HttpStatusCode ProcessLaunchCommand1(LaunchPadCommand launchPadCommand) =>
                TypeCommandHandler<LaunchPadLaunchCommand>(launchPadCommand, launchPad1Stub.Id, LaunchPadState.Launched, () =>
                {
                    launchPad1Stub.CommandHandler = null;
                    launchPad1Launched.TrySetResult();
                });

            HttpStatusCode ProcessPrepareCommand(LaunchPadCommand launchPadCommand) =>
                PrepareCommandHandler(launchPadCommand, launchAsset.Launchables.First(), launchPad1Stub.Id,
                    LaunchPadState.WaitingForLaunch, () => { launchPad1Stub.CommandHandler = ProcessLaunchCommand1; });

            launchPad1Stub.CommandHandler = ProcessPrepareCommand;

            // Setup launchpad 2, this one will stop responding and will be dropped
            TaskCompletionSource launchPad2GettingPayload = new(TaskCreationOptions.RunContinuationsAsynchronously);

            HttpStatusCode ProcessPrepareCommand2(LaunchPadCommand launchPadCommand) =>
                PrepareCommandHandler(launchPadCommand, launchAsset.Launchables.First(), launchPad2Stub.Id,
                    LaunchPadState.GettingPayload, () =>
                {
                    launchPad2Stub.CommandHandler = null;
                    launchPad2GettingPayload.TrySetResult();
                });

            launchPad2Stub.CommandHandler = ProcessPrepareCommand2;

            Stopwatch elapsedSinceLaunched;
            lock (m_LaunchPadsStatusLock)
            {
                elapsedSinceLaunched = Stopwatch.StartNew();
                m_LaunchTask = m_Manager!.LaunchAsync(playground.Manifest);
            }

            Assert.That(m_Manager.LaunchPadsCount, Is.EqualTo(2));
            Assert.That(m_Manager.RunningLaunchPads, Is.EqualTo(2));

            // Wait for launchpad 2 to enter GettingPayload state.
            Task timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            var finishedTask = await Task.WhenAny(launchPad2GettingPayload.Task, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(launchPad2GettingPayload.Task)); // Otherwise timeout

            // Set the state of the launchpad as undefined
            lock (m_LaunchPadsStatusLock)
            {
                m_LaunchPadsStatus[launchPad2Stub.Id].IsDefined = false;
                m_LaunchPadsStatus[launchPad2Stub.Id].UpdateError = "Testing undefined state";
                m_LaunchPadsStatus[launchPad2Stub.Id].SignalChanges(m_LaunchPadsStatus);
            }

            // Launchpad 1 shouldn't be launched yet as it is still waiting for launchpad 2
            Assert.That(launchPad1Launched.Task.IsCompleted, Is.False);

            // Continue waiting and eventually launchpad 2 undefined state will cause a timeout and we will proceed
            // with the launch of launchpad 1.
            timeoutTask = Task.Delay(TimeSpan.FromSeconds(10));
            finishedTask = await Task.WhenAny(launchPad1Launched.Task, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(launchPad1Launched.Task)); // Otherwise timeout

            m_LoggerMock.VerifyLog(l => l.LogError("LaunchPad {PadId} state is undefined for too long {Elapsed}*",
                launchPad2Stub.Id, It.IsAny<TimeSpan>()));
            Assert.That(elapsedSinceLaunched.Elapsed,
                Is.GreaterThan(TimeSpan.FromSeconds(playground.Manifest.Config.LaunchPadFeedbackTimeoutSec)));
            Assert.That(m_Manager.LaunchPadsCount, Is.EqualTo(2));
            Assert.That(m_Manager.RunningLaunchPads, Is.EqualTo(1));
        }

        [Test]
        public async Task DropLaunchPadOnUnexpectedStateChange()
        {
            var playground = PreparePlayground(2);
            playground.PrepareLaunchPadsStatus(m_LaunchPadsStatus);

            var launchPad1Stub = playground.LaunchPadStubs.ElementAt(0);
            var launchPad2Stub = playground.LaunchPadStubs.ElementAt(1);
            var launchAsset = playground.LaunchedAsset;

            // Setup launchpad 1 so that it succeeds its launch
            TaskCompletionSource launchPad1Launched = new(TaskCreationOptions.RunContinuationsAsynchronously);

            HttpStatusCode ProcessLaunchCommand1(LaunchPadCommand launchPadCommand) =>
                TypeCommandHandler<LaunchPadLaunchCommand>(launchPadCommand, launchPad1Stub.Id, LaunchPadState.Launched, () =>
                {
                    launchPad1Stub.CommandHandler = null;
                    launchPad1Launched.TrySetResult();
                });

            HttpStatusCode ProcessPrepareCommand1(LaunchPadCommand launchPadCommand) =>
                PrepareCommandHandler(launchPadCommand, launchAsset.Launchables.First(), launchPad1Stub.Id,
                    LaunchPadState.WaitingForLaunch, () => { launchPad1Stub.CommandHandler = ProcessLaunchCommand1; });

            launchPad1Stub.CommandHandler = ProcessPrepareCommand1;

            // Setup launchpad 2 so that it reaches running pre-launch.
            TaskCompletionSource launchPad2ReachedPreLaunch = new(TaskCreationOptions.RunContinuationsAsynchronously);

            HttpStatusCode ProcessPrepareCommand2(LaunchPadCommand launchPadCommand) =>
                PrepareCommandHandler(launchPadCommand, launchAsset.Launchables.First(), launchPad2Stub.Id,
                    LaunchPadState.PreLaunch, () =>
                {
                    launchPad2Stub.CommandHandler = null;
                    launchPad2ReachedPreLaunch.TrySetResult();
                });

            launchPad2Stub.CommandHandler = ProcessPrepareCommand2;

            // Launch everything
            lock (m_LaunchPadsStatusLock)
            {
                m_LaunchTask = m_Manager!.LaunchAsync(playground.Manifest);
            }

            // Wait for launchpad 2 to start executing pre-launch
            Task timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            var finishedTask = await Task.WhenAny(launchPad2ReachedPreLaunch.Task, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(launchPad2ReachedPreLaunch.Task)); // Otherwise timeout

            // Launchpad 1 shouldn't be launched yet as it is still waiting for launchpad 2
            Assert.That(launchPad1Launched.Task.IsCompleted, Is.False);

            Assert.That(m_Manager.LaunchPadsCount, Is.EqualTo(2));
            Assert.That(m_Manager.RunningLaunchPads, Is.EqualTo(2));

            // Simulate someone else messing up with the state of launchpad 2 (moving back one step)
            lock (m_LaunchPadsStatusLock)
            {
                m_LaunchPadsStatus[launchPad2Stub.Id].State = LaunchPadState.GettingPayload;
                m_LaunchPadsStatus[launchPad2Stub.Id].SignalChanges(m_LaunchPadsStatus);
            }

            // The above state change should have triggered the LaunchManager to stop waiting for launchpad 2 and
            // proceed with launching launchpad 1.
            timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            finishedTask = await Task.WhenAny(launchPad1Launched.Task, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(launchPad1Launched.Task)); // Otherwise timeout

            m_LoggerMock.VerifyLog(l => l.LogError("LaunchPad {PadId} state has moved backward, from {From} to {To}, " +
                "could the LaunchPad be used by another MissionControl?", launchPad2Stub.Id, LaunchPadState.PreLaunch,
                LaunchPadState.GettingPayload));
            Assert.That(m_Manager.LaunchPadsCount, Is.EqualTo(2));
            Assert.That(m_Manager.RunningLaunchPads, Is.EqualTo(1));
        }

        [Test]
        public async Task FromNotIdle()
        {
            var playground = PreparePlayground(2);
            playground.PrepareLaunchPadsStatus(m_LaunchPadsStatus);

            var launchPad1Stub = playground.LaunchPadStubs.ElementAt(0);
            var launchPad2Stub = playground.LaunchPadStubs.ElementAt(1);
            var launchAsset = playground.LaunchedAsset;

            // Node 1 will be in a non idle state and require an abort before starting up
            TaskCompletionSource launchPad1Launched = new(TaskCreationOptions.RunContinuationsAsynchronously);

            HttpStatusCode ProcessLaunchCommand1(LaunchPadCommand launchPadCommand) =>
                TypeCommandHandler<LaunchPadLaunchCommand>(launchPadCommand, launchPad1Stub.Id, LaunchPadState.Launched, () =>
                {
                    launchPad1Stub.CommandHandler = null;
                    launchPad1Launched.TrySetResult();
                });

            HttpStatusCode ProcessPrepareCommand1(LaunchPadCommand launchPadCommand) =>
                PrepareCommandHandler(launchPadCommand, launchAsset.Launchables.First(), launchPad1Stub.Id,
                    LaunchPadState.WaitingForLaunch, () => { launchPad1Stub.CommandHandler = ProcessLaunchCommand1; });

            HttpStatusCode AbortCommand1(LaunchPadCommand launchPadCommand) =>
                TypeCommandHandler<LaunchPadAbortCommand>(launchPadCommand, launchPad1Stub.Id, LaunchPadState.Idle, () =>
                {
                    launchPad1Stub.CommandHandler = ProcessPrepareCommand1;
                    launchPad1Launched.TrySetResult();
                });

            launchPad1Stub.CommandHandler = AbortCommand1;

            lock (m_LaunchPadsStatusLock)
            {
                m_LaunchPadsStatus[launchPad1Stub.Id].State = LaunchPadState.Launched;
                m_LaunchPadsStatus[launchPad1Stub.Id].SignalChanges(m_LaunchPadsStatus);
            }

            // Node 2 will be in idle start so it can start "normally"
            TaskCompletionSource launchPad2Launched = new(TaskCreationOptions.RunContinuationsAsynchronously);

            HttpStatusCode ProcessLaunchCommand2(LaunchPadCommand launchPadCommand) =>
                TypeCommandHandler<LaunchPadLaunchCommand>(launchPadCommand, launchPad2Stub.Id, LaunchPadState.Launched, () =>
                {
                    launchPad2Stub.CommandHandler = null;
                    launchPad2Launched.TrySetResult();
                });

            HttpStatusCode ProcessPrepareCommand2(LaunchPadCommand launchPadCommand) =>
                PrepareCommandHandler(launchPadCommand, launchAsset.Launchables.First(), launchPad2Stub.Id,
                    LaunchPadState.WaitingForLaunch, () => { launchPad2Stub.CommandHandler = ProcessLaunchCommand2; });

            launchPad2Stub.CommandHandler = ProcessPrepareCommand2;

            lock (m_LaunchPadsStatusLock)
            {
                m_LaunchTask = m_Manager!.LaunchAsync(playground.Manifest);
            }

            // Wait for the LaunchPads to be requested to launch
            Task timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            var finishedTask = await Task.WhenAny(launchPad1Launched.Task, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(launchPad1Launched.Task)); // Otherwise timeout
            finishedTask = await Task.WhenAny(launchPad2Launched.Task, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(launchPad2Launched.Task)); // Otherwise timeout
        }

        [Test]
        public async Task SelfStop()
        {
            var playground = PreparePlayground(1);
            playground.PrepareLaunchPadsStatus(m_LaunchPadsStatus);

            var launchPadStub = playground.LaunchPadStubs.First();
            var launchAsset = playground.LaunchedAsset;

            TaskCompletionSource processLaunched = new(TaskCreationOptions.RunContinuationsAsynchronously);

            HttpStatusCode ProcessLaunchCommand(LaunchPadCommand launchPadCommand) =>
                TypeCommandHandler<LaunchPadLaunchCommand>(launchPadCommand, launchPadStub.Id, LaunchPadState.Launched, () =>
                {
                    launchPadStub.CommandHandler = null;
                    processLaunched.TrySetResult();
                });

            HttpStatusCode ProcessPrepareCommand(LaunchPadCommand launchPadCommand) =>
                PrepareCommandHandler(launchPadCommand, launchAsset.Launchables.First(), launchPadStub.Id,
                    LaunchPadState.WaitingForLaunch, () => { launchPadStub.CommandHandler = ProcessLaunchCommand; });

            launchPadStub.CommandHandler = ProcessPrepareCommand;

            lock (m_LaunchPadsStatusLock)
            {
                m_LaunchTask = m_Manager!.LaunchAsync(playground.Manifest);
            }

            // Wait for the LaunchPad to be requested to launch
            Task timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            var finishedTask = await Task.WhenAny(processLaunched.Task, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(processLaunched.Task)); // Otherwise timeout

            // LaunchPad launched, but the launch task from LaunchAsync should still be running
            Assert.That(m_LaunchTask.IsCompleted, Is.False);

            Assert.That(m_Manager.LaunchPadsCount, Is.EqualTo(1));
            Assert.That(m_Manager.RunningLaunchPads, Is.EqualTo(1));

            // Have the launchpad transition himself to finished
            lock (m_LaunchPadsStatusLock)
            {
                m_LaunchPadsStatus[launchPadStub.Id].State = LaunchPadState.Over;
                m_LaunchPadsStatus[launchPadStub.Id].SignalChanges(m_LaunchPadsStatus);
            }

            // So now the launch should finish
            timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            finishedTask = await Task.WhenAny(m_LaunchTask, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(m_LaunchTask)); // Otherwise timeout

            Assert.That(m_Manager.LaunchPadsCount, Is.EqualTo(1));
            Assert.That(m_Manager.RunningLaunchPads, Is.EqualTo(0));

            // And we should be able to conclude the launch manager's launch
            lock (m_LaunchPadsStatusLock)
            {
                m_Manager.Conclude(m_LaunchPadsStatus);
            }
        }

        class Playground
        {
            public List<LaunchPadStub> LaunchPadStubs { get; set; } = new();
            public Asset LaunchedAsset { get; set; } = new(Guid.Empty);
            public List<LaunchComplex> LaunchComplexes { get; set; } = new();
            public LaunchConfiguration LaunchConfiguration { get; set; } = new();

            public void PrepareLaunchPadsStatus(IncrementalCollection<LaunchPadStatus> statuses)
            {
                foreach (var launchPadStub in LaunchPadStubs)
                {
                    statuses.Add(new LaunchPadStatus(launchPadStub.Id) { IsDefined = true });
                }
            }

            public LaunchManager.LaunchManifest Manifest { get; set; } = new();
        }

        Playground PreparePlayground(int nbrLaunchPads, float launchPadFeedbackTimeoutSec = 30)
        {
            Playground ret = new();

            for (int launchPadIdx = 0; launchPadIdx < nbrLaunchPads; ++launchPadIdx)
            {
                ret.LaunchPadStubs.Add(AddLaunchPad(8200 + launchPadIdx));
            }

            ret.LaunchedAsset = new(Guid.NewGuid())
            {
                Name = "Test asset",
                Launchables = new[]
                {
                    new Launchable()
                    {
                        Name = "LaunchableName",
                        Type = "ClusterNode",
                        PreLaunchPath = "PreLaunch.ps1",
                        LaunchPath = "Launch.exe",
                        Payloads = new[] { Guid.NewGuid() }
                    }
                }
            };

            var launchComplexId = Guid.NewGuid();
            ret.LaunchComplexes = new[]
            {
                new LaunchComplex(launchComplexId)
                {
                    LaunchPads = ret.LaunchPadStubs.Select(lps => new LaunchPad() {
                        Identifier = lps.Id, Endpoint = lps.EndPoint, SuitableFor = new[] { "ClusterNode" }}).ToList()
                }
            }.ToList();

            ret.LaunchConfiguration = new()
            {
                AssetId = ret.LaunchedAsset.Id,
                LaunchComplexes = new[]
                {
                    new LaunchComplexConfiguration()
                    {
                        Identifier = launchComplexId,
                        LaunchPads = ret.LaunchPadStubs.Select(
                            lps => new LaunchPadConfiguration() { Identifier = lps.Id, LaunchableName = "LaunchableName" }).ToList()
                    }
                }
            };

            ret.Manifest = new()
            {
                LaunchConfiguration = ret.LaunchConfiguration,
                Asset = ret.LaunchedAsset,
                Complexes = ret.LaunchComplexes,
                LaunchPadsStatus = m_LaunchPadsStatus,
                Config = new()
                {
                    LaunchPadsEntry = m_MissionControlEndPoint,
                    LaunchPadFeedbackTimeoutSec = launchPadFeedbackTimeoutSec
                }
            };

            return ret;
        }

        HttpStatusCode TypeCommandHandler<T>(LaunchPadCommand launchPadCommand, Guid launchPadId,
            LaunchPadState newState, Action then) where T : LaunchPadCommand
        {
            if (launchPadCommand is T)
            {
                lock (m_LaunchPadsStatusLock)
                {
                    m_LaunchPadsStatus[launchPadId].State = newState;
                    m_LaunchPadsStatus[launchPadId].SignalChanges(m_LaunchPadsStatus);
                }
                then();
                return HttpStatusCode.OK;
            }
            else
            {
                return HttpStatusCode.BadRequest;
            }
        }

        HttpStatusCode PrepareCommandHandler(LaunchPadCommand launchPadCommand, Launchable launchable, Guid launchPadId,
            LaunchPadState newState, Action then)
        {
            if (launchPadCommand is LaunchPadPrepareCommand prepareCommand &&
                prepareCommand.PayloadIds.SequenceEqual(launchable.Payloads) &&
                prepareCommand.PayloadSource != null && prepareCommand.PayloadSource.Equals(m_MissionControlEndPoint) &&
                prepareCommand.PreLaunchPath == launchable.PreLaunchPath &&
                prepareCommand.LaunchPath == launchable.LaunchPath)
            {
                lock (m_LaunchPadsStatusLock)
                {
                    m_LaunchPadsStatus[launchPadId].State = newState;
                    m_LaunchPadsStatus[launchPadId].SignalChanges(m_LaunchPadsStatus);
                }
                then();
                return HttpStatusCode.OK;
            }
            else
            {
                return HttpStatusCode.BadRequest;
            }
        }

        LaunchPadStub AddLaunchPad(int port)
        {
            LaunchPadStub ret = new(port);
            ret.Start();
            m_LaunchPads.Add(ret);
            return ret;
        }

        readonly Uri m_MissionControlEndPoint = new("http://127.0.0.1:8000/");

        Mock<ILogger> m_LoggerMock = new();
        HttpClient m_HttpClient = new();
        List<LaunchPadStub> m_LaunchPads = new();

        object m_LaunchPadsStatusLock = new();
        IncrementalCollection<LaunchPadStatus> m_LaunchPadsStatus = new();

        LaunchManager? m_Manager;
        Task? m_LaunchTask;
    }
}
