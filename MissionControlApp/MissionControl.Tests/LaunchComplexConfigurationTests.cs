using System.Text.Json;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    public class LaunchComplexConfigurationTests
    {
        [Test]
        public void Equal()
        {
            LaunchComplexConfiguration launchComplexA = new();
            LaunchComplexConfiguration launchComplexB = new();
            Assert.That(launchComplexA, Is.EqualTo(launchComplexB));

            launchComplexA.Identifier = Guid.NewGuid();
            launchComplexB.Identifier = Guid.NewGuid();
            Assert.That(launchComplexA, Is.Not.EqualTo(launchComplexB));
            launchComplexB.Identifier = launchComplexA.Identifier;
            Assert.That(launchComplexA, Is.EqualTo(launchComplexB));

            launchComplexA.Parameters = new[] { new LaunchParameterValue { Id = "NodeId", Value = 42 } };
            launchComplexB.Parameters = new[] { new LaunchParameterValue { Id = "NodeId", Value = 28 } };
            Assert.That(launchComplexA, Is.Not.EqualTo(launchComplexB));
            launchComplexB.Parameters = new[] { new LaunchParameterValue { Id = "NodeId", Value = 42 } };
            Assert.That(launchComplexA, Is.EqualTo(launchComplexB));

            launchComplexA.LaunchPads = new[] { new LaunchPadConfiguration() { Identifier = Guid.NewGuid() } };
            launchComplexB.LaunchPads = new[] { new LaunchPadConfiguration() { Identifier = Guid.NewGuid() } };
            Assert.That(launchComplexA, Is.Not.EqualTo(launchComplexB));
            launchComplexB.LaunchPads = new[] {
                new LaunchPadConfiguration() { Identifier = launchComplexA.LaunchPads.First().Identifier } };
            Assert.That(launchComplexA, Is.EqualTo(launchComplexB));
        }
        
        [Test]
        public void SerializeDefault()
        {
            LaunchComplexConfiguration toSerialize = new();
            var serializedParameter = JsonSerializer.Serialize(toSerialize, Json.SerializerOptions);
            var deserialized = JsonSerializer.Deserialize<LaunchComplexConfiguration>(serializedParameter,
                Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void SerializeFull()
        {
            LaunchComplexConfiguration toSerialize = new();
            toSerialize.Identifier = Guid.NewGuid();
            toSerialize.Parameters = new[] { new LaunchParameterValue { Id = "NodeId", Value = 42 } };
            toSerialize.LaunchPads = new[] { new LaunchPadConfiguration() { Identifier = Guid.NewGuid() } };
            var serializedParameter = JsonSerializer.Serialize(toSerialize, Json.SerializerOptions);
            var deserialized = JsonSerializer.Deserialize<LaunchComplexConfiguration>(serializedParameter,
                Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void DeepCloneDefault()
        {
            LaunchComplexConfiguration toClone = new();
            var cloned = toClone.DeepClone();
            Assert.That(cloned, Is.EqualTo(toClone));
        }

        [Test]
        public void DeepCloneFull()
        {
            LaunchComplexConfiguration toClone = new();
            toClone.Identifier = Guid.NewGuid();
            toClone.Parameters = new[] { new LaunchParameterValue { Id = "NodeId", Value = 42 } };
            toClone.LaunchPads = new[] { new LaunchPadConfiguration() { Identifier = Guid.NewGuid() } };
            var cloned = toClone.DeepClone();
            Assert.That(cloned, Is.EqualTo(toClone));

            // Verify that there isn't any sharing
            var serializeClone = JsonSerializer.Deserialize<LaunchComplexConfiguration>(
                JsonSerializer.Serialize(toClone, Json.SerializerOptions), Json.SerializerOptions);
            Assert.That(cloned, Is.EqualTo(serializeClone));

            toClone.Parameters.First().Value = 28;
            toClone.LaunchPads.First().Identifier = Guid.NewGuid();
            Assert.That(cloned, Is.EqualTo(serializeClone));
        }
    }
}
