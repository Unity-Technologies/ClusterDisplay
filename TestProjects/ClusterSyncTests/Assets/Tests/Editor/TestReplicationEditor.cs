using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using NUnit.Framework;
using Unity.ClusterDisplay.Scripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Unity.ClusterDisplay.Tests.Editor
{
    public class TestReplicationEditor
    {
        GameObject m_SourceObject;
        GameObject m_DestObject;
        EditorLinkConfig m_LinkConfig;

        ClusterReplication m_Replication;

        [SetUp]
        public void SetUp()
        {
            m_SourceObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            m_SourceObject.name = "Source";
            m_DestObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            m_DestObject.name = "Dest";

            m_SourceObject.AddComponent<SomeComponent>();
            m_DestObject.AddComponent<SomeComponent>();

            m_Replication = m_SourceObject.AddComponent<ClusterReplication>();

            m_LinkConfig = ScriptableObject.CreateInstance<EditorLinkConfig>();
            m_LinkConfig.Parse("127.0.0.1:40001");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(m_SourceObject);
            Object.DestroyImmediate(m_DestObject);
            Object.DestroyImmediate(m_LinkConfig);
        }

        [SpecializedReplicator(typeof(MeshRenderer))]
        class DummyReplicator
        {
            public DummyReplicator(Guid guid, MeshRenderer meshRenderer) { }
        }

        [Test]
        public void TestSpecializedReplicatorRegistration()
        {
            Assert.IsTrue(ClusterReplication.HasSpecializedReplicator(typeof(Transform)));
            Assert.IsTrue(ClusterReplication.HasSpecializedReplicator(typeof(MeshRenderer)));
            Assert.IsFalse(ClusterReplication.HasSpecializedReplicator(typeof(SomeComponent)));

        }

        [Test]
        public void TestAddReplicationTarget()
        {
            // invalid target (no property specified and no replicator specialization)
            m_Replication.AddTarget(m_SourceObject.GetComponent<SomeComponent>());

            m_Replication.AddTarget(m_SourceObject.GetComponent<SomeComponent>(), "MyProperty");
            m_Replication.AddTarget(m_SourceObject.GetComponent<Transform>());

            Assert.That(m_Replication.Replicators, Has.Count.EqualTo(3));

            // Call this to make sure the IsValid status appears correct in the inspector
            m_Replication.OnBeforeSerialize();

            foreach (var (target, replicator) in m_Replication.Replicators)
            {
                if (target.Component is Transform || !string.IsNullOrEmpty(target.Property))
                {
                    Assert.That(target.IsValid, Is.True);
                    Assert.That(replicator.IsValid, Is.True);
                }
                else
                {
                    Assert.That(target.IsValid, Is.False);
                    Assert.That(replicator.IsValid, Is.False);
                }
            }
        }

        void EnableEditorLink(ClusterReplication replication)
        {
            var serializedObject = new SerializedObject(replication);
            var editorLinkProp = serializedObject.FindProperty("m_EditorLinkConfig");
            editorLinkProp.objectReferenceValue = m_LinkConfig;
            serializedObject.ApplyModifiedProperties();
            replication.OnEditorLinkChanged();
        }

        static void DisableEditorLink(ClusterReplication replication)
        {
            var serializedObject = new SerializedObject(replication);
            var editorLinkProp = serializedObject.FindProperty("m_EditorLinkConfig");
            editorLinkProp.objectReferenceValue = null;
            serializedObject.ApplyModifiedProperties();
            replication.OnEditorLinkChanged();
        }

        [UnityTest]
        public IEnumerator TestEditorLink()
        {
            using var udpClient = new UdpClient(m_LinkConfig.Port);
            udpClient.Client.ReceiveTimeout = 500;
            m_Replication.AddTarget(m_SourceObject.GetComponent<Transform>());

            Assert.That(m_Replication.EditorLink, Is.Null);

            // Note: All replicators currently operating in Editor mode because this is the default
            // behaviour when running in the Editor without cluster logic enabled.

            EnableEditorLink(m_Replication);
            Assert.That(m_Replication.EditorLink, Is.Not.Null);

            // ReSharper disable once HeuristicUnreachableCode

            // Create another replication behaviour in the scene
            var anotherReplicator = m_DestObject.AddComponent<ClusterReplication>();

            EnableEditorLink(anotherReplicator);

            // There should be only one editor link instance in for all Replication scripts using
            // the same config
            Assert.That(anotherReplicator.EditorLink, Is.EqualTo(m_Replication.EditorLink));

            // Check that disabling works
            DisableEditorLink(anotherReplicator);
            Assert.IsNull(anotherReplicator.EditorLink);

            // Check that we have a property replicator
            var transformReplicator = m_Replication.Replicators.Values.First() ;
            Assert.NotNull(transformReplicator);

            yield return null;

            m_SourceObject.transform.Translate(3, 4, 5);
            m_SourceObject.transform.Rotate(30, 60, 90);

            // A change in the transform should trigger a link message to be emitted.
            yield return null;

            IPEndPoint remoteEndPoint = default;
            var bytes = udpClient.Receive(ref remoteEndPoint);
            var message = bytes.LoadStruct<ReplicationMessage<TransformMessage>>();
            Assert.That(message.Guid, Is.EqualTo(transformReplicator.Guid));
            Assert.That(message.Contents,
                Is.EqualTo(new TransformMessage(m_SourceObject.transform))
                    .Using(new TransformMessageComparer()));
        }
    }
}
