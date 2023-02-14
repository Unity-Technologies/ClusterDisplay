using System.Text.Json;
using Unity.ClusterDisplay.MissionControl.LaunchCatalog;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    public class MissionParameterTests
    {
        [Test]
        public void Equal()
        {
            Guid theId = Guid.NewGuid();
            MissionParameter parameterA = new(theId);
            MissionParameter parameterB = new(Guid.NewGuid());
            Assert.That(parameterA, Is.Not.EqualTo(parameterB));
            parameterB = new(theId);
            Assert.That(parameterA, Is.EqualTo(parameterB));

            parameterA.ValueIdentifier = "scene.lightIntensity";
            parameterB.ValueIdentifier = "scene.lightColor";
            Assert.That(parameterA, Is.Not.EqualTo(parameterB));
            parameterB.ValueIdentifier = "scene.lightIntensity";
            Assert.That(parameterA, Is.EqualTo(parameterB));

            parameterA.Name = "Light intensity";
            parameterB.Name = "Light color";
            Assert.That(parameterA, Is.Not.EqualTo(parameterB));
            parameterB.Name = "Light intensity";
            Assert.That(parameterA, Is.EqualTo(parameterB));

            parameterA.Description = "Intensity of the light in lumen.";
            parameterB.Description = "Color of the light.";
            Assert.That(parameterA, Is.Not.EqualTo(parameterB));
            parameterB.Description = "Intensity of the light in lumen.";
            Assert.That(parameterA, Is.EqualTo(parameterB));

            parameterA.Type = MissionParameterType.Integer;
            parameterB.Type = MissionParameterType.String;
            Assert.That(parameterA, Is.Not.EqualTo(parameterB));
            parameterB.Type = MissionParameterType.Integer;
            Assert.That(parameterA, Is.EqualTo(parameterB));

            parameterA.Constraint = new ListConstraint();
            Assert.That(parameterA, Is.Not.EqualTo(parameterB));
            parameterA.Constraint = null;
            parameterB.Constraint = new ListConstraint();
            Assert.That(parameterA, Is.Not.EqualTo(parameterB));
            parameterA.Constraint = new RegularExpressionConstraint();
            Assert.That(parameterA, Is.Not.EqualTo(parameterB));
            parameterB.Constraint = new RegularExpressionConstraint();
            Assert.That(parameterA, Is.EqualTo(parameterB));

            parameterA.Group = "Illumination";
            parameterB.Group = "Light";
            Assert.That(parameterA, Is.Not.EqualTo(parameterB));
            parameterB.Group = "Illumination";
            Assert.That(parameterA, Is.EqualTo(parameterB));
        }

        [Test]
        public void SerializeDefault()
        {
            MissionParameter toSerialize = new(Guid.NewGuid());
            var serializedParameter = JsonSerializer.Serialize(toSerialize, Json.SerializerOptions);
            var deserialized = JsonSerializer.Deserialize<MissionParameter>(serializedParameter, Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void SerializeFull()
        {
            MissionParameter toSerialize = new(Guid.NewGuid());
            toSerialize.ValueIdentifier = "scene.lightIntensity";
            toSerialize.Name = "Light intensity";
            toSerialize.Description = "Intensity of the light in lumen.";
            toSerialize.Type = MissionParameterType.Integer;
            toSerialize.Constraint = new ListConstraint();
            toSerialize.Group = "Illumination";
            var serializedParameter = JsonSerializer.Serialize(toSerialize, Json.SerializerOptions);
            var deserialized = JsonSerializer.Deserialize<MissionParameter>(serializedParameter, Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void DeepCloneDefault()
        {
            MissionParameter toClone = new(Guid.NewGuid());
            var cloned = toClone.DeepClone();
            Assert.That(cloned, Is.EqualTo(toClone));
        }

        [Test]
        public void DeepCloneFull()
        {
            MissionParameter toClone = new(Guid.NewGuid());
            toClone.ValueIdentifier = "scene.lightIntensity";
            toClone.Name = "Light intensity";
            toClone.Description = "Intensity of the light in lumen.";
            toClone.Type = MissionParameterType.Integer;
            toClone.Constraint = new ListConstraint();
            toClone.Group = "Illumination";
            var cloned = toClone.DeepClone();
            Assert.That(cloned, Is.EqualTo(toClone));

            // Verify that there isn't any sharing
            var serializeClone = JsonSerializer.Deserialize<MissionParameter>(
                JsonSerializer.Serialize(toClone, Json.SerializerOptions), Json.SerializerOptions);
            Assert.That(serializeClone, Is.EqualTo(cloned));

            ((ListConstraint)toClone.Constraint).Choices = new[] { "Another choice" };
            Assert.That(serializeClone, Is.EqualTo(cloned));
        }
    }
}
