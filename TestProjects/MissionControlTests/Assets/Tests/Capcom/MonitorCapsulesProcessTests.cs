using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NUnit.Framework;
using Unity.ClusterDisplay.MissionControl.Capsule;
using Unity.ClusterDisplay.MissionControl.MissionControl;
using UnityEngine;
using UnityEngine.TestTools;
using State = Unity.ClusterDisplay.MissionControl.LaunchPad.State;

namespace Unity.ClusterDisplay.MissionControl.Capcom
{
    public class MonitorCapsulesProcessTests
    {
        [SetUp]
        public void SetUp()
        {
            m_MissionControlStub.Start();
            m_Application = new (MissionControlStub.HttpListenerEndpoint, new());

            m_MonitorCapsulesProcessStop = new();
            m_MonitorCapsulesProcess = new(m_MonitorCapsulesProcessStop.Token, m_Application);

            m_DynamicEntries.Clear();
            m_MissionControlStub.FallbackHandler = (uri, request, response) =>
            {
                if (uri.StartsWith("api/v1/launchPadsStatus/" ) && uri.EndsWith("/dynamicEntries") &&
                    request.HttpMethod == HttpMethod.Put.ToString() && request.ContentType == "application/json")
                {
                    string dynamicEntriesJson;
                    using (StreamReader reader = new(request.InputStream))
                    {
                        dynamicEntriesJson = reader.ReadToEnd();
                    }
                    Guid launchPadId = Guid.Parse(uri.Split("/")[3]);
                    lock (m_DynamicEntries)
                    {
                        m_DynamicEntries[launchPadId] = JsonConvert.DeserializeObject<LaunchPadReportDynamicEntry[]>(
                            dynamicEntriesJson, Json.SerializerOptions);
                    }
                    MissionControlStub.Respond(response, HttpStatusCode.OK);
                }
                else
                {
                    MissionControlStub.Respond(response, HttpStatusCode.NotFound);
                }
            };

            m_AppRunningTask = m_Application.Start(false);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            m_MonitorCapsulesProcessStop.Cancel();
            m_Application.ManualStop();

            // Wait for all connections to close
            var waitingShutdownTime = Stopwatch.StartNew();
            while (m_MonitorCapsulesProcess.ConnectionCount != 0 || !m_AppRunningTask.IsCompleted)
            {
                if (waitingShutdownTime.Elapsed > TimeSpan.FromSeconds(5))
                {
                    break;
                }
                yield return null;
            }
            // Check MonitorCapsulesProcess.MonitorLoop reach the end of the method if the following asset fails
            Assert.That(m_MonitorCapsulesProcess.ConnectionCount, Is.EqualTo(0));
            // Check that Application.ProcessingLoop reach the end of the method if the following asset fails.
            Assert.That(m_AppRunningTask.IsCompleted);

            m_MissionControlStub.Stop();

            foreach (var capsule in m_Capsules)
            {
                capsule.ProcessingLoop.Stop();
                yield return capsule.ProcessingTask.AsIEnumerator();
            }
            m_Capsules.Clear();
        }

