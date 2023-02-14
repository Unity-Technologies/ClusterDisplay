using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Unity.ClusterDisplay.MissionControl.Capsule;
using Unity.ClusterDisplay.MissionControl.MissionControl;
using UnityEngine.TestTools;
using Launchable = Unity.ClusterDisplay.MissionControl.LaunchCatalog.Launchable;
using State = Unity.ClusterDisplay.MissionControl.LaunchPad.State;

namespace Unity.ClusterDisplay.MissionControl.Capcom
{
    public class FailOverProcessTests
    {
        [SetUp]
        public void SetUp()
        {
            m_Mirror = new(new());

            m_MissionControlStub.Start();
            m_MissionControlStub.MissionParameters.Clear();
            m_MissionControlStub.MissionParametersDesiredValues.Clear();
            m_MissionControlStub.MissionParametersEffectiveValues.Clear();
            m_Mirror.MissionControlHttpClient.BaseAddress = MissionControlStub.HttpListenerEndpoint;

            m_Process = new(new (){FailOverProcessTimeout = TimeSpan.FromSeconds(5)},
                () => Assert.Fail("Should not be called in these tests"));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            m_MissionControlStub.Stop();
            foreach (var capsule in m_Capsules)
            {
                capsule.ProcessingLoop.Stop();
                yield return capsule.ProcessingTask.AsIEnumerator();
            }
            m_Capsules.Clear();
        }

        [UnityTest]
        public IEnumerator NoBackup()
        {
            Setup(4);

            yield return Process();

            // No backup nodes, nothing to do if anything fails, so no fail parameters should be created.
            Assert.That(m_MissionControlStub.MissionParameters, Is.Empty);
        }

        [UnityTest]
        public IEnumerator OneBackup()
        {
            Setup(3);
            SetNodeRoleAs(1, NodeRole.Backup);

            PutNewFailedParameterValue(1, m_MissionControlStub.MissionParametersDesiredValues);
            PutNewFailedParameterValue(2, m_MissionControlStub.MissionParametersEffectiveValues, Guid.NewGuid());

            yield return Process();

            // We have a backup node, so there should be parameters to manually fail a node
            Assert.That(m_MissionControlStub.MissionParameters.Count, Is.EqualTo(3));
            TestFailParameter(0);
            TestFailParameter(1);
            TestFailParameter(2);

            // Creation of the MissionParameter should have deleted any desired values
            for (int i = 0; i < 3; ++i)
            {
                Assert.That(GetFailParameterValue(i, m_MissionControlStub.MissionParametersDesiredValues), Is.Null);
            }
        }

        [UnityTest]
        public IEnumerator NotBackupAnymore()
        {
            Setup(3);
            SetNodeRoleAs(1, NodeRole.Backup);

            yield return Process();

            // Also put some desired and effective values, they should all get cleaned when parameters get removes later
            PutNewFailedParameterValue(1, m_MissionControlStub.MissionParametersDesiredValues);
            PutNewFailedParameterValue(2, m_MissionControlStub.MissionParametersEffectiveValues);

            // We have a backup node, so there should be parameters to manually fail a node
            Assert.That(m_MissionControlStub.MissionParameters.Count, Is.EqualTo(3));
            TestFailParameter(0);
            TestFailParameter(1);
            TestFailParameter(2);

            SetNodeRoleAs(1, NodeRole.Emitter);

            yield return Process();

            // No backup anymore, so there shouldn't be anymore parameters to fail nodes...
            Assert.That(m_MissionControlStub.MissionParameters, Is.Empty);
            for (int i = 0; i < 3; ++i)
            {
                Assert.That(GetFailParameterValue(i, m_MissionControlStub.MissionParametersDesiredValues), Is.Null);
                Assert.That(GetFailParameterValue(i, m_MissionControlStub.MissionParametersEffectiveValues), Is.Null);
            }
        }

        [UnityTest]
        public IEnumerator LastBackupNodeStops(
            [Values(State.Idle, State.GettingPayload, State.PreLaunch,
                    State.WaitingForLaunch, State.Over)] State stoppedState)
        {
            Setup(3);
            SetNodeRoleAs(1, NodeRole.Backup);

            yield return Process();

            // No backup nodes, nothing to do if anything fails, so no fail parameters should be created.
            Assert.That(m_MissionControlStub.MissionParameters.Count, Is.EqualTo(3));
            TestFailParameter(0);
            TestFailParameter(1);
            TestFailParameter(2);

            // Mark one of the nodes execution as over
            SetNodeStateAs(1, stoppedState);

            yield return Process();

            // Parameter should be removed for all nodes since there is no more backup nodes to take the place of a
            // failed node.
            Assert.That(m_MissionControlStub.MissionParameters, Is.Empty);
        }

