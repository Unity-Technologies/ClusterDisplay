using System.Text.Json;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    public class LaunchConfigurationTests
    {
        [Test]
        public void Equal()
        {
            LaunchConfiguration configurationA = new();
            LaunchConfiguration configurationB = new();
            Assert.That(configurationA, Is.EqualTo(configurationB));

            configurationA.AssetId = Guid.NewGuid();
            configurationB.AssetId = Guid.NewGuid();
            Assert.That(configurationA, Is.Not.EqualTo(configurationB));
            configurationB.AssetId = configurationA.AssetId;
            Assert.That(configurationA, Is.EqualTo(configurationB));

            configurationA.Parameters = new[] { new LaunchParameterValue { Id = "NodeId", Value = 42 } };
            configurationB.Parameters = new[] { new LaunchParameterValue { Id = "NodeId", Value = 28 } };
            Assert.That(configurationA, Is.Not.EqualTo(configurationB));
            configurationB.Parameters = new[] { new LaunchParameterValue { Id = "NodeId", Value = 42 } };
            Assert.That(configurationA, Is.EqualTo(configurationB));

            configurationA.LaunchComplexes = new[] { new LaunchComplexConfiguration() { Identifier = Guid.NewGuid() } };
            configurationB.LaunchComplexes = new[] { new LaunchComplexConfiguration() { Identifier = Guid.NewGuid() } };
            Assert.That(configurationA, Is.Not.EqualTo(configurationB));
            configurationB.LaunchComplexes = new[] {
                new LaunchComplexConfiguration() { Identifier = configurationA.LaunchComplexes.First().Identifier } };
            Assert.That(configurationA, Is.EqualTo(configurationB));
        }
        
        [Test]
        public void SerializeDefault()
        {
            LaunchConfiguration toSerialize = new();
            var serializedParameter = JsonSerializer.Serialize(toSerialize, Json.SerializerOptions);
            var deserialized = JsonSerializer.Deserialize<LaunchConfiguration>(serializedParameter,
                Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void SerializeFull()
        {
            LaunchConfiguration toSerialize = new();
            toSerialize.AssetId = Guid.NewGuid();
            toSerialize.Parameters = new[] { new LaunchParameterValue { Id = "NodeId", Value = 42 } };
            toSerialize.LaunchComplexes = new[] { new LaunchComplexConfiguration() { Identifier = Guid.NewGuid() } };
            var serializedParameter = JsonSerializer.Serialize(toSerialize, Json.SerializerOptions);
            var deserialized = JsonSerializer.Deserialize<LaunchConfiguration>(serializedParameter,
                Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void DeepCloneDefault()
        {
            LaunchConfiguration toClone = new();
            var cloned = toClone.DeepClone();
            Assert.That(cloned, Is.EqualTo(toClone));
        }

        [Test]
        public void DeepCloneFull()
        {
            LaunchConfiguration toClone = new();
            toClone.AssetId = Guid.NewGuid();
            toClone.Parameters = new[] { new LaunchParameterValue { Id = "NodeId", Value = 42 } };
            toClone.LaunchComplexes = new[] { new LaunchComplexConfiguration() { Identifier = Guid.NewGuid() } };
            var cloned = toClone.DeepClone();
            Assert.That(cloned, Is.EqualTo(toClone));

            // Verify that there isn't any sharing
            var serializeClone = JsonSerializer.Deserialize<LaunchConfiguration>(
                JsonSerializer.Serialize(toClone, Json.SerializerOptions), Json.SerializerOptions);
            Assert.That(cloned, Is.EqualTo(serializeClone));

            toClone.Parameters.First().Value = 28;
            toClone.LaunchComplexes.First().Identifier = Guid.NewGuid();
            Assert.That(cloned, Is.EqualTo(serializeClone));
        }
    }
}
