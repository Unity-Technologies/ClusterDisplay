using Newtonsoft.Json;
using NUnit.Framework;

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
            var serializedConstraint = JsonConvert.SerializeObject(toSerialize, Json.SerializerOptions);
            var deserialized = JsonConvert.DeserializeObject<Constraint>(serializedConstraint, Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }
    }
}
