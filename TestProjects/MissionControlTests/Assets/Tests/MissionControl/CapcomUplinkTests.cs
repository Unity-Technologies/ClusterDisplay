using Newtonsoft.Json;
using NUnit.Framework;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    public class CapcomUplinkTests
    {
        [Test]
        public void Equal()
        {
            CapcomUplink uplinkA = new();
            CapcomUplink uplinkB = new();
            Assert.That(uplinkA, Is.EqualTo(uplinkB));

            uplinkA.IsRunning = true;
            uplinkB.IsRunning = false;
            Assert.That(uplinkA, Is.Not.EqualTo(uplinkB));
            uplinkB.IsRunning = true;
            Assert.That(uplinkA, Is.EqualTo(uplinkB));

            uplinkA.ProceedWithLanding = true;
            uplinkB.ProceedWithLanding = false;
            Assert.That(uplinkA, Is.Not.EqualTo(uplinkB));
            uplinkB.ProceedWithLanding = true;
            Assert.That(uplinkA, Is.EqualTo(uplinkB));
        }

        [Test]
        public void SerializeDefault()
        {
            CapcomUplink toSerialize = new();
            var serializedParameter = JsonConvert.SerializeObject(toSerialize, Json.SerializerOptions);
            var deserialized = JsonConvert.DeserializeObject<CapcomUplink>(serializedParameter, Json.SerializerOptions);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void SerializeFull()
        {
            CapcomUplink toSerialize = new();
            toSerialize.IsRunning = true;
            toSerialize.ProceedWithLanding = true;
            var serializedParameter = JsonConvert.SerializeObject(toSerialize, Json.SerializerOptions);
            var deserialized = JsonConvert.DeserializeObject<CapcomUplink>(serializedParameter, Json.SerializerOptions);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }
    }
}
