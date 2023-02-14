using System.Text.Json;

namespace Unity.ClusterDisplay.MissionControl
{
    public class RegularExpressionConstraintTests
    {
        [Test]
        public void Equal()
        {
            RegularExpressionConstraint constraintA = new();
            RegularExpressionConstraint constraintB = new();
            Assert.That(constraintA, Is.EqualTo(constraintB));

            constraintA.RegularExpression = "This.*";
            constraintB.RegularExpression = "That.*";
            Assert.That(constraintA, Is.Not.EqualTo(constraintB));
            constraintB.RegularExpression = "This.*";
            Assert.That(constraintA, Is.EqualTo(constraintB));

            constraintA.ErrorMessage = "This is an error message";
            constraintB.ErrorMessage = "That is not the expected value";
            Assert.That(constraintA, Is.Not.EqualTo(constraintB));
            constraintB.ErrorMessage = "This is an error message";
            Assert.That(constraintA, Is.EqualTo(constraintB));
        }

        [Test]
        public void Serialize()
        {
            RegularExpressionConstraint toSerialize = new();
            toSerialize.RegularExpression = "This.*";
            toSerialize.ErrorMessage = "This is an error message";
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
            toClone.ErrorMessage = "This is an error message";
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

            constraint.RegularExpression = "T.st$";
            Assert.That(constraint.Validate("Test"), Is.True);
            Assert.That(constraint.Validate("Test with more stuff"), Is.False);

            constraint.RegularExpression = "^$";
            Assert.That(constraint.Validate(""), Is.True);
            Assert.That(constraint.Validate("Something"), Is.False);

            constraint.RegularExpression = "^((25[0-5]|(2[0-4]|1\\d|[1-9]|)\\d)\\.?\\b){4}$";
            Assert.That(constraint.Validate("123.123.123.123"), Is.True);
            Assert.That(constraint.Validate("123.123.123.1234"), Is.False);
            Assert.That(constraint.Validate("123.123.123."), Is.False);
            Assert.That(constraint.Validate(""), Is.False);
        }
    }
}
