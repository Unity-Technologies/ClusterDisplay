using System;
using System.Text.Json;

namespace Unity.ClusterDisplay.MissionControl
{
    public class ConfirmationConstraintTests
    {
        [Test]
        public void Equal()
        {
            ConfirmationConstraint constraintA = new();
            ConfirmationConstraint constraintB = new();
            Assert.That(constraintA, Is.EqualTo(constraintB));

            constraintA.ConfirmationType = ConfirmationType.Informative;
            constraintB.ConfirmationType = ConfirmationType.Danger;
            Assert.That(constraintA, Is.Not.EqualTo(constraintB));
            constraintB.ConfirmationType = ConfirmationType.Informative;
            Assert.That(constraintA, Is.EqualTo(constraintB));

            constraintA.Title = "Title A";
            constraintB.Title = "Title B";
            Assert.That(constraintA, Is.Not.EqualTo(constraintB));
            constraintB.Title = "Title A";
            Assert.That(constraintA, Is.EqualTo(constraintB));

            constraintA.FullText = "Full text A";
            constraintB.FullText = "Full text B";
            Assert.That(constraintA, Is.Not.EqualTo(constraintB));
            constraintB.FullText = "Full text A";
            Assert.That(constraintA, Is.EqualTo(constraintB));

            constraintA.ConfirmText = "Ok";
            constraintB.ConfirmText = "Okay";
            Assert.That(constraintA, Is.Not.EqualTo(constraintB));
            constraintB.ConfirmText = "Ok";
            Assert.That(constraintA, Is.EqualTo(constraintB));

            constraintA.AbortText = "No";
            constraintB.AbortText = "No way!";
            Assert.That(constraintA, Is.Not.EqualTo(constraintB));
            constraintB.AbortText = "No";
            Assert.That(constraintA, Is.EqualTo(constraintB));
        }

        [Test]
        public void Serialize()
        {
            ConfirmationConstraint toSerialize = new();
            toSerialize.ConfirmationType = ConfirmationType.Danger;
            toSerialize.Title = "Title A";
            toSerialize.FullText = "Full text A";
            toSerialize.ConfirmText = "Ok";
            toSerialize.AbortText = "No";
            var serializedConstraint = JsonSerializer.Serialize(toSerialize, Json.SerializerOptions);
            var deserialized = JsonSerializer.Deserialize<Constraint>(serializedConstraint, Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void CloneDefault()
        {
            ConfirmationConstraint toClone = new();
            Constraint cloned = toClone.DeepClone();
            Assert.That(cloned, Is.Not.Null);
            Assert.That(cloned, Is.EqualTo(toClone));
        }

        [Test]
        public void CloneFull()
        {
            ConfirmationConstraint toClone = new();
            toClone.ConfirmationType = ConfirmationType.Danger;
            toClone.Title = "Title A";
            toClone.FullText = "Full text A";
            toClone.ConfirmText = "Ok";
            toClone.AbortText = "No";
            Constraint cloned = toClone.DeepClone();
            Assert.That(cloned, Is.Not.Null);
            Assert.That(cloned, Is.EqualTo(toClone));
        }

        [Test]
        public void Validate()
        {
            ConfirmationConstraint constraint = new();
            Assert.That(constraint.Validate("Some text"), Is.True);
            Assert.That(constraint.Validate(42), Is.True);
            Assert.That(constraint.Validate(null!), Is.True);
        }
    }
}
