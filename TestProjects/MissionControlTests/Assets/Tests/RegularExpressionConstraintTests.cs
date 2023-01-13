using Newtonsoft.Json;
using NUnit.Framework;

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
            toSerialize.ErrorMessage ="This is an error message";
            var serializedConstraint = JsonConvert.SerializeObject(toSerialize, Json.SerializerOptions);
            var deserialized = JsonConvert.DeserializeObject<Constraint>(serializedConstraint, Json.SerializerOptions);
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
            toClone.ErrorMessage ="This is an error message";
            Constraint cloned = toClone.DeepClone();
            Assert.That(cloned, Is.Not.Null);
            Assert.That(cloned, Is.EqualTo(toClone));
        }
    }
}
