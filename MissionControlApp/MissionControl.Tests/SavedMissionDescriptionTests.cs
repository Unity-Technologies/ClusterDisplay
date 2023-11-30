using System.Text.Json;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    public class SavedMissionDescriptionTests
    {
        [Test]
        public void Equal()
        {
            SavedMissionDescription descriptionA = new();
            SavedMissionDescription descriptionB = new();

            descriptionA.Name = "My first mission";
            descriptionB.Name = "My second mission";
            Assert.That(descriptionA, Is.Not.EqualTo(descriptionB));
            descriptionB.Name = "My first mission";
            Assert.That(descriptionA, Is.EqualTo(descriptionB));

            descriptionA.Details = "This is what I've done in my first mission";
            descriptionB.Details = "This is what I've done in my second mission";
            Assert.That(descriptionA, Is.Not.EqualTo(descriptionB));
            descriptionB.Details = "This is what I've done in my first mission";
            Assert.That(descriptionA, Is.EqualTo(descriptionB));
        }

        [Test]
        public void SerializeDefault()
        {
            SavedMissionDescription toSerialize = new();
            var serializedParameter = JsonSerializer.Serialize(toSerialize, Json.SerializerOptions);
            var deserialized = JsonSerializer.Deserialize<SavedMissionDescription>(serializedParameter, Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void SerializeFull()
        {
            SavedMissionDescription toSerialize = new();
            toSerialize.Name = "Mission name";
            toSerialize.Details = "Mission details";
            var serializedParameter = JsonSerializer.Serialize(toSerialize, Json.SerializerOptions);
            var deserialized = JsonSerializer.Deserialize<SavedMissionDescription>(serializedParameter, Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void DeepCopyDefault()
        {
            SavedMissionDescription toCopyFrom = new();
            SavedMissionDescription toCopyTo = new();
            toCopyTo.Name = "Mission name";
            toCopyTo.Details = "Mission details";
            toCopyTo.DeepCopyFrom(toCopyFrom);
            Assert.That(toCopyTo, Is.EqualTo(toCopyFrom));
        }

        [Test]
        public void DeepCopyFull()
        {
            SavedMissionDescription toCopyFrom = new();
            toCopyFrom.Name = "Mission name";
            toCopyFrom.Details = "Mission details";
            SavedMissionDescription toCopyTo = new();
            toCopyTo.DeepCopyFrom(toCopyFrom);
            Assert.That(toCopyTo, Is.EqualTo(toCopyFrom));
        }
    }
}
