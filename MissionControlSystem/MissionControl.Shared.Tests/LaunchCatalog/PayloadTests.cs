using System.Text.Json;

namespace Unity.ClusterDisplay.MissionControl.LaunchCatalog
{
    public class PayloadTests
    {
        [Test]
        public void Equal()
        {
            Payload payloadA = new();
            Payload payloadB = new();
            Assert.That(payloadA, Is.EqualTo(payloadB));

            payloadA.Name = "ForClusterNodes";
            payloadB.Name = "ForLiveEditor";
            Assert.That(payloadA, Is.Not.EqualTo(payloadB));
            payloadB.Name = "ForClusterNodes";
            Assert.That(payloadA, Is.EqualTo(payloadB));

            payloadA.Files = new[] { new PayloadFile() { Path = "SpaceshipDemo_Data/sharedassets3.assets" },
                new PayloadFile() { Path = "SpaceshipDemo.exe" } };
            payloadB.Files = new[] { new PayloadFile() { Path = "SpaceshipDemo_Data/sharedassets3.assets" } };
            Assert.That(payloadA, Is.Not.EqualTo(payloadB));
            payloadB.Files = new[] { new PayloadFile() { Path = "SpaceshipDemo_Data/sharedassets3.assets" },
                new PayloadFile() { Path = "SpaceshipDemo.exe" } };
            Assert.That(payloadA, Is.EqualTo(payloadB));
        }

        [Test]
        public void SerializeDefault()
        {
            Payload toSerialize = new();
            var serializedParameter = JsonSerializer.Serialize(toSerialize, Json.SerializerOptions);
            var deserialized = JsonSerializer.Deserialize<Payload>(serializedParameter, Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void SerializeFull()
        {
            Payload toSerialize = new();
            toSerialize.Name = "ForClusterNodes";
            toSerialize.Files = new[] { new PayloadFile() { Path = "SpaceshipDemo_Data/sharedassets3.assets" },
                new PayloadFile() { Path = "SpaceshipDemo.exe" } };
            var serializedParameter = JsonSerializer.Serialize(toSerialize, Json.SerializerOptions);
            var deserialized = JsonSerializer.Deserialize<Payload>(serializedParameter, Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }
    }
}