        [UnityTest]
        public IEnumerator OneOutOfTwoBackupNodeStops(
            [Values(State.Idle, State.GettingPayload, State.PreLaunch,
                State.WaitingForLaunch, State.Over)] State stoppedState)
        {
            Setup(4);
            SetNodeRoleAs(1, NodeRole.Backup);
            SetNodeRoleAs(3, NodeRole.Backup);

            yield return Process();

            // No backup nodes, nothing to do if anything fails, so no fail parameters should be created.
            Assert.That(m_MissionControlStub.MissionParameters.Count, Is.EqualTo(4));
            TestFailParameter(0);
            TestFailParameter(1);
            TestFailParameter(2);
            TestFailParameter(3);

            // Mark one of the nodes execution as over
            SetNodeStateAs(1, stoppedState);

            yield return Process();

            // Parameter should be removed for that node that is now stopped, the others should stay as they were.
            Assert.That(m_MissionControlStub.MissionParameters.Count, Is.EqualTo(3));
            TestFailParameter(0);
            TestFailParameter(2);
            TestFailParameter(3);
        }

        [UnityTest]
        public IEnumerator NodeStarts()
        {
            Setup(3);
            for (int i = 0; i < 3; ++i)
            {
                SetNodeStateAs(i, State.Idle);
            }
            SetNodeRoleAs(0, NodeRole.Emitter);
            SetNodeRoleAs(1, NodeRole.Repeater);
            SetNodeRoleAs(2, NodeRole.Backup);

            yield return Process();

            // Nothing is running yet backup nodes, nothing to do if anything fails, so no fail parameters should be created.
            Assert.That(m_MissionControlStub.MissionParameters, Is.Empty);

            // Start the nodes
            for (int i = 0; i < 3; ++i)
            {
                SetNodeStateAs(i, State.Launched);
            }

            yield return Process();

            // Failed parameters should be created
            Assert.That(m_MissionControlStub.MissionParameters.Count, Is.EqualTo(3));
            TestFailParameter(0);
            TestFailParameter(1);
            TestFailParameter(2);
        }

        public enum FailMethod
        {
            TagAsOver,
            TagAsOverAndCloseCapsuleCommunication,
            MissionParameter
        }
        static readonly FailMethod[] k_AllTerminationMethods = new[] {FailMethod.TagAsOver,
            FailMethod.TagAsOverAndCloseCapsuleCommunication, FailMethod.MissionParameter};

        IEnumerator FailNode(FailMethod failMethod, int launchpadIndex)
        {
            if (failMethod is FailMethod.TagAsOver or FailMethod.TagAsOverAndCloseCapsuleCommunication)
            {
                SetNodeStateAs(launchpadIndex, State.Over);
            }
            if (failMethod is FailMethod.TagAsOverAndCloseCapsuleCommunication)
            {
                m_Mirror.LaunchPadsInformation[launchpadIndex].CapsulePort = 0;
                m_Capsules[launchpadIndex].ProcessingLoop.Stop();
                yield return m_Capsules[launchpadIndex].ProcessingTask.AsIEnumerator();
            }
            if (failMethod is FailMethod.MissionParameter)
            {
                MissionParameterValue toAdd = new(Guid.NewGuid()) {
                    ValueIdentifier = m_Mirror.LaunchPadsInformation[launchpadIndex].FailMissionParameterValueIdentifier,
                    Value = JToken.Parse($"\"{Guid.NewGuid()}\"")
                };
                m_MissionControlStub.MissionParametersDesiredValues.Add(toAdd);
            }
        }

        [UnityTest]
        public IEnumerator FailRepeater([ValueSource(nameof(k_AllTerminationMethods))] FailMethod failMethod)
        {
            Setup(3);
            SetNodeRoleAs(0, NodeRole.Emitter);
            SetNodeRoleAs(1, NodeRole.Repeater);
            SetNodeRoleAs(2, NodeRole.Backup);

            // Initial process to establish the initial configuration
            yield return Process();
            Assert.That(m_MissionControlStub.MissionParameters.Count, Is.EqualTo(3));
            TestDidNotReceivedClusterConfiguration();

            // Terminate repeater
            yield return FailNode(failMethod, 1);
            yield return Process();

            // Every capsule should now have receive the new cluster configuration to deal with the failed node.
            ChangeClusterTopologyEntry[] expectedEntries = {
                new() {NodeId = 0, NodeRole = NodeRole.Emitter, RenderNodeId = 0},
                new() {NodeId = 2, NodeRole = NodeRole.Repeater, RenderNodeId = 1}
            };
            if (failMethod != FailMethod.TagAsOverAndCloseCapsuleCommunication)
            {
                TestReceivedClusterConfiguration(1, expectedEntries);
            }
            else
            {
                TestDidNotReceivedClusterConfiguration(1);
            }
            TestReceivedClusterConfiguration(new[] {0, 2}, expectedEntries);
        }

