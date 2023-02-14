using Newtonsoft.Json;
using NUnit.Framework;

namespace Unity.ClusterDisplay.MissionControl
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
            var serializedConstraint = JsonConvert.SerializeObject(toSerialize, Json.SerializerOptions);
            var deserialized = JsonConvert.DeserializeObject<Constraint>(serializedConstraint, Json.SerializerOptions);
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
            toClone.Choices = new [] {"Quarante deux", "Vingt huit"};
            Constraint cloned = toClone.DeepClone();
            Assert.That(cloned, Is.Not.Null);
            Assert.That(cloned, Is.EqualTo(toClone));
        }
    }
}
