using System;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    public class LaunchParameterValueTests
    {
        [Test]
        public void Equal()
        {
            LaunchParameterValue launchParameterValueA = new();
            LaunchParameterValue launchParameterValueB = new();
            Assert.That(launchParameterValueA, Is.EqualTo(launchParameterValueB));

            launchParameterValueA.Id = "NodeId";
            launchParameterValueB.Id = "NOdeId";
            Assert.That(launchParameterValueA, Is.Not.EqualTo(launchParameterValueB));
            launchParameterValueB.Id = "NodeId";
            Assert.That(launchParameterValueA, Is.EqualTo(launchParameterValueB));

            launchParameterValueA.Value = 42;
            launchParameterValueB.Value = 28;
            Assert.That(launchParameterValueA, Is.Not.EqualTo(launchParameterValueB));
            launchParameterValueB.Value = "Quarante deux";
            Assert.That(launchParameterValueA, Is.Not.EqualTo(launchParameterValueB));
            launchParameterValueB.Value = 42;
            Assert.That(launchParameterValueA, Is.EqualTo(launchParameterValueB));
        }

        [Test]
        public void SerializeDefault()
        {
            LaunchParameterValue toSerialize = new();
            var serializedParameter = JsonConvert.SerializeObject(toSerialize, Json.SerializerOptions);
            var deserialized = JsonConvert.DeserializeObject<LaunchParameterValue>(serializedParameter,
                Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void SerializeFullBoolean()
        {
            LaunchParameterValue toSerialize = new();
            toSerialize.Id = "NodeId";
            toSerialize.Value = true;
            var serializedParameter = JsonConvert.SerializeObject(toSerialize, Json.SerializerOptions);
            var deserialized = JsonConvert.DeserializeObject<LaunchParameterValue>(serializedParameter,
                Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));

            toSerialize.Value = false;
            serializedParameter = JsonConvert.SerializeObject(toSerialize, Json.SerializerOptions);
            deserialized = JsonConvert.DeserializeObject<LaunchParameterValue>(serializedParameter,
                Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void SerializeFullInt()
        {
            LaunchParameterValue toSerialize = new();
            toSerialize.Id = "NodeId";
            toSerialize.Value = 42;
            var serializedParameter = JsonConvert.SerializeObject(toSerialize, Json.SerializerOptions);
            var deserialized = JsonConvert.DeserializeObject<LaunchParameterValue>(serializedParameter,
                Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void SerializeFullFloat()
        {
            LaunchParameterValue toSerialize = new();
            toSerialize.Id = "NodeId";
            toSerialize.Value = 42.28f;
            var serializedParameter = JsonConvert.SerializeObject(toSerialize, Json.SerializerOptions);
            var deserialized = JsonConvert.DeserializeObject<LaunchParameterValue>(serializedParameter,
                Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void SerializeFullString()
        {
            LaunchParameterValue toSerialize = new();
            toSerialize.Id = "NodeId";
            toSerialize.Value = "Quarante deux";
            var serializedParameter = JsonConvert.SerializeObject(toSerialize, Json.SerializerOptions);
            var deserialized = JsonConvert.DeserializeObject<LaunchParameterValue>(serializedParameter,
                Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void DeepCloneDefault()
        {
            LaunchParameterValue toClone = new();
            var cloned = toClone.DeepClone();
            Assert.That(cloned, Is.EqualTo(toClone));
        }

        [Test]
        public void DeepCloneFullBool()
        {
            LaunchParameterValue toClone = new();
            toClone.Id = "ParameterId";
            toClone.Value = true;
            var cloned = toClone.DeepClone();
            Assert.That(cloned, Is.EqualTo(toClone));
        }

        [Test]
        public void DeepCloneFullInt()
        {
            LaunchParameterValue toClone = new();
            toClone.Id = "ParameterId";
            toClone.Value = 42;
            var cloned = toClone.DeepClone();
            Assert.That(cloned, Is.EqualTo(toClone));
        }

        [Test]
        public void DeepCloneFullFloat()
        {
            LaunchParameterValue toClone = new();
            toClone.Id = "ParameterId";
            toClone.Value = 42.28f;
            var cloned = toClone.DeepClone();
            Assert.That(cloned, Is.EqualTo(toClone));
        }

        [Test]
        public void DeepCloneFullString()
        {
            LaunchParameterValue toClone = new();
            toClone.Id = "ParameterId";
            toClone.Value = "Quarante deux";
            var cloned = toClone.DeepClone();
            Assert.That(cloned, Is.EqualTo(toClone));
        }

        [Test]
        public void ValueType()
        {
            LaunchParameterValue toClone = new();
            Assert.That(() => toClone.Value = true, Throws.Nothing);
            Assert.That(() => toClone.Value = false, Throws.Nothing);
            Assert.That(() => toClone.Value = 42, Throws.Nothing);
            Assert.That(() => toClone.Value = 42.28f, Throws.Nothing);
            Assert.That(() => toClone.Value = "Quarante deux", Throws.Nothing);

            Assert.That(() => toClone.Value = Guid.NewGuid(), Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void JsonValueType()
        {
            Assert.That(() => JsonConvert.DeserializeObject<LaunchParameterValue>(
                "{\"id\":\"something\",\"value\":true}", Json.SerializerOptions), Throws.Nothing);
            Assert.That(() => JsonConvert.DeserializeObject<LaunchParameterValue>(
                "{\"id\":\"something\",\"value\":false}", Json.SerializerOptions), Throws.Nothing);
            Assert.That(() => JsonConvert.DeserializeObject<LaunchParameterValue>(
                "{\"id\":\"something\",\"value\":42}", Json.SerializerOptions), Throws.Nothing);
            Assert.That(() => JsonConvert.DeserializeObject<LaunchParameterValue>(
                "{\"id\":\"something\",\"value\":\"some value\"}", Json.SerializerOptions), Throws.Nothing);
            Assert.That(() => JsonConvert.DeserializeObject<LaunchParameterValue>(
                "{\"id\":\"something\",\"value\":{\"object\":28}}", Json.SerializerOptions), Throws.TypeOf<JsonSerializationException>());
            Assert.That(() => JsonConvert.DeserializeObject<LaunchParameterValue>(
                "{\"id\":\"something\",\"value\":[1,2,3]}", Json.SerializerOptions), Throws.TypeOf<JsonSerializationException>());
        }
    }
}
