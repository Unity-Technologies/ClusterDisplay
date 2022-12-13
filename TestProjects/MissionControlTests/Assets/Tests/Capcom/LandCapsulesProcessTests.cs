using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Unity.ClusterDisplay.MissionControl.Capcom
{
    public class LandCapsulesProcessTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var capsule in m_Capsules)
            {
                capsule.ProcessingLoop.Stop();
                yield return capsule.ProcessingTask.AsIEnumerator();
            }
            m_Capsules.Clear();
        }

        [Test]
        public void TestLanding()
        {
            AddFakeCapsule(k_TestPort+0);
            AddFakeCapsule(k_TestPort+1);

            LandCapsulesProcess testProcess = new();
            m_Mirror.CapcomUplink.ProceedWithLanding = true;
            testProcess.Process(m_Mirror);

            Assert.That(m_Capsules[0].FakeLandHandler.Called, Is.True);
            Assert.That(m_Capsules[1].FakeLandHandler.Called, Is.True);
        }

        class FakeLandHandler : Capsule.IMessageHandler
        {
            public async ValueTask HandleMessage(NetworkStream networkStream)
            {
                await networkStream.ReadStructAsync<Capsule.LandMessage>(m_MessageBuffer).ConfigureAwait(false);
                await networkStream.WriteStructAsync(new Capsule.LandResponse(), m_ResponseBuffer).ConfigureAwait(false);
                Called = true;
            }

            public bool Called { get; private set; }

            byte[] m_MessageBuffer = new byte[Marshal.SizeOf<Capsule.LandMessage>()];
            byte[] m_ResponseBuffer = new byte[Marshal.SizeOf<Capsule.LandResponse>()];
        }

        class FakeCapsule
        {
            public FakeCapsule(int port)
            {
                ProcessingLoop.AddMessageHandler(Capsule.MessagesId.Land, FakeLandHandler);
                ProcessingTask = ProcessingLoop.Start(port);
            }

            public Capsule.ProcessingLoop ProcessingLoop { get; } = new(false);
            public FakeLandHandler FakeLandHandler { get; } = new();
            public Task ProcessingTask { get; }
        }

        void AddFakeCapsule(int port)
        {
            m_Capsules.Add(new(port));
            m_Mirror.LaunchPadsInformation.Add(new () {
                Definition = new() {
                    Endpoint = new("http://127.0.0.1:1234")
                },
                CapsulePort = port
            });
        }

        const int k_TestPort = Helpers.ListenPort;
        List<FakeCapsule> m_Capsules = new();
        MissionControlMirror m_Mirror = new(new());
    }
}
