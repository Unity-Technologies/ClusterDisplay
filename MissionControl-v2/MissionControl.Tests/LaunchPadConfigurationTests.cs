using System.Text.Json;

namespace Unity.ClusterDisplay.MissionControl.MissionControl.Tests
{
    public class LaunchPadConfigurationTests
    {
        [Test]
        public void Equal()
        {
            LaunchPadConfiguration launchPadA = new();
            LaunchPadConfiguration launchPadB = new();
            Assert.That(launchPadA, Is.EqualTo(launchPadB));

            launchPadA.Identifier = Guid.NewGuid();
            launchPadB.Identifier = Guid.NewGuid();
            Assert.That(launchPadA, Is.Not.EqualTo(launchPadB));
            launchPadB.Identifier = launchPadA.Identifier;
            Assert.That(launchPadA, Is.EqualTo(launchPadB));

            launchPadA.LaunchableName = "ClusterNode";
            launchPadB.LaunchableName = "LiveEdit";
            Assert.That(launchPadA, Is.Not.EqualTo(launchPadB));
            launchPadB.LaunchableName = "ClusterNode";
            Assert.That(launchPadA, Is.EqualTo(launchPadB));
        }

        [Test]
        public void SerializeDefault()
        {
            LaunchPadConfiguration toSerialize = new();
            var serializedParameter = JsonSerializer.Serialize(toSerialize, Json.SerializerOptions);
            var deserialized = JsonSerializer.Deserialize<LaunchPadConfiguration>(serializedParameter, Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void SerializeFull()
        {
            LaunchPadConfiguration toSerialize = new();
            toSerialize.Identifier = Guid.NewGuid();
            toSerialize.LaunchableName = "ClusterNode";
            var serializedParameter = JsonSerializer.Serialize(toSerialize, Json.SerializerOptions);
            var deserialized = JsonSerializer.Deserialize<LaunchPadConfiguration>(serializedParameter, Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void DeepCloneDefault()
        {
            LaunchPadConfiguration toClone = new();
            var cloned = toClone.DeepClone();
            Assert.That(cloned, Is.EqualTo(toClone));
        }

        [Test]
        public void DeepCloneFull()
        {
            LaunchPadConfiguration toClone = new();
            toClone.Identifier = Guid.NewGuid();
            toClone.LaunchableName = "ClusterNode";
            var cloned = toClone.DeepClone();
            Assert.That(cloned, Is.EqualTo(toClone));
        }
    }
}
