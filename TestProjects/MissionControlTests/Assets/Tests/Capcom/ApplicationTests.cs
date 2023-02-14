using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.ClusterDisplay.MissionControl.Capcom
{
    public class ApplicationTests
    {
        [SetUp]
        public void SetUp()
        {
            m_MissionControlStub.Start();
            m_MissionControlStub.LaunchComplexes.Clear();
            m_MissionControlStub.LaunchConfiguration = new();
            m_MissionControlStub.CapcomUplink = new() {IsRunning = true};

            m_Application = new (MissionControlStub.HttpListenerEndpoint, new());
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            m_MissionControlStub.CapcomUplink = new() {IsRunning = false};
            EnumeratorTimeout timeout = new(TimeSpan.FromSeconds(5));
            bool wasAlreadyFinished = true;
            if (m_AppRunningTask != null)
            {
                wasAlreadyFinished = m_AppRunningTask.IsCompleted;
                yield return m_AppRunningTask.AsIEnumerator(timeout);
                m_AppRunningTask = null;
                m_Application = null;
            }

            m_MissionControlStub.Stop();

            // We make these asserts last we want to be sure we do m_MissionControlStub.Stop as otherwise the port might
            // be kept open...
            Assert.That(wasAlreadyFinished, Is.False);
            Assert.That(timeout.TimedOut, Is.False);
        }

        [UnityTest]
        public IEnumerator UpdatedObjectSingleChange()
        {
            StartApplication();

            // So that appRunningTask has the time to conclude in the case the app would not wait for the exit signal
            yield return Task.Delay(25).AsIEnumerator();

            // The rest of the test is done in TearDown that tries to stop the app
        }

        [UnityTest]
        public IEnumerator UpdatedObjectMultipleChanges()
        {
            StartApplication();

            // Make an unrelated change to capcom uplink to be sure it support multiple changes and does not shutdown
            // as soon as something happens.
            m_MissionControlStub.CapcomUplink = new() {IsRunning = true, ProceedWithLanding = true};

            // So that appRunningTask has the time to conclude in the case the app would not wait for the exit signal
            yield return Task.Delay(25).AsIEnumerator();

            // The rest of the test is done in TearDown that tries to stop the app
        }

        [UnityTest]
        public IEnumerator ComplexesChange()
        {
            GetComplexesProcess complexesAccessor = new();
            m_Application.AddProcess(complexesAccessor);
            StartApplication();

            var launchComplex1Id = Guid.NewGuid();
            m_MissionControlStub.LaunchComplexes.Add(new(launchComplex1Id));

            yield return complexesAccessor.WaitFor(list => list.Count == 1 && list[0].Id == launchComplex1Id);

            var launchComplex2Id = Guid.NewGuid();
            m_MissionControlStub.LaunchComplexes.Add(new(launchComplex2Id));

            yield return complexesAccessor.WaitFor(list => list.Count == 2);
            Assert.That(complexesAccessor.Contains(launchComplex1Id));
            Assert.That(complexesAccessor.Contains(launchComplex2Id));

            var launchComplex3Id = Guid.NewGuid();
            m_MissionControlStub.LaunchComplexes.Add(new(launchComplex3Id));

            yield return complexesAccessor.WaitFor(list => list.Count == 3);
            Assert.That(complexesAccessor.Contains(launchComplex1Id));
            Assert.That(complexesAccessor.Contains(launchComplex2Id));
            Assert.That(complexesAccessor.Contains(launchComplex3Id));

            m_MissionControlStub.LaunchComplexes.Remove(launchComplex2Id);
            yield return complexesAccessor.WaitFor(list => list.Count == 2);
            Assert.That(complexesAccessor.Contains(launchComplex1Id));
            Assert.That(complexesAccessor.Contains(launchComplex3Id));

            m_MissionControlStub.LaunchComplexes.Clear();
            yield return complexesAccessor.WaitFor(list => list.Count == 0);
        }

        class GetComplexesProcess: IApplicationProcess
        {
            public void Process(MissionControlMirror missionControlMirror)
            {
                lock (m_Lock)
                {
                    Complexes = missionControlMirror.Complexes.Values.Select(c => c.DeepClone()).ToList();
                }
            }

            public IEnumerator WaitFor(Func<List<MissionControl.LaunchComplex>, bool> predicate)
            {
                var stopwatch = Stopwatch.StartNew();
                bool success = false;
                while (stopwatch.Elapsed < TimeSpan.FromSeconds(5))
                {
                    lock (m_Lock)
                    {
                        if (predicate(Complexes))
                        {
                            success = true;
                            break;
                        }
                    }
                    yield return null;
                }
                Assert.That(success, Is.True);
            }

            public bool Contains(Guid id)
            {
                lock (m_Lock)
                {
                    return Complexes.Any(c => c.Id == id);
                }
            }

            object m_Lock = new();
            List<MissionControl.LaunchComplex> Complexes { get; set; } = new();
        }

        void StartApplication()
        {
            m_AppRunningTask = m_Application.Start();
        }

        Application m_Application;
        Task m_AppRunningTask;
        MissionControlStub m_MissionControlStub = new();
    }
}
