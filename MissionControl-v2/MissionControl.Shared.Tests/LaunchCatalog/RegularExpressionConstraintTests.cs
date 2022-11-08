using System.Text.Json;

namespace Unity.ClusterDisplay.MissionControl.LaunchCatalog
{
    public class RegularExpressionConstraintTests
    {
        [Test]
        public void Equal()
        {
            RegularExpressionConstraint constraintA = new();
            constraintA.RegularExpression = "This.*";

            RegularExpressionConstraint constraintB = new();
            Assert.That(constraintA, Is.Not.EqualTo(constraintB));

            constraintB.RegularExpression = "This.*";
            Assert.That(constraintA, Is.EqualTo(constraintB));
        }

        [Test]
        public void Serialize()
        {
            RegularExpressionConstraint toSerialize = new();
            toSerialize.RegularExpression = "This.*";
            var serializedConstraint = JsonSerializer.Serialize(toSerialize, Json.SerializerOptions);
            var deserialized = JsonSerializer.Deserialize<Constraint>(serializedConstraint, Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void CloneDefault()
        {
            RegularExpressionConstraint toClone = new();
            Constraint cloned = toClone.DeepClone();
            Assert.That(cloned, Is.Not.Null);
            Assert.That(cloned, Is.EqualTo(toClone));
        }

        [Test]
        public void CloneFull()
        {
            RegularExpressionConstraint toClone = new();
            toClone.RegularExpression = "This.*";
            Constraint cloned = toClone.DeepClone();
            Assert.That(cloned, Is.Not.Null);
            Assert.That(cloned, Is.EqualTo(toClone));
        }

        [Test]
        public void Validate()
        {
            RegularExpressionConstraint constraint = new();
            constraint.RegularExpression = "This.*";
            Assert.That(constraint.Validate("This match the expression"), Is.True);
            Assert.That(constraint.Validate("Thistle is also a word"), Is.True);
            Assert.That(constraint.Validate("but this does not match"), Is.False);

            constraint.RegularExpression = "T.st";
            Assert.That(constraint.Validate("Test"), Is.True);
            Assert.That(constraint.Validate("Test with more stuff"), Is.False);

            constraint.RegularExpression = "";
            Assert.That(constraint.Validate(""), Is.True);
            Assert.That(constraint.Validate("Something"), Is.False);
        }
    }
}
