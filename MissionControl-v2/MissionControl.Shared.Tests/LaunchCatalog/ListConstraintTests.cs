using System.Text.Json;

namespace Unity.ClusterDisplay.MissionControl.LaunchCatalog.Tests
{
    public class ListConstraintTests
    {
        [Test]
        public void Equal()
        {
            ListConstraint constraintA = new();
            constraintA.Choices = new [] {"Quarante deux", "Vingt huit"};

            ListConstraint constraintB = new();
            Assert.That(constraintA, Is.Not.EqualTo(constraintB));

            constraintB.Choices = new [] {"Quarante deux", "Vingt huit"};
            Assert.That(constraintA, Is.EqualTo(constraintB));
        }

        [Test]
        public void Serialize()
        {
            ListConstraint toSerialize = new();
            toSerialize.Choices = new [] {"Quarante deux", "Vingt huit"};
            var serializedConstraint = JsonSerializer.Serialize(toSerialize, Json.SerializerOptions);
            var deserialized = JsonSerializer.Deserialize<Constraint>(serializedConstraint, Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }
    }
}
