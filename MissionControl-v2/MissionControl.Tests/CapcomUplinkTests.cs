using System;
using System.Text.Json;

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
            var serializedParameter = JsonSerializer.Serialize(toSerialize, Json.SerializerOptions);
            var deserialized = JsonSerializer.Deserialize<CapcomUplink>(serializedParameter, Json.SerializerOptions);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void SerializeFull()
        {
            CapcomUplink toSerialize = new();
            toSerialize.IsRunning = true;
            toSerialize.ProceedWithLanding = true;
            var serializedParameter = JsonSerializer.Serialize(toSerialize, Json.SerializerOptions);
            var deserialized = JsonSerializer.Deserialize<CapcomUplink>(serializedParameter, Json.SerializerOptions);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }
    }
}
