using System.Text.Json;

namespace Unity.ClusterDisplay.MissionControl.MissionControl.Tests
{
    public class LaunchPadTests
    {
        [Test]
        public void Equal()
        {
            LaunchPad launchPadA = new();
            LaunchPad launchPadB = new();
            Assert.That(launchPadA, Is.EqualTo(launchPadB));

            launchPadA.Identifier = Guid.NewGuid();
            launchPadB.Identifier = Guid.NewGuid();
            Assert.That(launchPadA, Is.Not.EqualTo(launchPadB));
            launchPadB.Identifier = launchPadA.Identifier;
            Assert.That(launchPadA, Is.EqualTo(launchPadB));

            launchPadA.Name = "Top left";
            launchPadB.Name = "Top right";
            Assert.That(launchPadA, Is.Not.EqualTo(launchPadB));
            launchPadB.Name = "Top left";
            Assert.That(launchPadA, Is.EqualTo(launchPadB));

            launchPadA.Endpoint = new("http://1.2.3.4:8200");
            launchPadB.Endpoint = new("http://1.2.3.5:8200");
            Assert.That(launchPadA, Is.Not.EqualTo(launchPadB));
            launchPadB.Endpoint = new("http://1.2.3.4:8200");
            Assert.That(launchPadA, Is.EqualTo(launchPadB));

            launchPadA.SuitableFor = new[] { "clusterNode", "liveEdit" };
            launchPadB.SuitableFor = new[] { "clusterNode" };
            Assert.That(launchPadA, Is.Not.EqualTo(launchPadB));
            launchPadB.SuitableFor = new[] { "clusterNode", "liveEdit" };
            Assert.That(launchPadA, Is.EqualTo(launchPadB));
        }

        [Test]
        public void SerializeDefault()
        {
            LaunchPad toSerialize = new();
            var serializedParameter = JsonSerializer.Serialize(toSerialize, Json.SerializerOptions);
            var deserialized = JsonSerializer.Deserialize<LaunchPad>(serializedParameter, Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void SerializeFull()
        {
            LaunchPad toSerialize = new();
            toSerialize.Identifier = Guid.NewGuid();
            toSerialize.Name = "Bottom left";
            toSerialize.Endpoint = new("http://1.2.3.6:8200");
            toSerialize.SuitableFor = new[] { "clusterNode" };
            var serializedParameter = JsonSerializer.Serialize(toSerialize, Json.SerializerOptions);
            var deserialized = JsonSerializer.Deserialize<LaunchPad>(serializedParameter, Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void DeepCloneDefault()
        {
            LaunchPad toClone = new();
            var cloned = toClone.DeepClone();
            Assert.That(cloned, Is.EqualTo(toClone));
        }

        [Test]
        public void DeepCloneFull()
        {
            LaunchPad toClone = new();
            toClone.Identifier = Guid.NewGuid();
            toClone.Name = "Bottom left";
            toClone.Endpoint = new("http://1.2.3.6:8200");
            toClone.SuitableFor = new[] { "clusterNode" };
            var cloned = toClone.DeepClone();
            Assert.That(cloned, Is.EqualTo(toClone));
        }
    }
}