        [UnityTest]
        public IEnumerator FailEmitter([ValueSource(nameof(k_AllTerminationMethods))] FailMethod failMethod)
        {
            Setup(3);
            SetNodeRoleAs(0, NodeRole.Emitter);
            SetNodeRoleAs(1, NodeRole.Repeater);
            SetNodeRoleAs(2, NodeRole.Backup);

            // Initial process to establish the initial configuration
            yield return Process();
            Assert.That(m_MissionControlStub.MissionParameters.Count, Is.EqualTo(3));
            TestDidNotReceivedClusterConfiguration();

            // Terminate repeater
            yield return FailNode(failMethod, 0);
            yield return Process();

            // Every capsule should now have receive the new cluster configuration to deal with the failed node.
            ChangeClusterTopologyEntry[] expectedEntries = {
                new() {NodeId = 1, NodeRole = NodeRole.Repeater, RenderNodeId = 1},
                new() {NodeId = 2, NodeRole = NodeRole.Emitter, RenderNodeId = 0}
            };
            if (failMethod != FailMethod.TagAsOverAndCloseCapsuleCommunication)
            {
                TestReceivedClusterConfiguration(0, expectedEntries);
            }
            else
            {
                TestDidNotReceivedClusterConfiguration(0);
            }
            TestReceivedClusterConfiguration(new[] {1, 2}, expectedEntries);
        }

        [UnityTest]
        public IEnumerator FailBackup([ValueSource(nameof(k_AllTerminationMethods))] FailMethod failMethod)
        {
            Setup(3);
            SetNodeRoleAs(0, NodeRole.Emitter);
            SetNodeRoleAs(1, NodeRole.Repeater);
            SetNodeRoleAs(2, NodeRole.Backup);

            // Initial process to establish the initial configuration
            yield return Process();
            Assert.That(m_MissionControlStub.MissionParameters.Count, Is.EqualTo(3));
            TestDidNotReceivedClusterConfiguration();

            // Terminate backup
            yield return FailNode(failMethod, 2);
            yield return Process();

            // Every capsule should now have receive the new cluster configuration to deal with the failed node.
            ChangeClusterTopologyEntry[] expectedEntries = {
                new() {NodeId = 0, NodeRole = NodeRole.Emitter, RenderNodeId = 0},
                new() {NodeId = 1, NodeRole = NodeRole.Repeater, RenderNodeId = 1}
            };
            TestReceivedClusterConfiguration(new[] {0, 1}, expectedEntries);
            if (failMethod != FailMethod.TagAsOverAndCloseCapsuleCommunication)
            {
                TestReceivedClusterConfiguration(2, expectedEntries);
            }
            else
            {
                TestDidNotReceivedClusterConfiguration(2);
            }

            // Just process again to be sure we do not broadcast the failed backup again.
            FakeStatusChange();
            yield return Process();
            TestDidNotReceivedClusterConfiguration();
        }

        [UnityTest]
        public IEnumerator SimultaneousFailures([ValueSource(nameof(k_AllTerminationMethods))] FailMethod failMethod)
        {
            Setup(4);
            SetNodeRoleAs(0, NodeRole.Emitter);
            SetNodeRoleAs(1, NodeRole.Repeater);
            SetNodeRoleAs(2, NodeRole.Backup);
            SetNodeRoleAs(3, NodeRole.Backup);

            // Initial process to establish the initial configuration
            yield return Process();
            Assert.That(m_MissionControlStub.MissionParameters.Count, Is.EqualTo(4));
            TestDidNotReceivedClusterConfiguration();

            // Terminate repeater
            yield return FailNode(failMethod, 0);
            yield return FailNode(failMethod, 1);
            yield return Process();

            // Every capsule should now have receive the new cluster configuration to deal with the failed node.
            ChangeClusterTopologyEntry[] expectedEntries = {
                new() {NodeId = 2, NodeRole = NodeRole.Emitter, RenderNodeId = 0},
                new() {NodeId = 3, NodeRole = NodeRole.Repeater, RenderNodeId = 1}
            };
            if (failMethod != FailMethod.TagAsOverAndCloseCapsuleCommunication)
            {
                TestReceivedClusterConfiguration(0, expectedEntries);
                TestReceivedClusterConfiguration(1, expectedEntries);
            }
            else
            {
                TestDidNotReceivedClusterConfiguration(0);
                TestDidNotReceivedClusterConfiguration(1);
            }
            TestReceivedClusterConfiguration(new[] {2, 3}, expectedEntries);
        }

