using System;
using System.Text.Json;

using static Unity.ClusterDisplay.MissionControl.MissionControl.Helpers;

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

            parameterA.Value = ParseJsonToElement("42");
            parameterB.Value = ParseJsonToElement("28");
            Assert.That(parameterA, Is.Not.EqualTo(parameterB));
            parameterB.Value = ParseJsonToElement("[42]");
            Assert.That(parameterA, Is.Not.EqualTo(parameterB));
            parameterB.Value = ParseJsonToElement("{\"property\":42}");
            Assert.That(parameterA, Is.Not.EqualTo(parameterB));
            parameterB.Value = ParseJsonToElement("\t42 ");
            Assert.That(parameterA, Is.EqualTo(parameterB));
        }

        [Test]
        public void SerializeDefault()
        {
            MissionParameterValue toSerialize = new(Guid.NewGuid());
            var serializedParameter = JsonSerializer.Serialize(toSerialize, Json.SerializerOptions);
            var deserialized = JsonSerializer.Deserialize<MissionParameterValue>(serializedParameter, Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void SerializeFull()
        {
            MissionParameterValue toSerialize = new(Guid.NewGuid());
            toSerialize.ValueIdentifier = "scene.lightIntensity";
            toSerialize.Value = ParseJsonToElement("[42, 28]");
            var serializedParameter = JsonSerializer.Serialize(toSerialize, Json.SerializerOptions);
            var deserialized = JsonSerializer.Deserialize<MissionParameterValue>(serializedParameter, Json.SerializerOptions);
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
            toClone.Value = ParseJsonToElement("[42, 28]");
            var cloned = toClone.DeepClone();
            Assert.That(cloned, Is.EqualTo(toClone));
        }

        [Test]
        public void AsBoolean()
        {
            MissionParameterValue parameterValue = new(Guid.NewGuid());

            parameterValue.Value = JsonSerializer.SerializeToElement(true);
            Assert.That(parameterValue.AsBoolean(), Is.True);

            parameterValue.Value = JsonSerializer.SerializeToElement(false);
            Assert.That(parameterValue.AsBoolean(), Is.False);

            parameterValue.Value = JsonSerializer.SerializeToElement(42);
            Assert.That(() => parameterValue.AsBoolean(), Throws.Exception);

            parameterValue.Value = JsonSerializer.SerializeToElement(0);
            Assert.That(() => parameterValue.AsBoolean(), Throws.Exception);

            parameterValue.Value = JsonSerializer.SerializeToElement(1);
            Assert.That(() => parameterValue.AsBoolean(), Throws.Exception);

            parameterValue.Value = JsonSerializer.SerializeToElement(42.28);
            Assert.That(() => parameterValue.AsBoolean(), Throws.Exception);

            parameterValue.Value = JsonSerializer.SerializeToElement(new[] { 42 });
            Assert.That(() => parameterValue.AsBoolean(), Throws.Exception);

            parameterValue.Value = JsonSerializer.SerializeToElement("true");
            Assert.That(() => parameterValue.AsBoolean(), Throws.Exception);

            parameterValue.Value = JsonSerializer.SerializeToElement("false");
            Assert.That(() => parameterValue.AsBoolean(), Throws.Exception);

            parameterValue.Value = JsonSerializer.SerializeToElement(new object());
            Assert.That(() => parameterValue.AsBoolean(), Throws.Exception);

            parameterValue.Value = JsonSerializer.SerializeToElement(null, typeof(object));
            Assert.That(() => parameterValue.AsBoolean(), Throws.Exception);

            parameterValue.Value = null;
            Assert.That(() => parameterValue.AsBoolean(), Throws.Exception);
        }

        [Test]
        public void AsGuid()
        {
            MissionParameterValue parameterValue = new(Guid.NewGuid());
            var testGuid = Guid.NewGuid();
            parameterValue.Value = JsonSerializer.SerializeToElement(testGuid);
            Assert.That(parameterValue.AsGuid(), Is.EqualTo(testGuid));

            parameterValue.Value = JsonSerializer.SerializeToElement("Something");
            Assert.That(() => parameterValue.AsGuid(), Throws.Exception);

            parameterValue.Value = JsonSerializer.SerializeToElement(42);
            Assert.That(() => parameterValue.AsGuid(), Throws.Exception);

            parameterValue.Value = JsonSerializer.SerializeToElement(42.28);
            Assert.That(() => parameterValue.AsGuid(), Throws.Exception);

            parameterValue.Value = JsonSerializer.SerializeToElement(true);
            Assert.That(() => parameterValue.AsGuid(), Throws.Exception);

            parameterValue.Value = JsonSerializer.SerializeToElement(new[] { "something" });
            Assert.That(() => parameterValue.AsGuid(), Throws.Exception);

            parameterValue.Value = JsonSerializer.SerializeToElement(new object());
            Assert.That(() => parameterValue.AsGuid(), Throws.Exception);

            parameterValue.Value = JsonSerializer.SerializeToElement(null, typeof(object));
            Assert.That(() => parameterValue.AsGuid(), Throws.Exception);

            parameterValue.Value = null;
            Assert.That(() => parameterValue.AsGuid(), Throws.Exception);
        }

        [Test]
        public void AsInt32()
        {
            MissionParameterValue parameterValue = new(Guid.NewGuid());
            parameterValue.Value = JsonSerializer.SerializeToElement(42);
            Assert.That(parameterValue.AsInt32(), Is.EqualTo(42));

            parameterValue.Value = JsonSerializer.SerializeToElement(42.28);
            Assert.That(() => parameterValue.AsInt32(), Throws.Exception);

            parameterValue.Value = JsonSerializer.SerializeToElement(true);
            Assert.That(() => parameterValue.AsInt32(), Throws.Exception);

            parameterValue.Value = JsonSerializer.SerializeToElement(new[] { 42 });
            Assert.That(() => parameterValue.AsInt32(), Throws.Exception);

            parameterValue.Value = JsonSerializer.SerializeToElement("42");
            Assert.That(() => parameterValue.AsInt32(), Throws.Exception);

            parameterValue.Value = JsonSerializer.SerializeToElement(new object());
            Assert.That(() => parameterValue.AsInt32(), Throws.Exception);

            parameterValue.Value = JsonSerializer.SerializeToElement(null, typeof(object));
            Assert.That(() => parameterValue.AsInt32(), Throws.Exception);

            parameterValue.Value = null;
            Assert.That(() => parameterValue.AsInt32(), Throws.Exception);
        }

        [Test]
        public void AsSingle()
        {
            MissionParameterValue parameterValue = new(Guid.NewGuid());
            parameterValue.Value = JsonSerializer.SerializeToElement(42);
            Assert.That(parameterValue.AsSingle(), Is.EqualTo(42));

            parameterValue.Value = JsonSerializer.SerializeToElement(42.28);
            Assert.That(parameterValue.AsSingle(), Is.EqualTo(42.28f));

            parameterValue.Value = JsonSerializer.SerializeToElement(true);
            Assert.That(() => parameterValue.AsSingle(), Throws.Exception);

            parameterValue.Value = JsonSerializer.SerializeToElement(new[] { 42 });
            Assert.That(() => parameterValue.AsSingle(), Throws.Exception);

            parameterValue.Value = JsonSerializer.SerializeToElement("42.28");
            Assert.That(() => parameterValue.AsSingle(), Throws.Exception);

            parameterValue.Value = JsonSerializer.SerializeToElement(new object());
            Assert.That(() => parameterValue.AsSingle(), Throws.Exception);

            parameterValue.Value = JsonSerializer.SerializeToElement(null, typeof(object));
            Assert.That(() => parameterValue.AsSingle(), Throws.Exception);

            parameterValue.Value = null;
            Assert.That(() => parameterValue.AsSingle(), Throws.Exception);
        }

        [Test]
        public void AsString()
        {
            MissionParameterValue parameterValue = new(Guid.NewGuid());
            parameterValue.Value =JsonSerializer.SerializeToElement("Something");
            Assert.That(parameterValue.AsString(), Is.EqualTo("Something"));

            parameterValue.Value = JsonSerializer.SerializeToElement(42);
            Assert.That(() => parameterValue.AsString(), Throws.Exception);

            parameterValue.Value = JsonSerializer.SerializeToElement(42.28);
            Assert.That(() => parameterValue.AsString(), Throws.Exception);

            parameterValue.Value = JsonSerializer.SerializeToElement(true);
            Assert.That(() => parameterValue.AsString(), Throws.Exception);

            parameterValue.Value = JsonSerializer.SerializeToElement(new[] { "Something" });
            Assert.That(() => parameterValue.AsString(), Throws.Exception);

            parameterValue.Value = JsonSerializer.SerializeToElement(new object());
            Assert.That(() => parameterValue.AsString(), Throws.Exception);

            parameterValue.Value = JsonSerializer.SerializeToElement(null, typeof(object));
            Assert.That(() => parameterValue.AsString(), Throws.Exception);

            parameterValue.Value = null;
            Assert.That(() => parameterValue.AsString(), Throws.Exception);
        }

        [Test]
        public void IsNull()
        {
            MissionParameterValue parameterValue = new(Guid.NewGuid());
            Assert.That(parameterValue.IsNull, Is.True);

            parameterValue.Value = JsonSerializer.SerializeToElement(null, typeof(object));
            Assert.That(parameterValue.IsNull, Is.True);

            parameterValue.Value = JsonSerializer.SerializeToElement(42);
            Assert.That(parameterValue.IsNull, Is.False);

            parameterValue.Value = JsonSerializer.SerializeToElement(Array.Empty<object>());
            Assert.That(parameterValue.IsNull, Is.False);

            parameterValue.Value = JsonSerializer.SerializeToElement(new object());
            Assert.That(parameterValue.IsNull, Is.False);
        }
    }
}
