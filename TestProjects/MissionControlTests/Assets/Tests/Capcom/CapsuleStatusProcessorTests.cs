using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NUnit.Framework;
using Unity.ClusterDisplay.MissionControl.Capsule;
using Unity.ClusterDisplay.MissionControl.MissionControl;
using UnityEngine.TestTools;

namespace Unity.ClusterDisplay.MissionControl.Capcom
{
    public class CapsuleStatusProcessorTests
    {
        [SetUp]
        public void SetUp()
        {
            HttpClient missionControlClient = new();
            missionControlClient.BaseAddress = MissionControlStub.HttpListenerEndpoint;
            m_Mirror = new(missionControlClient);

            m_MissionControlStub.Start();
            m_MissionControlStub.LaunchParametersForReview.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            m_MissionControlStub.Stop();
        }

        static readonly ValueTuple<byte, string>[] k_Roles = {(0, "Unassigned"), (1, "Emitter"), (2, "Repeater")};

        [UnityTest]
        public IEnumerator Working([ValueSource(nameof(k_Roles))] ValueTuple<byte, string> role)
        {
            Guid launchPadId = Guid.NewGuid();
            string dynamicEntriesJson = "";
            m_MissionControlStub.FallbackHandler = (uri, request, response) =>
            {
                if (uri == $"api/v1/launchPadsStatus/{launchPadId}/dynamicEntries" &&
                    request.HttpMethod == HttpMethod.Put.ToString() && request.ContentType == "application/json")
                {
                    using (StreamReader reader = new(request.InputStream))
                    {
                        dynamicEntriesJson = reader.ReadToEnd();
                    }
                    MissionControlStub.Respond(response, HttpStatusCode.OK);
                }
                else
                {
                    MissionControlStub.Respond(response, HttpStatusCode.NotFound);
                }
            };

            var bytesBuffer = new byte[Marshal.SizeOf<CapsuleStatusMessage>()];
            MemoryStream memoryStream = new();
            CapsuleStatusMessage capsuleStatusMessage = new() {NodeRole = role.Item1, NodeId = 28, RenderNodeId = 42};
            memoryStream.WriteStructAsync(capsuleStatusMessage, bytesBuffer);
            memoryStream.Position = 0;

            CapsuleStatusProcessor processor = new();
            // Need to be ran asynchronously because implementation of Process make REST calls that need to be awaited
            // on exploiting a problem with NUnit running in Unity.
            Task.Run(() => processor.Process(m_Mirror, launchPadId, memoryStream));
            int stopAtLength = Marshal.SizeOf<CapsuleStatusMessage>() + Marshal.SizeOf<CapsuleStatusResponse>();
            while (memoryStream.Length != stopAtLength)
            {
                yield return null;
            }

            var entries = JsonConvert.DeserializeObject<LaunchPadReportDynamicEntry[]>(dynamicEntriesJson,
                Json.SerializerOptions);
            Assert.That(entries, Is.Not.Null);
            Assert.That(entries.Length, Is.EqualTo(3));
            Assert.That(entries[0].Name, Is.EqualTo("Role"));
            Assert.That(entries[0].Value, Is.EqualTo(role.Item2));
            Assert.That(entries[1].Name, Is.EqualTo("Node id"));
            Assert.That(entries[1].Value, Is.EqualTo(28));
            Assert.That(entries[2].Name, Is.EqualTo("Render node id"));
            Assert.That(entries[2].Value, Is.EqualTo(42));
        }

        MissionControlMirror m_Mirror;
        MissionControlStub m_MissionControlStub = new();
    }
}