        [UnityTest]
        public IEnumerator SuccessiveFailures([ValueSource(nameof(k_AllTerminationMethods))] FailMethod failMethod,
            [Values(true, false)] bool acceptNewRole)
        {
            Setup(7);
            SetNodeRoleAs(0, NodeRole.Emitter);
            SetNodeRoleAs(1, NodeRole.Repeater);
            for (int i = 2; i < 7; ++i)
            {
                SetNodeRoleAs(i, NodeRole.Backup);
            }

            // Initial process to establish the initial configuration
            yield return Process();
            Assert.That(m_MissionControlStub.MissionParameters.Count, Is.EqualTo(7));
            TestDidNotReceivedClusterConfiguration();

            // Terminate 0
            yield return FailNode(failMethod, 0);
            yield return Process();

            // Every capsule should now have receive the new cluster configuration to deal with the failed node.
            ChangeClusterTopologyEntry[] expectedEntries = {
                new() {NodeId = 1, NodeRole = NodeRole.Repeater, RenderNodeId = 1},
                new() {NodeId = 2, NodeRole = NodeRole.Emitter, RenderNodeId = 0},
                new() {NodeId = 3, NodeRole = NodeRole.Backup, RenderNodeId = 0},
                new() {NodeId = 4, NodeRole = NodeRole.Backup, RenderNodeId = 0},
                new() {NodeId = 5, NodeRole = NodeRole.Backup, RenderNodeId = 0},
                new() {NodeId = 6, NodeRole = NodeRole.Backup, RenderNodeId = 0}
            };
            if (failMethod != FailMethod.TagAsOverAndCloseCapsuleCommunication)
            {
                TestReceivedClusterConfiguration(0, expectedEntries);
            }
            else
            {
                TestDidNotReceivedClusterConfiguration(0);
            }
            TestReceivedClusterConfiguration(new[] {1, 2, 3, 4, 5, 6}, expectedEntries);

            // Simulate the node accepting its new role or test that the number of pending change is as expected
            if (acceptNewRole)
            {
                SetNodeRoleAs(2, NodeRole.Emitter);
                SetRenderNodeIdTo(2, 0);
            }
            Assert.That(m_Process.PendingAssignments, Is.EqualTo(1));

            // Terminate 1
            yield return FailNode(failMethod, 1);
            yield return Process();

            // Every capsule should now have receive the new cluster configuration to deal with the failed node.
            expectedEntries = new ChangeClusterTopologyEntry[] {
                new() {NodeId = 2, NodeRole = NodeRole.Emitter, RenderNodeId = 0},
                new() {NodeId = 3, NodeRole = NodeRole.Repeater, RenderNodeId = 1},
                new() {NodeId = 4, NodeRole = NodeRole.Backup, RenderNodeId = 0},
                new() {NodeId = 5, NodeRole = NodeRole.Backup, RenderNodeId = 0},
                new() {NodeId = 6, NodeRole = NodeRole.Backup, RenderNodeId = 0}
            };
            if (failMethod != FailMethod.TagAsOverAndCloseCapsuleCommunication)
            {
                TestReceivedClusterConfiguration(new[] {0, 1}, expectedEntries);
            }
            else
            {
                TestDidNotReceivedClusterConfiguration(new[] {0, 1});
            }
            TestReceivedClusterConfiguration(new[] {2, 3, 4, 5, 6}, expectedEntries);

            // Simulate the node accepting its new role or test that the number of pending change is as expected
            if (acceptNewRole)
            {
                SetNodeRoleAs(3, NodeRole.Repeater);
                SetRenderNodeIdTo(3, 1);
                Assert.That(m_Process.PendingAssignments, Is.EqualTo(1));
            }
            else
            {
                Assert.That(m_Process.PendingAssignments, Is.EqualTo(2));
            }

            // Terminate 2
            yield return FailNode(failMethod, 2);
            yield return Process();

            // Every capsule should now have receive the new cluster configuration to deal with the failed node.
            expectedEntries = new ChangeClusterTopologyEntry[] {
                new() {NodeId = 3, NodeRole = NodeRole.Repeater, RenderNodeId = 1},
                new() {NodeId = 4, NodeRole = NodeRole.Emitter, RenderNodeId = 0},
                new() {NodeId = 5, NodeRole = NodeRole.Backup, RenderNodeId = 0},
                new() {NodeId = 6, NodeRole = NodeRole.Backup, RenderNodeId = 0}
            };
            if (failMethod != FailMethod.TagAsOverAndCloseCapsuleCommunication)
            {
                TestReceivedClusterConfiguration(new[] {0, 1, 2}, expectedEntries);
            }
            else
            {
                TestDidNotReceivedClusterConfiguration(new[] {0, 1, 2});
            }
            TestReceivedClusterConfiguration(new[] {3, 4, 5, 6}, expectedEntries);

            // Simulate the node accepting its new role or test that the number of pending change is as expected
            if (acceptNewRole)
            {
                SetNodeRoleAs(4, NodeRole.Emitter);
                SetRenderNodeIdTo(4, 0);
                Assert.That(m_Process.PendingAssignments, Is.EqualTo(1));
            }
            else
            {
                Assert.That(m_Process.PendingAssignments, Is.EqualTo(3));
            }

            // Terminate 3
            yield return FailNode(failMethod, 3);
            yield return Process();

            // Every capsule should now have receive the new cluster configuration to deal with the failed node.
            expectedEntries = new ChangeClusterTopologyEntry[] {
                new() {NodeId = 4, NodeRole = NodeRole.Emitter, RenderNodeId = 0},
                new() {NodeId = 5, NodeRole = NodeRole.Repeater, RenderNodeId = 1},
                new() {NodeId = 6, NodeRole = NodeRole.Backup, RenderNodeId = 0}
            };
            if (failMethod != FailMethod.TagAsOverAndCloseCapsuleCommunication)
            {
                TestReceivedClusterConfiguration(new[] {0, 1, 2, 3}, expectedEntries);
            }
            else
            {
                TestDidNotReceivedClusterConfiguration(new[] {0, 1, 2, 3});
            }
            TestReceivedClusterConfiguration(new[] {4, 5, 6}, expectedEntries);

            // Simulate the node accepting its new role or test that the number of pending change is as expected
            if (acceptNewRole)
            {
                SetNodeRoleAs(5, NodeRole.Repeater);
                SetRenderNodeIdTo(5, 1);
                Assert.That(m_Process.PendingAssignments, Is.EqualTo(1));
            }
            else
            {
                Assert.That(m_Process.PendingAssignments, Is.EqualTo(4));
            }

            // Terminate 4
            yield return FailNode(failMethod, 4);
            yield return Process();

            // Every capsule should now have receive the new cluster configuration to deal with the failed node.
            expectedEntries = new ChangeClusterTopologyEntry[] {
                new() {NodeId = 5, NodeRole = NodeRole.Repeater, RenderNodeId = 1},
                new() {NodeId = 6, NodeRole = NodeRole.Emitter, RenderNodeId = 0}
            };
            if (failMethod != FailMethod.TagAsOverAndCloseCapsuleCommunication)
            {
                TestReceivedClusterConfiguration(new[] {0, 1, 2, 3, 4}, expectedEntries);
            }
            else
            {
                TestDidNotReceivedClusterConfiguration(new[] {0, 1, 2, 3, 4});
            }
            TestReceivedClusterConfiguration(new[] {5, 6}, expectedEntries);

            // Simulate the node accepting its new role or test that the number of pending change is as expected
            Assert.That(m_Process.PendingAssignments, acceptNewRole ? Is.EqualTo(1) : Is.EqualTo(5));

            // Fake a restart, this should clean pending assignments
            ++m_Mirror.LaunchPadsInformationVersion;
            yield return Process();
            Assert.That(m_Process.PendingAssignments, Is.EqualTo(0));
        }

