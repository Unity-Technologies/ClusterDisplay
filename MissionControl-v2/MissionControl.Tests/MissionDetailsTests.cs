using System;
using System.Text.Json;
using Unity.ClusterDisplay.MissionControl.MissionControl.Library;

namespace Unity.ClusterDisplay.MissionControl.MissionControl.Tests
{
    public class MissionDetailsTests
    {
        [Test]
        public void Equal()
        {
            MissionDetails detailsA = new();
            MissionDetails detailsB = new();

            detailsA.Identifier = Guid.NewGuid();
            detailsB.Identifier = Guid.NewGuid();
            Assert.That(detailsA, Is.Not.EqualTo(detailsB));
            detailsB.Identifier = detailsA.Identifier;
            Assert.That(detailsA, Is.EqualTo(detailsB));

            detailsA.Description.Name = "My first mission";
            detailsB.Description.Name = "My second mission";
            Assert.That(detailsA, Is.Not.EqualTo(detailsB));
            detailsB.Description.Name = "My first mission";
            Assert.That(detailsA, Is.EqualTo(detailsB));

            detailsA.Description.Details = "This is what I've done in my first mission";
            detailsB.Description.Details = "This is what I've done in my second mission";
            Assert.That(detailsA, Is.Not.EqualTo(detailsB));
            detailsB.Description.Details = "This is what I've done in my first mission";
            Assert.That(detailsA, Is.EqualTo(detailsB));

            detailsA.LaunchConfiguration = new LaunchConfiguration() { AssetId = Guid.NewGuid() };
            detailsB.LaunchConfiguration = new LaunchConfiguration() { AssetId = Guid.NewGuid() };
            Assert.That(detailsA, Is.Not.EqualTo(detailsB));
            detailsB.LaunchConfiguration = detailsA.LaunchConfiguration.DeepClone();
            Assert.That(detailsA, Is.EqualTo(detailsB));
        }

        [Test]
        public void SerializeDefault()
        {
            MissionDetails toSerialize = new();
            var serializedParameter = JsonSerializer.Serialize(toSerialize, Json.SerializerOptions);
            var deserialized = JsonSerializer.Deserialize<MissionDetails>(serializedParameter, Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void SerializeFull()
        {
            MissionDetails toSerialize = new();
            toSerialize.Identifier = Guid.NewGuid();
            toSerialize.Description.Name = "Mission name";
            toSerialize.Description.Details = "Mission description";
            toSerialize.LaunchConfiguration = new LaunchConfiguration() { AssetId = Guid.NewGuid() };
            var serializedParameter = JsonSerializer.Serialize(toSerialize, Json.SerializerOptions);
            var deserialized = JsonSerializer.Deserialize<MissionDetails>(serializedParameter, Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void DeepCloneDefault()
        {
            MissionDetails toClone = new();
            var cloned = toClone.DeepClone();
            Assert.That(cloned, Is.EqualTo(toClone));
        }

        [Test]
        public void DeepCloneFull()
        {
            MissionDetails toClone = new();
            toClone.Identifier = Guid.NewGuid();
            toClone.Description.Name = "Mission name";
            toClone.Description.Details = "Mission description";
            toClone.LaunchConfiguration = new LaunchConfiguration() { AssetId = Guid.NewGuid() };
            var cloned = toClone.DeepClone();
            Assert.That(cloned, Is.EqualTo(toClone));
        }
    }
}
