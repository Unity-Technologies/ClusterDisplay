using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    public class MissionParameterValueTests
    {
        [Test]
        public void Equal()
        {
            Guid theId = Guid.NewGuid();
            MissionParameterValue parameterA = new(theId);
            MissionParameterValue parameterB = new(Guid.NewGuid());
            Assert.That(parameterA, Is.Not.EqualTo(parameterB));
            parameterB = new(theId);
            Assert.That(parameterA, Is.EqualTo(parameterB));

            parameterA.ValueIdentifier = "scene.lightIntensity";
            parameterB.ValueIdentifier = "scene.lightColor";
            Assert.That(parameterA, Is.Not.EqualTo(parameterB));
            parameterB.ValueIdentifier = "scene.lightIntensity";
            Assert.That(parameterA, Is.EqualTo(parameterB));

            parameterA.Value = JToken.Parse("42");
            parameterB.Value = JToken.Parse("28");
            Assert.That(parameterA, Is.Not.EqualTo(parameterB));
            parameterB.Value = JToken.Parse("[42]");
            Assert.That(parameterA, Is.Not.EqualTo(parameterB));
            parameterB.Value = JToken.Parse("\t42 ");
            Assert.That(parameterA, Is.EqualTo(parameterB));
        }

        [Test]
        public void SerializeDefault()
        {
            MissionParameterValue toSerialize = new(Guid.NewGuid());
            var serializedParameter = JsonConvert.SerializeObject(toSerialize, Json.SerializerOptions);
            var deserialized = JsonConvert.DeserializeObject<MissionParameterValue>(serializedParameter,
                Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void SerializeFull()
        {
            MissionParameterValue toSerialize = new(Guid.NewGuid());
            toSerialize.ValueIdentifier = "scene.lightIntensity";
            toSerialize.Value = ("[42, 28JToken.Parse(]");
            var serializedParameter = JsonConvert.SerializeObject(toSerialize, Json.SerializerOptions);
            var deserialized = JsonConvert.DeserializeObject<MissionParameterValue>(serializedParameter,
                Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void DeepCloneDefault()
        {
            MissionParameterValue toClone = new(Guid.NewGuid());
            var cloned = toClone.DeepClone();
            Assert.That(cloned, Is.EqualTo(toClone));
        }

        [Test]
        public void DeepCloneFull()
        {
            MissionParameterValue toClone = new(Guid.NewGuid());
            toClone.ValueIdentifier = "scene.lightIntensity";
            toClone.Value = JToken.Parse("[42, 28]");
            var cloned = toClone.DeepClone();
            Assert.That(cloned, Is.EqualTo(toClone));
        }

        [Test]
        public void AsBoolean()
        {
            MissionParameterValue parameterValue = new(Guid.NewGuid());

            parameterValue.Value = JToken.Parse("true");
            Assert.That(parameterValue.AsBoolean(), Is.True);

            parameterValue.Value = JToken.Parse("false");
            Assert.That(parameterValue.AsBoolean(), Is.False);

            parameterValue.Value = JToken.Parse("42");
            Assert.That(() => parameterValue.AsBoolean(), Throws.Exception);

            parameterValue.Value = JToken.Parse("0");
            Assert.That(() => parameterValue.AsBoolean(), Throws.Exception);

            parameterValue.Value = JToken.Parse("1");
            Assert.That(() => parameterValue.AsBoolean(), Throws.Exception);

            parameterValue.Value = JToken.Parse("42.28");
            Assert.That(() => parameterValue.AsBoolean(), Throws.Exception);

            parameterValue.Value = JToken.Parse("[42]");
            Assert.That(() => parameterValue.AsBoolean(), Throws.Exception);

            parameterValue.Value = JToken.Parse("\"true\"");
            Assert.That(() => parameterValue.AsBoolean(), Throws.Exception);

            parameterValue.Value = JToken.Parse("\"false\"");
            Assert.That(() => parameterValue.AsBoolean(), Throws.Exception);

            parameterValue.Value = JToken.Parse("{}");
            Assert.That(() => parameterValue.AsBoolean(), Throws.Exception);

            parameterValue.Value = JToken.Parse("null");
            Assert.That(() => parameterValue.AsBoolean(), Throws.Exception);

            parameterValue.Value = null;
            Assert.That(() => parameterValue.AsBoolean(), Throws.Exception);
        }

        [Test]
        public void AsGuid()
        {
            MissionParameterValue parameterValue = new(Guid.NewGuid());
            parameterValue.Value = JToken.Parse("\"1D56AF97-854A-4827-AC3F-FC203496446B\"");
            Assert.That(parameterValue.AsGuid(), Is.EqualTo(new Guid("1D56AF97-854A-4827-AC3F-FC203496446B")));

            parameterValue.Value = JToken.Parse("\"Something\"");
            Assert.That(() => parameterValue.AsGuid(), Throws.Exception);

            parameterValue.Value = JToken.Parse("42");
            Assert.That(() => parameterValue.AsGuid(), Throws.Exception);

            parameterValue.Value = JToken.Parse("42.28");
            Assert.That(() => parameterValue.AsGuid(), Throws.Exception);

            parameterValue.Value = JToken.Parse("true");
            Assert.That(() => parameterValue.AsGuid(), Throws.Exception);

            parameterValue.Value = JToken.Parse("[\"Something\"]");
            Assert.That(() => parameterValue.AsGuid(), Throws.Exception);

            parameterValue.Value = JToken.Parse("{}");
            Assert.That(() => parameterValue.AsGuid(), Throws.Exception);

            parameterValue.Value = JToken.Parse("null");
            Assert.That(() => parameterValue.AsGuid(), Throws.Exception);

            parameterValue.Value = null;
            Assert.That(() => parameterValue.AsGuid(), Throws.Exception);
        }

        [Test]
        public void AsInt32()
        {
            MissionParameterValue parameterValue = new(Guid.NewGuid());
            parameterValue.Value = JToken.Parse("42");
            Assert.That(parameterValue.AsInt32(), Is.EqualTo(42));

            parameterValue.Value = JToken.Parse("42.28");
            Assert.That(() => parameterValue.AsInt32(), Throws.Exception);

            parameterValue.Value = JToken.Parse("true");
            Assert.That(() => parameterValue.AsInt32(), Throws.Exception);

            parameterValue.Value = JToken.Parse("[42]");
            Assert.That(() => parameterValue.AsInt32(), Throws.Exception);

            parameterValue.Value = JToken.Parse("\"42\"");
            Assert.That(() => parameterValue.AsInt32(), Throws.Exception);

            parameterValue.Value = JToken.Parse("{}");
            Assert.That(() => parameterValue.AsInt32(), Throws.Exception);

            parameterValue.Value = JToken.Parse("null");
            Assert.That(() => parameterValue.AsInt32(), Throws.Exception);

            parameterValue.Value = null;
            Assert.That(() => parameterValue.AsInt32(), Throws.Exception);
        }

        [Test]
        public void AsSingle()
        {
            MissionParameterValue parameterValue = new(Guid.NewGuid());
            parameterValue.Value = JToken.Parse("42");
            Assert.That(parameterValue.AsSingle(), Is.EqualTo(42));

            parameterValue.Value = JToken.Parse("42.28");
            Assert.That(parameterValue.AsSingle(), Is.EqualTo(42.28f));

            parameterValue.Value = JToken.Parse("true");
            Assert.That(() => parameterValue.AsSingle(), Throws.Exception);

            parameterValue.Value = JToken.Parse("[42]");
            Assert.That(() => parameterValue.AsSingle(), Throws.Exception);

            parameterValue.Value = JToken.Parse("\"42.28\"");
            Assert.That(() => parameterValue.AsSingle(), Throws.Exception);

            parameterValue.Value = JToken.Parse("{}");
            Assert.That(() => parameterValue.AsSingle(), Throws.Exception);

            parameterValue.Value = JToken.Parse("null");
            Assert.That(() => parameterValue.AsSingle(), Throws.Exception);

            parameterValue.Value = null;
            Assert.That(() => parameterValue.AsSingle(), Throws.Exception);
        }

        [Test]
        public void AsString()
        {
            MissionParameterValue parameterValue = new(Guid.NewGuid());
            parameterValue.Value = JToken.Parse("\"Something\"");
            Assert.That(parameterValue.AsString(), Is.EqualTo("Something"));

            parameterValue.Value = JToken.Parse("42");
            Assert.That(() => parameterValue.AsString(), Throws.Exception);

            parameterValue.Value = JToken.Parse("42.28");
            Assert.That(() => parameterValue.AsString(), Throws.Exception);

            parameterValue.Value = JToken.Parse("true");
            Assert.That(() => parameterValue.AsString(), Throws.Exception);

            parameterValue.Value = JToken.Parse("[\"Something\"]");
            Assert.That(() => parameterValue.AsString(), Throws.Exception);

            parameterValue.Value = JToken.Parse("{}");
            Assert.That(() => parameterValue.AsString(), Throws.Exception);

            parameterValue.Value = JToken.Parse("null");
            Assert.That(() => parameterValue.AsString(), Throws.Exception);

            parameterValue.Value = null;
            Assert.That(() => parameterValue.AsString(), Throws.Exception);
        }

        [Test]
        public void IsNull()
        {
            MissionParameterValue parameterValue = new(Guid.NewGuid());
            Assert.That(parameterValue.IsNull, Is.True);

            parameterValue.Value = JToken.Parse("null");
            Assert.That(parameterValue.IsNull, Is.True);

            parameterValue.Value = JToken.Parse("42");
            Assert.That(parameterValue.IsNull, Is.False);

            parameterValue.Value = JToken.Parse("[]");
            Assert.That(parameterValue.IsNull, Is.False);

            parameterValue.Value = JToken.Parse("{}");
            Assert.That(parameterValue.IsNull, Is.False);
        }
    }
}