        // An undefined status could happen for example if the Launchpad crashes.  Although this is far from ideal, the
        // capsule is what is important and it is still working fine.  So undefined launchpad status should cause a
        // change in the cluster configuration.
        [UnityTest]
        public IEnumerator UndefinedStatusDoesNotTriggerReconfiguration()
        {
            Setup(3);
            SetNodeRoleAs(0, NodeRole.Emitter);
            SetNodeRoleAs(1, NodeRole.Repeater);
            SetNodeRoleAs(2, NodeRole.Backup);

            // Initial process to establish the initial configuration
            yield return Process();
            Assert.That(m_MissionControlStub.MissionParameters.Count, Is.EqualTo(3));
            TestDidNotReceivedClusterConfiguration();

            int undefinedCount = 0;
            foreach (var index in new[] {1,0,2})
            {
                SetLaunchPadAsUndefined(index);
                ++undefinedCount;
                yield return Process();
                Assert.That(m_MissionControlStub.MissionParameters.Count, Is.EqualTo(3 - undefinedCount));
                TestDidNotReceivedClusterConfiguration();
            }
        }

        [UnityTest]
        public IEnumerator ManualFailSetsEffectiveValue()
        {
            Setup(4);
            SetNodeRoleAs(2, NodeRole.Backup);
            SetNodeRoleAs(3, NodeRole.Backup);

            var failExecutionId = Guid.NewGuid();
            PutNewFailedParameterValue(1, m_MissionControlStub.MissionParametersDesiredValues, failExecutionId);

            yield return Process();

            // Mission Control should have received the effective value
            var parameterValue = GetFailParameterValue(1, m_MissionControlStub.MissionParametersEffectiveValues);
            Assert.That(parameterValue, Is.Not.Null);
            Assert.That(parameterValue.AsGuid(), Is.EqualTo(failExecutionId));

            // Fake a hard failure of node 3, shouldn't affect the effective value that was just created
            SetNodeStateAs(3, State.Over);
            yield return Process();

            parameterValue = GetFailParameterValue(1, m_MissionControlStub.MissionParametersEffectiveValues);
            Assert.That(parameterValue, Is.Not.Null);
            Assert.That(parameterValue.AsGuid(), Is.EqualTo(failExecutionId));

            // But concluding of the manually failed launchpad should clear the effective value
            SetNodeStateAs(1, State.Over);
            yield return Process();

            Assert.That(GetFailParameterValue(1, m_MissionControlStub.MissionParametersEffectiveValues), Is.Null);
        }