        [UnityTest]
        public IEnumerator Normal()
        {
            AddFakeCapsule(k_TestPort+0);
            AddFakeCapsule(k_TestPort+1);

            // Start with an empty list
            List<LaunchPadInformation> launchPadsInformation = new(m_Mirror.LaunchPadsInformation);
            m_Mirror.LaunchPadsInformation.Clear();
            var launchPad0Id = launchPadsInformation[0].Definition.Identifier;
            var launchPad1Id = launchPadsInformation[1].Definition.Identifier;

            // First, process with an empty list
            m_MonitorCapsulesProcess.Process(m_Mirror);

            // No connection should be done, so capsule state change should go un-noticed
            m_Capsules[0].FakeStatusChange(0, 0);
            m_Capsules[1].FakeStatusChange(0, 1);

            // I know its is not supposed to do anything, but still wait a little bit just in case it would be done in
            // the background.
            yield return Task.Delay(TimeSpan.FromMilliseconds(250)).AsIEnumerator();
            Assert.That(m_MonitorCapsulesProcess.ConnectionCount, Is.EqualTo(0));
            Assert.That(m_DynamicEntries, Is.Empty);

            // Connect the first capsule
            m_Mirror.LaunchPadsInformation.Add(launchPadsInformation[1]);
            m_MonitorCapsulesProcess.Process(m_Mirror);

            // Consume the initial status update
            yield return WaitForUpdates(m_DynamicEntries, 1);
            m_DynamicEntries.Clear();
            Assert.That(m_MonitorCapsulesProcess.ConnectionCount, Is.EqualTo(1));

            // Send updates
            m_Capsules[0].FakeStatusChange(0, 2);
            m_Capsules[1].FakeStatusChange(1, 3);

            // Wait until MissionControl receive those updates
            yield return WaitForUpdates(m_DynamicEntries, 1);
            TestEntry(m_DynamicEntries, launchPad1Id, 1, 3);
            m_DynamicEntries.Clear();

            // Add a second launchpad to be monitored
            m_Mirror.LaunchPadsInformation.Insert(0, launchPadsInformation[0]);
            m_MonitorCapsulesProcess.Process(m_Mirror);

            // Consume the initial status update
            yield return WaitForUpdates(m_DynamicEntries, 1);
            m_DynamicEntries.Clear();
            Assert.That(m_MonitorCapsulesProcess.ConnectionCount, Is.EqualTo(2));

            // Send updates
            m_Capsules[0].FakeStatusChange(0, 4);
            m_Capsules[1].FakeStatusChange(1, 5);

            // Wait until MissionControl receive those updates
            yield return WaitForUpdates(m_DynamicEntries, 2);
            TestEntry(m_DynamicEntries, launchPad0Id, 0, 4);
            TestEntry(m_DynamicEntries, launchPad1Id, 1, 5);
            m_DynamicEntries.Clear();

            // Shutdown a capsule (we need to remove it before removing the launchpad capcom should always outlive
            // the capsule (else there would be some error messages to deal with in the test).
            m_Capsules[0].ProcessingLoop.Stop();
            yield return m_Capsules[0].ProcessingTask.AsIEnumerator();

            // Remove a launchpad
            m_Mirror.LaunchPadsInformation.RemoveAt(0);
            m_MonitorCapsulesProcess.Process(m_Mirror);
            yield return WaitForConnectionCount(1);

            // Send updates
            m_Capsules[1].FakeStatusChange(1, 6);

            // Wait until MissionControl receive those updates
            yield return WaitForUpdates(m_DynamicEntries, 1);
            TestEntry(m_DynamicEntries, launchPad1Id, 1, 6);
        }

        [UnityTest]
        public IEnumerator BrokenConnection()
        {
            AddFakeCapsule(k_TestPort + 0);
            AddFakeCapsule(k_TestPort + 1);

            var launchPad0Id = m_Mirror.LaunchPadsInformation[0].Definition.Identifier;
            var launchPad1Id = m_Mirror.LaunchPadsInformation[1].Definition.Identifier;

            // Make the initial connection
            m_MonitorCapsulesProcess.Process(m_Mirror);

            // Consume the initial status updates
            yield return WaitForUpdates(m_DynamicEntries, 2);
            m_DynamicEntries.Clear();
            Assert.That(m_MonitorCapsulesProcess.ConnectionCount, Is.EqualTo(2));

            // Send first update
            m_Capsules[0].FakeStatusChange(0, 0);
            m_Capsules[1].FakeStatusChange(1, 1);

            yield return WaitForUpdates(m_DynamicEntries, 2);
            TestEntry(m_DynamicEntries, launchPad0Id, 0, 0);
            TestEntry(m_DynamicEntries, launchPad1Id, 1, 1);
            m_DynamicEntries.Clear();

            // Disconnect one of the capsules
            m_Capsules[1].ProcessingLoop.Stop();
            yield return m_Capsules[1].ProcessingTask.AsIEnumerator();

            // Wait until capcom realize the capsule is gone
            yield return WaitForConnectionCount(1);

            // Test updates of the other capsule still work fine
            m_Capsules[0].FakeStatusChange(0, 2);
            yield return WaitForUpdates(m_DynamicEntries, 1);
            TestEntry(m_DynamicEntries, launchPad0Id, 0, 2);
            m_DynamicEntries.Clear();

            // Reconnect the capsule
            m_Capsules[1] = new(k_TestPort + 1);

            // Consume the initial status updates that should be send when the connection is done to the replacement
            // capsule.
            yield return WaitForUpdates(m_DynamicEntries, 1);
            m_DynamicEntries.Clear();
            Assert.That(m_MonitorCapsulesProcess.ConnectionCount, Is.EqualTo(2));

            // Send a new update to validate everything is now back to normal
            m_Capsules[0].FakeStatusChange(0, 3);
            m_Capsules[1].FakeStatusChange(1, 4);

            yield return WaitForUpdates(m_DynamicEntries, 2);
            TestEntry(m_DynamicEntries, launchPad0Id, 0, 3);
            TestEntry(m_DynamicEntries, launchPad1Id, 1, 4);
            m_DynamicEntries.Clear();
        }

