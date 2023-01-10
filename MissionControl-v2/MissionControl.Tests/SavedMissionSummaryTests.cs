using System.Text.Json;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    public class SavedMissionSummaryTests
    {
        [Test]
        public void Equal()
        {
            Guid theId = Guid.NewGuid();
            SavedMissionSummary summaryA = new(theId);
            SavedMissionSummary summaryB = new(Guid.NewGuid());
            Assert.That(summaryA, Is.Not.EqualTo(summaryB));
            summaryB = new(theId);
            Assert.That(summaryA, Is.EqualTo(summaryB));

            summaryA.Description.Name = "My first mission";
            summaryB.Description.Name = "My second mission";
            Assert.That(summaryA, Is.Not.EqualTo(summaryB));
            summaryB.Description.Name = "My first mission";
            Assert.That(summaryA, Is.EqualTo(summaryB));

            summaryA.Description.Details = "This is what I've done in my first mission";
            summaryB.Description.Details = "This is what I've done in my second mission";
            Assert.That(summaryA, Is.Not.EqualTo(summaryB));
            summaryB.Description.Details = "This is what I've done in my first mission";
            Assert.That(summaryA, Is.EqualTo(summaryB));

            summaryA.SaveTime = DateTime.Now;
            summaryB.SaveTime = DateTime.Now + TimeSpan.FromHours(1);
            Assert.That(summaryA, Is.Not.EqualTo(summaryB));
            summaryB.SaveTime = summaryA.SaveTime;
            Assert.That(summaryA, Is.EqualTo(summaryB));

            summaryA.AssetId = Guid.NewGuid();
            summaryB.AssetId = Guid.NewGuid();
            Assert.That(summaryA, Is.Not.EqualTo(summaryB));
            summaryB.AssetId = summaryA.AssetId;
            Assert.That(summaryA, Is.EqualTo(summaryB));
        }

        [Test]
        public void SerializeDefault()
        {
            SavedMissionSummary toSerialize = new(Guid.NewGuid());
            var serializedParameter = JsonSerializer.Serialize(toSerialize, Json.SerializerOptions);
            var deserialized = JsonSerializer.Deserialize<SavedMissionSummary>(serializedParameter, Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void SerializeFull()
        {
            SavedMissionSummary toSerialize = new(Guid.NewGuid());
            toSerialize.Description.Name = "Mission name";
            toSerialize.Description.Details = "Mission description";
            toSerialize.SaveTime = DateTime.Now;
            toSerialize.AssetId = Guid.NewGuid();
            var serializedParameter = JsonSerializer.Serialize(toSerialize, Json.SerializerOptions);
            var deserialized = JsonSerializer.Deserialize<SavedMissionSummary>(serializedParameter, Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void DeepCloneDefault()
        {
            SavedMissionSummary toClone = new(Guid.NewGuid());
            var cloned = toClone.DeepClone();
            Assert.That(cloned, Is.EqualTo(toClone));
        }

        [Test]
        public void DeepCloneFull()
        {
            SavedMissionSummary toClone = new(Guid.NewGuid());
            toClone.Description.Name = "Mission name";
            toClone.Description.Details = "Mission description";
            toClone.SaveTime = DateTime.Now;
            toClone.AssetId = Guid.NewGuid();
            var cloned = toClone.DeepClone();
            Assert.That(cloned, Is.EqualTo(toClone));
        }
    }
}