        [UnityTest]
        public IEnumerator ManualFailOnlyOnce()
        {
            Setup(4);
            SetNodeRoleAs(2, NodeRole.Backup);
            SetNodeRoleAs(3, NodeRole.Backup);

            var fail1ExecutionId = Guid.NewGuid();
            PutNewFailedParameterValue(1, m_MissionControlStub.MissionParametersDesiredValues, fail1ExecutionId);

            yield return Process();

            // Process should transfer the uuid from desired to effective
            var parameterValue = GetFailParameterValue(1, m_MissionControlStub.MissionParametersEffectiveValues);
            Assert.That(parameterValue, Is.Not.Null);
            Assert.That(parameterValue.AsGuid(), Is.EqualTo(fail1ExecutionId));

            // Try to execute the fail command on the same node a second time, shouldn't change anything
            var fail2ExecutionId = Guid.NewGuid();
            PutNewFailedParameterValue(1, m_MissionControlStub.MissionParametersDesiredValues, fail2ExecutionId);

            yield return Process();

            parameterValue = GetFailParameterValue(1, m_MissionControlStub.MissionParametersEffectiveValues);
            Assert.That(parameterValue, Is.Not.Null);
            Assert.That(parameterValue.AsGuid(), Is.EqualTo(fail1ExecutionId));

            // But failing another node would work
            var fail3ExecutionId = Guid.NewGuid();
            PutNewFailedParameterValue(0, m_MissionControlStub.MissionParametersDesiredValues, fail3ExecutionId);

            yield return Process();

            // Node 1 still unchanged
            parameterValue = GetFailParameterValue(1, m_MissionControlStub.MissionParametersEffectiveValues);
            Assert.That(parameterValue, Is.Not.Null);
            Assert.That(parameterValue.AsGuid(), Is.EqualTo(fail1ExecutionId));

            // But node 0 is now also failed
            parameterValue = GetFailParameterValue(0, m_MissionControlStub.MissionParametersEffectiveValues);
            Assert.That(parameterValue, Is.Not.Null);
            Assert.That(parameterValue.AsGuid(), Is.EqualTo(fail3ExecutionId));
        }

        void PutNewFailedParameterValue(int launchpadIndex, IncrementalCollection<MissionParameterValue> collection,
            Guid? value = default)
        {
            collection.Add(new(Guid.NewGuid()) {
                ValueIdentifier = m_Mirror.LaunchPadsInformation[launchpadIndex].FailMissionParameterValueIdentifier,
                Value = value != null ? JToken.Parse($"\"{value}\"") : null
            });
        }

        void SetNodeRoleAs(int launchPadIndex, NodeRole role)
        {
            var status = m_Mirror.LaunchPadsInformation[launchPadIndex].Status;
            status.DynamicEntries.First(e => e.Name == LaunchPadReportDynamicEntryConstants.StatusNodeRole).Value =
                role.ToString();
            if (role == NodeRole.Backup)
            {
                status.DynamicEntries.First(
                    e => e.Name == LaunchPadReportDynamicEntryConstants.StatusRenderNodeId).Value = 0;
            }
            status.SignalChanges(m_Mirror.LaunchPadsStatus);
        }

        void SetRenderNodeIdTo(int launchPadIndex, byte renderNodeId)
        {
            var status = m_Mirror.LaunchPadsInformation[launchPadIndex].Status;
            status.DynamicEntries.First(
                e => e.Name == LaunchPadReportDynamicEntryConstants.StatusRenderNodeId).Value = (int)renderNodeId;
            status.SignalChanges(m_Mirror.LaunchPadsStatus);
        }

