using System.Text.Json;
using Unity.ClusterDisplay.MissionControl.LaunchCatalog;

namespace Unity.ClusterDisplay.MissionControl
{
    public class ListConstraintTests
    {
        [Test]
        public void Equal()
        {
            ListConstraint constraintA = new();
            constraintA.Choices = new[] { "Quarante deux", "Vingt huit" };

            ListConstraint constraintB = new();
            Assert.That(constraintA, Is.Not.EqualTo(constraintB));

            constraintB.Choices = new[] { "Quarante deux", "Vingt huit" };
            Assert.That(constraintA, Is.EqualTo(constraintB));
        }

        [Test]
        public void Serialize()
        {
            ListConstraint toSerialize = new();
            toSerialize.Choices = new[] { "Quarante deux", "Vingt huit" };
            var serializedConstraint = JsonSerializer.Serialize(toSerialize, Json.SerializerOptions);
            var deserialized = JsonSerializer.Deserialize<Constraint>(serializedConstraint, Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void CloneDefault()
        {
            ListConstraint toClone = new();
            Constraint cloned = toClone.DeepClone();
            Assert.That(cloned, Is.Not.Null);
            Assert.That(cloned, Is.EqualTo(toClone));
        }

        [Test]
        public void CloneFull()
        {
            ListConstraint toClone = new();
            toClone.Choices = new[] { "Quarante deux", "Vingt huit" };
            Constraint cloned = toClone.DeepClone();
            Assert.That(cloned, Is.Not.Null);
            Assert.That(cloned, Is.EqualTo(toClone));
        }

        [Test]
        public void Validate()
        {
            ListConstraint constraint = new();
            constraint.Choices = new[] { "Quarante deux", "Vingt huit" };
            Assert.That(constraint.Validate("Quarante deux"), Is.True);
            Assert.That(constraint.Validate("quarante deux"), Is.False);
            Assert.That(constraint.Validate("Patate"), Is.False);
            Assert.That(constraint.Validate(""), Is.False);
            Assert.That(constraint.Validate("Vingt huit"), Is.True);
        }
    }
}
