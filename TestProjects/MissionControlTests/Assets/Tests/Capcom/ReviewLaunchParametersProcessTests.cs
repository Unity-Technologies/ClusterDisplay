using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using NUnit.Framework;
using Unity.ClusterDisplay.MissionControl.MissionControl;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.ClusterDisplay.MissionControl.Capcom
{
    public class ReviewLaunchParametersProcessTests
    {
        [SetUp]
        public void SetUp()
        {
            m_Mirror = new();
            m_LaunchComplexesId.Clear();

            m_MissionControlStub.Start();
            m_MissionControlStub.LaunchParametersForReview.Clear();

            m_Process = new(new HttpClient() {BaseAddress = MissionControlStub.HttpListenerEndpoint});
        }

        [TearDown]
        public void TearDown()
        {
            m_MissionControlStub.Stop();
        }

        [UnityTest]
        public IEnumerator AllDefault()
        {
            Setup(4);

            yield return Process();

            TestLaunchParameterForReview(m_LaunchComplexesId[0], LaunchParameterConstants.NodeIdParameterId, 0);
            TestLaunchParameterForReview(m_LaunchComplexesId[0], LaunchParameterConstants.NodeRoleParameterId,
                LaunchParameterConstants.NodeRoleEmitter);
            TestLaunchParameterForReview(m_LaunchComplexesId[0], LaunchParameterConstants.RepeaterCountParameterId, 3);

            TestLaunchParameterForReview(m_LaunchComplexesId[1], LaunchParameterConstants.NodeIdParameterId, 1);
            TestLaunchParameterForReview(m_LaunchComplexesId[1], LaunchParameterConstants.NodeRoleParameterId,
                LaunchParameterConstants.NodeRoleRepeater);
            TestLaunchParameterForReview(m_LaunchComplexesId[1], LaunchParameterConstants.RepeaterCountParameterId, 3);

            TestLaunchParameterForReview(m_LaunchComplexesId[2], LaunchParameterConstants.NodeIdParameterId, 2);
            TestLaunchParameterForReview(m_LaunchComplexesId[2], LaunchParameterConstants.NodeRoleParameterId,
                LaunchParameterConstants.NodeRoleRepeater);
            TestLaunchParameterForReview(m_LaunchComplexesId[2], LaunchParameterConstants.RepeaterCountParameterId, 3);

            TestLaunchParameterForReview(m_LaunchComplexesId[3], LaunchParameterConstants.NodeIdParameterId, 3);
            TestLaunchParameterForReview(m_LaunchComplexesId[3], LaunchParameterConstants.NodeRoleParameterId,
                LaunchParameterConstants.NodeRoleRepeater);
            TestLaunchParameterForReview(m_LaunchComplexesId[3], LaunchParameterConstants.RepeaterCountParameterId, 3);
        }

        [UnityTest]
        public IEnumerator SingleNode()
        {
            Setup(1);

            yield return Process();

            TestLaunchParameterForReview(m_LaunchComplexesId[0], LaunchParameterConstants.NodeIdParameterId, 0);
            TestLaunchParameterForReview(m_LaunchComplexesId[0], LaunchParameterConstants.NodeRoleParameterId,
                LaunchParameterConstants.NodeRoleEmitter);
            TestLaunchParameterForReview(m_LaunchComplexesId[0], LaunchParameterConstants.RepeaterCountParameterId, 0);
        }

        [UnityTest]
        public IEnumerator TooManyNodes()
        {
            Setup(257);

            yield return Process();

            for (int i = 0; i <= 255; ++i)
            {
                TestLaunchParameterForReview(m_LaunchComplexesId[i], LaunchParameterConstants.NodeIdParameterId, i);
            }
            TestLaunchParameterForReview(m_LaunchComplexesId[256], LaunchParameterConstants.NodeIdParameterId, -1,
                $"Cannot find a free NodeId value, are we really trying to run a {byte.MaxValue + 1} cluster?");
        }

        [UnityTest]
        public IEnumerator NodeIdSomeManual()
        {
            Setup(4);

            var launchPad2NodeId =
                GetLaunchParameterForReview(m_LaunchComplexesId[2], LaunchParameterConstants.NodeIdParameterId);
            launchPad2NodeId.Value.Value = 1;
            var launchPad3NodeId =
                GetLaunchParameterForReview(m_LaunchComplexesId[3], LaunchParameterConstants.NodeIdParameterId);
            launchPad3NodeId.Value.Value = 0;

            yield return Process();

            TestLaunchParameterForReview(m_LaunchComplexesId[0], LaunchParameterConstants.NodeIdParameterId, 2);
            TestLaunchParameterForReview(m_LaunchComplexesId[1], LaunchParameterConstants.NodeIdParameterId, 3);
            TestLaunchParameterForReview(m_LaunchComplexesId[2], LaunchParameterConstants.NodeIdParameterId, 1);
            TestLaunchParameterForReview(m_LaunchComplexesId[3], LaunchParameterConstants.NodeIdParameterId, 0);
        }

        [UnityTest]
        public IEnumerator NodeIdIgnoreMissing()
        {
            Setup(4);

            var launchPad1NodeId =
                GetLaunchParameterForReview(m_LaunchComplexesId[1], LaunchParameterConstants.NodeIdParameterId);
            m_MissionControlStub.LaunchParametersForReview.Remove(launchPad1NodeId.Id);

            yield return Process();

            TestLaunchParameterForReview(m_LaunchComplexesId[0], LaunchParameterConstants.NodeIdParameterId, 0);
            TestLaunchParameterForReview(m_LaunchComplexesId[2], LaunchParameterConstants.NodeIdParameterId, 1);
            TestLaunchParameterForReview(m_LaunchComplexesId[3], LaunchParameterConstants.NodeIdParameterId, 2);
        }

        [UnityTest]
        public IEnumerator NodeIdInvalidTypeToDefault()
        {
            Setup(3);

            var launchPad1NodeId =
                GetLaunchParameterForReview(m_LaunchComplexesId[1], LaunchParameterConstants.NodeIdParameterId);
            launchPad1NodeId.Value.Value = "Shouldn't be a string";

            yield return Process();

            TestLaunchParameterForReview(m_LaunchComplexesId[0], LaunchParameterConstants.NodeIdParameterId, 0);
            TestLaunchParameterForReview(m_LaunchComplexesId[1], LaunchParameterConstants.NodeIdParameterId, 1,
                "Must be an integer.");
            TestLaunchParameterForReview(m_LaunchComplexesId[2], LaunchParameterConstants.NodeIdParameterId, 2);
        }

        [UnityTest]
        public IEnumerator NodeIdOutOfBound()
        {
            Setup(3);

            var launchPad1NodeId =
                GetLaunchParameterForReview(m_LaunchComplexesId[1], LaunchParameterConstants.NodeIdParameterId);
            launchPad1NodeId.Value.Value = -2;
            var launchPad2NodeId =
                GetLaunchParameterForReview(m_LaunchComplexesId[2], LaunchParameterConstants.NodeIdParameterId);
            launchPad2NodeId.Value.Value = 256;

            yield return Process();

            TestLaunchParameterForReview(m_LaunchComplexesId[0], LaunchParameterConstants.NodeIdParameterId, 0);
            TestLaunchParameterForReview(m_LaunchComplexesId[1], LaunchParameterConstants.NodeIdParameterId, 1,
                "Must be in the [0, 255] range.");
            TestLaunchParameterForReview(m_LaunchComplexesId[2], LaunchParameterConstants.NodeIdParameterId, 2,
                "Must be in the [0, 255] range.");
        }

        [UnityTest]
        public IEnumerator NodeIdRepeat()
        {
            Setup(3);

            var launchPad1NodeId =
                GetLaunchParameterForReview(m_LaunchComplexesId[1], LaunchParameterConstants.NodeIdParameterId);
            launchPad1NodeId.Value.Value = 0;
            var launchPad2NodeId =
                GetLaunchParameterForReview(m_LaunchComplexesId[2], LaunchParameterConstants.NodeIdParameterId);
            launchPad2NodeId.Value.Value = 0;

            yield return Process();

            TestLaunchParameterForReview(m_LaunchComplexesId[0], LaunchParameterConstants.NodeIdParameterId, 1);
            TestLaunchParameterForReview(m_LaunchComplexesId[1], LaunchParameterConstants.NodeIdParameterId, 0);
            TestLaunchParameterForReview(m_LaunchComplexesId[2], LaunchParameterConstants.NodeIdParameterId, 2,
                $"NodeId 0 is already used by LaunchPad {launchPad1NodeId.LaunchPadId}.");
        }

        [UnityTest]
        public IEnumerator EmitterManual()
        {
            Setup(3);

            var launchPad1NodeRole =
                GetLaunchParameterForReview(m_LaunchComplexesId[1], LaunchParameterConstants.NodeRoleParameterId);
            launchPad1NodeRole.Value.Value = LaunchParameterConstants.NodeRoleEmitter;

            yield return Process();

            TestLaunchParameterForReview(m_LaunchComplexesId[0], LaunchParameterConstants.NodeIdParameterId, 0);
            TestLaunchParameterForReview(m_LaunchComplexesId[0], LaunchParameterConstants.NodeRoleParameterId,
                LaunchParameterConstants.NodeRoleRepeater);

            TestLaunchParameterForReview(m_LaunchComplexesId[1], LaunchParameterConstants.NodeIdParameterId, 1);
            TestLaunchParameterForReview(m_LaunchComplexesId[1], LaunchParameterConstants.NodeRoleParameterId,
                LaunchParameterConstants.NodeRoleEmitter);

            TestLaunchParameterForReview(m_LaunchComplexesId[2], LaunchParameterConstants.NodeIdParameterId, 2);
            TestLaunchParameterForReview(m_LaunchComplexesId[2], LaunchParameterConstants.NodeRoleParameterId,
                LaunchParameterConstants.NodeRoleRepeater);
        }

        [UnityTest]
        public IEnumerator EmitterTooMany()
        {
            Setup(3);

            var launchPad1NodeRole =
                GetLaunchParameterForReview(m_LaunchComplexesId[1], LaunchParameterConstants.NodeRoleParameterId);
            launchPad1NodeRole.Value.Value = LaunchParameterConstants.NodeRoleEmitter;
            var launchPad2NodeRole =
                GetLaunchParameterForReview(m_LaunchComplexesId[2], LaunchParameterConstants.NodeRoleParameterId);
            launchPad2NodeRole.Value.Value = LaunchParameterConstants.NodeRoleEmitter;

            yield return Process();

            TestLaunchParameterForReview(m_LaunchComplexesId[0], LaunchParameterConstants.NodeIdParameterId, 0);
            TestLaunchParameterForReview(m_LaunchComplexesId[0], LaunchParameterConstants.NodeRoleParameterId,
                LaunchParameterConstants.NodeRoleRepeater);

            TestLaunchParameterForReview(m_LaunchComplexesId[1], LaunchParameterConstants.NodeIdParameterId, 1);
            TestLaunchParameterForReview(m_LaunchComplexesId[1], LaunchParameterConstants.NodeRoleParameterId,
                LaunchParameterConstants.NodeRoleEmitter);

            TestLaunchParameterForReview(m_LaunchComplexesId[2], LaunchParameterConstants.NodeIdParameterId, 2);
            TestLaunchParameterForReview(m_LaunchComplexesId[2], LaunchParameterConstants.NodeRoleParameterId,
                LaunchParameterConstants.NodeRoleRepeater, "There can only be one emitter, role changed to repeater.");
        }

        [UnityTest]
        public IEnumerator EmitterLowestNodeIdToEmitterUnassigned()
        {
            Setup(3);

            var launchPad1NodeId =
                GetLaunchParameterForReview(m_LaunchComplexesId[1], LaunchParameterConstants.NodeIdParameterId);
            launchPad1NodeId.Value.Value = 0;

            yield return Process();

            TestLaunchParameterForReview(m_LaunchComplexesId[0], LaunchParameterConstants.NodeIdParameterId, 1);
            TestLaunchParameterForReview(m_LaunchComplexesId[0], LaunchParameterConstants.NodeRoleParameterId,
                LaunchParameterConstants.NodeRoleRepeater);

            TestLaunchParameterForReview(m_LaunchComplexesId[1], LaunchParameterConstants.NodeIdParameterId, 0);
            TestLaunchParameterForReview(m_LaunchComplexesId[1], LaunchParameterConstants.NodeRoleParameterId,
                LaunchParameterConstants.NodeRoleEmitter);

            TestLaunchParameterForReview(m_LaunchComplexesId[2], LaunchParameterConstants.NodeIdParameterId, 2);
            TestLaunchParameterForReview(m_LaunchComplexesId[2], LaunchParameterConstants.NodeRoleParameterId,
                LaunchParameterConstants.NodeRoleRepeater);
        }

        [UnityTest]
        public IEnumerator EmitterLowestNodeIdToEmitterRepeater()
        {
            Setup(3);

            var launchPad1NodeId =
                GetLaunchParameterForReview(m_LaunchComplexesId[1], LaunchParameterConstants.NodeIdParameterId);
            launchPad1NodeId.Value.Value = 0;
            var launchPad1NodeRole =
                GetLaunchParameterForReview(m_LaunchComplexesId[1], LaunchParameterConstants.NodeRoleParameterId);
            launchPad1NodeRole.Value.Value = LaunchParameterConstants.NodeRoleRepeater;

            yield return Process();

            TestLaunchParameterForReview(m_LaunchComplexesId[0], LaunchParameterConstants.NodeIdParameterId, 1);
            TestLaunchParameterForReview(m_LaunchComplexesId[0], LaunchParameterConstants.NodeRoleParameterId,
                LaunchParameterConstants.NodeRoleRepeater);

            TestLaunchParameterForReview(m_LaunchComplexesId[1], LaunchParameterConstants.NodeIdParameterId, 0);
            TestLaunchParameterForReview(m_LaunchComplexesId[1], LaunchParameterConstants.NodeRoleParameterId,
                LaunchParameterConstants.NodeRoleEmitter, "Changed to emitter as there was no emitter assigned to " +
                "any node.");

            TestLaunchParameterForReview(m_LaunchComplexesId[2], LaunchParameterConstants.NodeIdParameterId, 2);
            TestLaunchParameterForReview(m_LaunchComplexesId[2], LaunchParameterConstants.NodeRoleParameterId,
                LaunchParameterConstants.NodeRoleRepeater);
        }

        [UnityTest]
        public IEnumerator SkipLaunchpadsOfOtherType()
        {
            Setup(2);

            var launchpadToChangeTypeOf = m_Mirror.Complexes[m_LaunchComplexesId[1]].LaunchPads.First();
            launchpadToChangeTypeOf.SuitableFor.Clear();
            launchpadToChangeTypeOf.SuitableFor.Add("Something Else");

            yield return Process();

            TestLaunchParameterForReview(m_LaunchComplexesId[0], LaunchParameterConstants.NodeIdParameterId, 0);
            TestLaunchParameterForReview(m_LaunchComplexesId[0], LaunchParameterConstants.NodeRoleParameterId,
                LaunchParameterConstants.NodeRoleEmitter);
            TestLaunchParameterForReview(m_LaunchComplexesId[0], LaunchParameterConstants.RepeaterCountParameterId, 0);

            var notReviewed = GetLaunchParameterForReview(m_LaunchComplexesId[1],
                LaunchParameterConstants.NodeIdParameterId);
            Assert.That(notReviewed.Ready, Is.False);
            notReviewed = GetLaunchParameterForReview(m_LaunchComplexesId[1], LaunchParameterConstants.NodeRoleParameterId);
            Assert.That(notReviewed.Ready, Is.False);
            notReviewed = GetLaunchParameterForReview(m_LaunchComplexesId[1], LaunchParameterConstants.RepeaterCountParameterId);
            Assert.That(notReviewed.Ready, Is.False);
        }

        [UnityTest]
        public IEnumerator SkipLaunchablesOfOtherType()
        {
            Setup(2);

            Asset newAsset = new(Guid.NewGuid());
            newAsset.Launchables.Add(new() {
                Name = "Other Launchable", Type = "Something Else"
            });
            m_Mirror.Assets.Add(newAsset);

            var launchpadToChangeTypeOf = m_Mirror.Complexes[m_LaunchComplexesId[1]].LaunchPads.First();
            launchpadToChangeTypeOf.SuitableFor.Add("Something Else");
            var launchComplexConfigurationToModify = m_Mirror.LaunchConfiguration.LaunchComplexes.First(
                lcc => lcc.Identifier == m_LaunchComplexesId[1]);
            launchComplexConfigurationToModify.LaunchPads.First().LaunchableName = "Other Launchable";

            yield return Process();

            TestLaunchParameterForReview(m_LaunchComplexesId[0], LaunchParameterConstants.NodeIdParameterId, 0);
            TestLaunchParameterForReview(m_LaunchComplexesId[0], LaunchParameterConstants.NodeRoleParameterId,
                LaunchParameterConstants.NodeRoleEmitter);
            TestLaunchParameterForReview(m_LaunchComplexesId[0], LaunchParameterConstants.RepeaterCountParameterId, 0);

            var notReviewed = GetLaunchParameterForReview(m_LaunchComplexesId[1],
                LaunchParameterConstants.NodeIdParameterId);
            Assert.That(notReviewed.Ready, Is.False);
            notReviewed = GetLaunchParameterForReview(m_LaunchComplexesId[1], LaunchParameterConstants.NodeRoleParameterId);
            Assert.That(notReviewed.Ready, Is.False);
            notReviewed = GetLaunchParameterForReview(m_LaunchComplexesId[1], LaunchParameterConstants.RepeaterCountParameterId);
            Assert.That(notReviewed.Ready, Is.False);
        }

        [UnityTest]
        public IEnumerator CapsulePortOneLaunchPad()
        {
            Setup(2);

            yield return Process();

            TestLaunchParameterForReview(m_LaunchComplexesId[0], LaunchParameterConstants.CapsuleBasePortParameterId,
                LaunchParameterConstants.DefaultCapsuleBasePort);
            TestLaunchParameterForReview(m_LaunchComplexesId[1], LaunchParameterConstants.CapsuleBasePortParameterId,
                LaunchParameterConstants.DefaultCapsuleBasePort);
        }

        [UnityTest]
        public IEnumerator CapsulePortTwoLaunchPads()
        {
            Setup(2);

            var newLaunchPad = CreateNewLaunchpad();
            m_Mirror.Complexes[m_LaunchComplexesId[0]].LaunchPads.Add(newLaunchPad);
            m_Mirror.LaunchConfiguration.LaunchComplexes
                .First(lc => lc.Identifier == m_LaunchComplexesId[0])
                .LaunchPads.Add(new LaunchPadConfiguration() {
                            Identifier = newLaunchPad.Identifier, LaunchableName = "Test Launchable"});

            yield return Process();

            TestLaunchParameterForReview(m_LaunchComplexesId[0], LaunchParameterConstants.CapsuleBasePortParameterId,
                LaunchParameterConstants.DefaultCapsuleBasePort);
            TestLaunchPadParameterForReview(newLaunchPad.Identifier, LaunchParameterConstants.CapsuleBasePortParameterId,
                LaunchParameterConstants.DefaultCapsuleBasePort + 1);
            TestLaunchParameterForReview(m_LaunchComplexesId[1], LaunchParameterConstants.CapsuleBasePortParameterId,
                LaunchParameterConstants.DefaultCapsuleBasePort);
        }

        [UnityTest]
        public IEnumerator CapsuleInvalidTypeToDefault()
        {
            Setup(1);

            var capsulePort =
                GetLaunchParameterForReview(m_LaunchComplexesId[0], LaunchParameterConstants.CapsuleBasePortParameterId);
            capsulePort.Value.Value = "Shouldn't be a string";

            yield return Process();

            TestLaunchParameterForReview(m_LaunchComplexesId[0], LaunchParameterConstants.CapsuleBasePortParameterId,
                LaunchParameterConstants.DefaultCapsuleBasePort, "Must be an integer.");
        }

        [UnityTest]
        public IEnumerator KeepLaunchpadsInformationUntilNextLaunch(
            [Values(State.Idle, State.Preparing)] State nextLaunchState)
        {
            Setup(1);

            yield return Process();

            TestLaunchParameterForReview(m_LaunchComplexesId[0], LaunchParameterConstants.NodeIdParameterId, 0);
            TestLaunchParameterForReview(m_LaunchComplexesId[0], LaunchParameterConstants.NodeRoleParameterId,
                LaunchParameterConstants.NodeRoleEmitter);
            TestLaunchParameterForReview(m_LaunchComplexesId[0], LaunchParameterConstants.RepeaterCountParameterId, 0);
            var savedLaunchPadInformation = m_Mirror.LaunchPadsInformation.First();

            m_Mirror.LaunchParametersForReview.Add(new(Guid.NewGuid()){LaunchPadId = Guid.NewGuid(), Ready = false});
            ++m_Mirror.LaunchParametersForReviewNextVersion;
            yield return Process();
            Assert.That(m_Mirror.LaunchPadsInformation.First(), Is.SameAs(savedLaunchPadInformation));

            m_Mirror.Status.State = State.Failure;
            ++m_Mirror.StatusNextVersion;
            yield return Process();
            Assert.That(m_Mirror.LaunchPadsInformation.First(), Is.SameAs(savedLaunchPadInformation));

            m_Mirror.CapcomUplink.ProceedWithLanding = true;
            ++m_Mirror.CapcomUplinkNextVersion;
            yield return Process();
            Assert.That(m_Mirror.LaunchPadsInformation.First(), Is.SameAs(savedLaunchPadInformation));

            m_Mirror.Status.State = nextLaunchState;
            m_Mirror.Status.EnteredStateTime += TimeSpan.FromMinutes(1);
            ++m_Mirror.StatusNextVersion;
            yield return Process();
            if (nextLaunchState == State.Idle)
            {
                Assert.That(m_Mirror.LaunchPadsInformation, Is.Empty);
            }
            else
            {
                Assert.That(m_Mirror.LaunchPadsInformation.First(), Is.Not.SameAs(savedLaunchPadInformation));
            }
        }

        void Setup(int nodesCount)
        {
            m_Mirror.Status.State = State.Preparing;
            m_Mirror.Status.EnteredStateTime = DateTime.Now;

            var assetId = Guid.NewGuid();
            m_Mirror.Assets.Add(new(assetId));
            m_Mirror.Assets.Values.First().Launchables.Add(new() {
                Name = "Test Launchable", Type = LaunchCatalog.Launchable.ClusterNodeType
            });

            for (int nodeIndex = 0; nodeIndex < nodesCount; ++nodeIndex)
            {
                var launchComplexId = Guid.NewGuid();
                m_LaunchComplexesId.Add(launchComplexId);
                LaunchComplex launchComplex = new(launchComplexId)
                {
                    LaunchPads = {CreateNewLaunchpad()}
                };
                m_Mirror.Complexes.Add(launchComplex);
                m_Mirror.LaunchConfiguration.AssetId = assetId;
                m_Mirror.LaunchConfiguration.LaunchComplexes.Add( new() {
                    Identifier = launchComplex.Id,
                    LaunchPads = { new LaunchPadConfiguration() {
                        Identifier = launchComplex.LaunchPads[0].Identifier,
                        LaunchableName = "Test Launchable"
                    }}
                });
            }
        }

        MissionControl.LaunchPad CreateNewLaunchpad()
        {
            MissionControl.LaunchPad ret = new() {
                Identifier = Guid.NewGuid(),
                SuitableFor = {LaunchCatalog.Launchable.ClusterNodeType}
            };

            m_MissionControlStub.LaunchParametersForReview.Add(new(Guid.NewGuid()) {
                LaunchPadId = ret.Identifier, Value = new() {
                    Id = LaunchParameterConstants.NodeIdParameterId, Value = -1
                }
            });
            m_MissionControlStub.LaunchParametersForReview.Add(new(Guid.NewGuid()) {
                LaunchPadId = ret.Identifier, Value = new() {
                    Id = LaunchParameterConstants.NodeRoleParameterId,
                    Value = LaunchParameterConstants.NodeRoleUnassigned
                }
            });
            m_MissionControlStub.LaunchParametersForReview.Add(new(Guid.NewGuid()) {
                LaunchPadId = ret.Identifier, Value = new() {
                    Id = LaunchParameterConstants.RepeaterCountParameterId, Value = 0
                }
            });
            m_MissionControlStub.LaunchParametersForReview.Add(new(Guid.NewGuid()) {
                LaunchPadId = ret.Identifier, Value = new() {
                    Id = LaunchParameterConstants.CapsuleBasePortParameterId,
                    Value = LaunchParameterConstants.DefaultCapsuleBasePort
                }
            });

            return ret;
        }

        IEnumerator Process()
        {
            var toReviewDelta = m_MissionControlStub.LaunchParametersForReview.GetDeltaSince(0);
            m_Mirror.LaunchParametersForReview.ApplyDelta(toReviewDelta);

            // Need to be ran asynchronously because implementation of Process make REST calls that need to be awaited
            // on exploiting a problem with NUnit running in Unity.
            yield return Task.Run(() => m_Process.Process(m_Mirror)).AsIEnumerator();
        }

        LaunchParameterForReview GetLaunchParameterForReview(Guid launchComplexId, string parameterId)
        {
            var launchPadId = m_Mirror.Complexes[launchComplexId].LaunchPads.First().Identifier;
            return m_MissionControlStub.LaunchParametersForReview.Values.First(lpfr =>
                lpfr.LaunchPadId == launchPadId && lpfr.Value.Id == parameterId);
        }

        void TestLaunchParameterForReview(Guid launchComplexId, string parameterId, object value, string comments = "")
        {
            var launchParameterForReview = GetLaunchParameterForReview(launchComplexId, parameterId);
            Assert.That(launchParameterForReview.Ready, Is.True);
            Assert.That(launchParameterForReview.Value.Value, Is.EqualTo(value));
            Assert.That(launchParameterForReview.ReviewComments, Is.EqualTo(comments));
        }

        void TestLaunchPadParameterForReview(Guid launchPadId, string parameterId, object value, string comments = "")
        {
            var launchParameterForReview = m_MissionControlStub.LaunchParametersForReview.Values.First(lpfr =>
                lpfr.LaunchPadId == launchPadId && lpfr.Value.Id == parameterId);
            Assert.That(launchParameterForReview.Ready, Is.True);
            Assert.That(launchParameterForReview.Value.Value, Is.EqualTo(value));
            Assert.That(launchParameterForReview.ReviewComments, Is.EqualTo(comments));
        }

        MissionControlStub m_MissionControlStub = new();
        List<Guid> m_LaunchComplexesId = new();
        MissionControlMirror m_Mirror;
        ReviewLaunchParametersProcess m_Process;
    }
}