        void SetNodeStateAs(int launchPadIndex, State state)
        {
            var status = m_Mirror.LaunchPadsInformation[launchPadIndex].Status;
            status.State = state;
            status.SignalChanges(m_Mirror.LaunchPadsStatus);
        }

        void SetLaunchPadAsUndefined(int launchPadIndex)
        {
            var status = m_Mirror.LaunchPadsInformation[launchPadIndex].Status;
            status.IsDefined = false;
            status.State = State.Idle;
            status.SignalChanges(m_Mirror.LaunchPadsStatus);
        }

        void FakeStatusChange()
        {
            LaunchPadStatus newDummy = new(Guid.NewGuid());
            m_Mirror.LaunchPadsStatus.Add(newDummy);
            m_Mirror.LaunchPadsStatus.Remove(newDummy.Id);
        }

        void Setup(int nodesCount)
        {
            m_Mirror.Status.State = MissionControl.State.Launched;
            m_Mirror.Status.EnteredStateTime = DateTime.Now;

            var assetId = Guid.NewGuid();
            m_Mirror.Assets.Add(new(assetId));
            m_Mirror.Assets.Values.First().Launchables.Add(new() {
                Name = "Test Launchable", Type = Launchable.ClusterNodeType
            });

            for (int nodeIndex = 0; nodeIndex < nodesCount; ++nodeIndex)
            {
                var launchComplexId = Guid.NewGuid();
                var launchPadId = Guid.NewGuid();

                LaunchPadStatus status = new(launchPadId)
                {
                    IsDefined = true,
                    State = State.Launched,
                    DynamicEntries = new LaunchPadReportDynamicEntry[] {
                        new() {
                            Name = LaunchPadReportDynamicEntryConstants.StatusNodeRole,
                            Value = NodeRole.Repeater.ToString()
                        },
                        new() {
                            Name = LaunchPadReportDynamicEntryConstants.StatusRenderNodeId,
                            Value = nodeIndex
                        }
                    }
                };

                int capsulePort = Helpers.ListenPort + nodeIndex;
                m_Mirror.LaunchPadsInformation.Add(new()
                {
                    Definition = new () {
                        Identifier = launchPadId,
                        Endpoint = new("http://127.0.0.1:1234")
                    },
                    ComplexDefinition = new (launchComplexId),
                    CapsulePort = capsulePort,
                    NodeId = nodeIndex,
                    Status = status
                });
                m_Mirror.LaunchPadsStatus[launchPadId] = status;

                m_Capsules.Add(new(capsulePort));
            }
        }

        IEnumerator Process()
        {
            if (m_MissionControlStub.MissionParametersDesiredValues.VersionNumber >
                    m_MirrorMissionParametersDesiredValuesLastUpdateFrom)
            {
                var update = m_MissionControlStub.MissionParametersDesiredValues.GetDeltaSince(
                    m_MirrorMissionParametersDesiredValuesLastUpdateFrom);
                m_Mirror.ParametersDesiredValues.ApplyDelta(update);
                m_MirrorMissionParametersDesiredValuesLastUpdateFrom =
                    m_MissionControlStub.MissionParametersDesiredValues.VersionNumber;
            }

            if (m_MissionControlStub.MissionParametersEffectiveValues.VersionNumber >
                    m_MirrorMissionParametersEffectiveValuesLastUpdateFrom)
            {
                var update = m_MissionControlStub.MissionParametersEffectiveValues.GetDeltaSince(
                    m_MirrorMissionParametersEffectiveValuesLastUpdateFrom);
                m_Mirror.ParametersEffectiveValues.ApplyDelta(update);
                m_MirrorMissionParametersEffectiveValuesLastUpdateFrom =
                    m_MissionControlStub.MissionParametersEffectiveValues.VersionNumber;
            }

            // Need to be ran asynchronously because implementation of Process make REST calls that need to be awaited
            // on exploiting a problem with NUnit running in Unity.
            yield return Task.Run(() => m_Process.Process(m_Mirror)).AsIEnumerator();
        }

        void TestFailParameter(int launchpadIndex)
        {
            var launchPadInfo = m_Mirror.LaunchPadsInformation[launchpadIndex];
            TestMissionParameter(launchPadInfo.FailMissionParameterValueIdentifier, "Set Failover",
                MissionParameterType.Command, launchPadInfo.Definition.Identifier.ToString());

            var matchingEffectiveValue = GetFailParameterValue(launchpadIndex,
                m_MissionControlStub.MissionParametersEffectiveValues);
            Assert.That(matchingEffectiveValue, Is.Not.Null);
            Assert.That(matchingEffectiveValue.IsNull, Is.True);
        }