        class FakeCapsule
        {
            public FakeCapsule(int port)
            {
                ProcessingTask = ProcessingLoop.Start(port);
            }

            public void FakeStatusChange(byte nodeId, byte renderNodeId)
            {
                var capsuleStatus = SendCapsuleStatus.New();
                capsuleStatus.NodeRole = NodeRole.Repeater;
                capsuleStatus.NodeId = nodeId;
                capsuleStatus.RenderNodeId = renderNodeId;
                ProcessingLoop.QueueSendMessage(capsuleStatus);
            }

            public ProcessingLoop ProcessingLoop { get; } = new(false);
            public Task ProcessingTask { get; }
        }

        void AddFakeCapsule(int port)
        {
            m_Capsules.Add(new(port));
            Guid launchPadId = Guid.NewGuid();
            m_Mirror.LaunchPadsInformation.Add(new () {
                Definition = new() {
                    Identifier = launchPadId,
                    Endpoint = new($"http://127.0.0.1:{port}")
                },
                CapsulePort = port,
                Status = new(launchPadId) { State = State.Launched }
            });
        }

        IEnumerator WaitForConnectionCount(int count)
        {
            var waitTime = Stopwatch.StartNew();
            while (waitTime.Elapsed < TimeSpan.FromSeconds(10))
            {
                if (m_MonitorCapsulesProcess.ConnectionCount == count)
                {
                    break;
                }
                yield return null;
            }
            Assert.That(m_MonitorCapsulesProcess.ConnectionCount, Is.EqualTo(count));
        }

        static IEnumerator WaitForUpdates(Dictionary<Guid, LaunchPadReportDynamicEntry[]> dynamicEntries, int count)
        {
            var waitTime = Stopwatch.StartNew();
            while (waitTime.Elapsed < TimeSpan.FromSeconds(10))
            {
                lock (dynamicEntries)
                {
                    if (dynamicEntries.Count == count)
                    {
                        break;
                    }
                }
                yield return null;
            }
            lock (dynamicEntries)
            {
                Assert.That(dynamicEntries.Count, Is.EqualTo(count));
            }
        }

        static void TestEntry(Dictionary<Guid, LaunchPadReportDynamicEntry[]> dynamicEntries, Guid launchPadId,
            int expectedNodeId, int expectedRenderNodeId)
        {
            lock (dynamicEntries)
            {
                Assert.That(dynamicEntries.ContainsKey(launchPadId), Is.True);
                Assert.That(dynamicEntries[launchPadId].Length, Is.EqualTo(3));
                Assert.That(dynamicEntries[launchPadId][0].Name, Is.EqualTo("Role"));
                Assert.That(dynamicEntries[launchPadId][0].Value, Is.EqualTo("Repeater"));
                Assert.That(dynamicEntries[launchPadId][1].Name, Is.EqualTo("Node id"));
                Assert.That(dynamicEntries[launchPadId][1].Value, Is.EqualTo(expectedNodeId));
                Assert.That(dynamicEntries[launchPadId][2].Name, Is.EqualTo("Render node id"));
                Assert.That(dynamicEntries[launchPadId][2].Value, Is.EqualTo(expectedRenderNodeId));
            }
        }

        const int k_TestPort = Helpers.ListenPort;

        Application m_Application;
        Task m_AppRunningTask;
        MissionControlStub m_MissionControlStub = new();

        CancellationTokenSource m_MonitorCapsulesProcessStop;
        MonitorCapsulesProcess m_MonitorCapsulesProcess;
        Dictionary<Guid, LaunchPadReportDynamicEntry[]> m_DynamicEntries = new();

        List<FakeCapsule> m_Capsules = new();
        MissionControlMirror m_Mirror = new(new());
    }
}