        void TestMissionParameter(string valueIdentifier, string name, MissionParameterType type, string group)
        {
            var parameter = m_MissionControlStub.MissionParameters.Values
                .FirstOrDefault(p => p.ValueIdentifier == valueIdentifier);
            Assert.That(parameter, Is.Not.Null);

            Assert.That(parameter.ValueIdentifier, Is.EqualTo(valueIdentifier));
            Assert.That(parameter.Name, Is.EqualTo(name));
            Assert.That(parameter.Type, Is.EqualTo(type));
            Assert.That(parameter.Group, Is.EqualTo(group));
        }

        MissionParameterValue GetFailParameterValue(int launchpadIndex,
            IncrementalCollection<MissionParameterValue> collection)
        {
            string valueIdentifier = m_Mirror.LaunchPadsInformation[launchpadIndex].FailMissionParameterValueIdentifier;
            return collection.Values.FirstOrDefault(v => v.ValueIdentifier == valueIdentifier);
        }

        void TestReceivedClusterConfiguration(int launchpadIndex, IEnumerable<ChangeClusterTopologyEntry> entries)
        {
            var fakeHandler = m_Capsules[launchpadIndex].FakeTopologyChangeHandler;
            Assert.That(fakeHandler.Called, Is.True);
            Assert.That(fakeHandler.Entries, Is.EqualTo(entries));
            fakeHandler.Entries.Clear();
            fakeHandler.Called = false;
        }

        void TestReceivedClusterConfiguration(IEnumerable<int> launchpadIndices, IEnumerable<ChangeClusterTopologyEntry> entries)
        {
            foreach (var launchpadIndex in launchpadIndices)
            {
                TestReceivedClusterConfiguration(launchpadIndex, entries);
            }
        }

        void TestDidNotReceivedClusterConfiguration(int launchpadIndex)
        {
            var fakeHandler = m_Capsules[launchpadIndex].FakeTopologyChangeHandler;
            Assert.That(fakeHandler.Called, Is.False);
        }

        void TestDidNotReceivedClusterConfiguration(IEnumerable<int> launchpadIndices)
        {
            foreach (var launchpadIndex in launchpadIndices)
            {
                TestDidNotReceivedClusterConfiguration(launchpadIndex);
            }
        }

        void TestDidNotReceivedClusterConfiguration()
        {
            for (int i = 0; i < m_Capsules.Count; ++i)
            {
                TestDidNotReceivedClusterConfiguration(i);
            }
        }

        class FakeTopologyChangeHandler : IMessageHandler
        {
            public async ValueTask HandleMessage(NetworkStream networkStream)
            {
                var header = await networkStream.ReadStructAsync<ChangeClusterTopologyMessageHeader>(
                    m_MessageBuffer).ConfigureAwait(false);
                if (header != null)
                {
                    for (int i = 0; i < header.Value.EntriesCount; ++i)
                    {
                        var entry = await networkStream.ReadStructAsync<ChangeClusterTopologyEntry>(m_MessageBuffer);
                        if (entry != null)
                        {
                            Entries.Add(entry.Value);
                        }
                    }
                }
                await networkStream.WriteStructAsync(new LandResponse(), m_ResponseBuffer).ConfigureAwait(false);
                Called = true;
            }

            public bool Called { get; set; }
            public List<ChangeClusterTopologyEntry> Entries { get; } = new();

            byte[] m_MessageBuffer = new byte[Math.Max(Marshal.SizeOf<ChangeClusterTopologyMessageHeader>(),
                                                       Marshal.SizeOf<ChangeClusterTopologyEntry>())];
            byte[] m_ResponseBuffer = new byte[Marshal.SizeOf<ChangeClusterTopologyResponse>()];
        }

        class FakeCapsule
        {
            public FakeCapsule(int port)
            {
                ProcessingLoop.AddMessageHandler(MessagesId.ChangeClusterTopology, FakeTopologyChangeHandler);
                ProcessingTask = ProcessingLoop.Start(port);
            }

            public ProcessingLoop ProcessingLoop { get; } = new(false);
            public FakeTopologyChangeHandler FakeTopologyChangeHandler { get; } = new();
            public Task ProcessingTask { get; }
        }

        MissionControlStub m_MissionControlStub = new();
        List<FakeCapsule> m_Capsules = new();
        MissionControlMirror m_Mirror;
        FailOverProcess m_Process;
        ulong m_MirrorMissionParametersDesiredValuesLastUpdateFrom;
        ulong m_MirrorMissionParametersEffectiveValuesLastUpdateFrom;
    }
}
