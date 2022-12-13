using Newtonsoft.Json;
using NUnit.Framework;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    public class LaunchPadReportDynamicEntryTests
    {
        [Test]
        public void Equal()
        {
            LaunchPadReportDynamicEntry entryA = new();
            LaunchPadReportDynamicEntry entryB = new();
            Assert.That(entryA, Is.EqualTo(entryB));

            entryA.Name = "Role";
            entryB.Name = "Render NodeId";
            Assert.That(entryA, Is.Not.EqualTo(entryB));
            entryB.Name = "Role";
            Assert.That(entryA, Is.EqualTo(entryB));

            entryA.Value = 42;
            entryB.Value = "Forty two";
            Assert.That(entryA, Is.Not.EqualTo(entryB));
            entryB.Value = 42;
            Assert.That(entryA, Is.EqualTo(entryB));
        }

        [Test]
        public void SerializeDefault()
        {
            LaunchPadReportDynamicEntry toSerialize = new();
            var serializedParameter = JsonConvert.SerializeObject(toSerialize, Json.SerializerOptions);
            var deserialized = JsonConvert.DeserializeObject<LaunchPadReportDynamicEntry>(serializedParameter,
                Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void SerializeFullString()
        {
            LaunchPadReportDynamicEntry toSerialize = new();
            toSerialize.Name = "Role";
            toSerialize.Value = "Emitter";
            var serializedParameter = JsonConvert.SerializeObject(toSerialize, Json.SerializerOptions);
            var deserialized = JsonConvert.DeserializeObject<LaunchPadReportDynamicEntry>(serializedParameter,
                Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void SerializeFullInt()
        {
            LaunchPadReportDynamicEntry toSerialize = new();
            toSerialize.Name = "Render NodeId";
            toSerialize.Value = 42;
            var serializedParameter = JsonConvert.SerializeObject(toSerialize, Json.SerializerOptions);
            var deserialized = JsonConvert.DeserializeObject<LaunchPadReportDynamicEntry>(serializedParameter,
                Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void SerializeFullFloat()
        {
            LaunchPadReportDynamicEntry toSerialize = new();
            toSerialize.Name = "Something floaty...";
            toSerialize.Value = 42.28f;
            var serializedParameter = JsonConvert.SerializeObject(toSerialize, Json.SerializerOptions);
            var deserialized = JsonConvert.DeserializeObject<LaunchPadReportDynamicEntry>(serializedParameter,
                Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void SerializeFullTrue()
        {
            LaunchPadReportDynamicEntry toSerialize = new();
            toSerialize.Name = "Yes";
            toSerialize.Value = true;
            var serializedParameter = JsonConvert.SerializeObject(toSerialize, Json.SerializerOptions);
            var deserialized = JsonConvert.DeserializeObject<LaunchPadReportDynamicEntry>(serializedParameter,
                Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void SerializeFullFalse()
        {
            LaunchPadReportDynamicEntry toSerialize = new();
            toSerialize.Name = "No";
            toSerialize.Value = false;
            var serializedParameter = JsonConvert.SerializeObject(toSerialize, Json.SerializerOptions);
            var deserialized = JsonConvert.DeserializeObject<LaunchPadReportDynamicEntry>(serializedParameter,
                Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void DeepCloneDefault()
        {
            LaunchPadReportDynamicEntry toClone = new();
            var cloned = toClone.DeepClone();
            Assert.That(cloned, Is.EqualTo(toClone));
        }

        [Test]
        public void DeepCloneFull()
        {
            LaunchPadReportDynamicEntry toClone = new();
            toClone.Name = "Role";
            toClone.Value = "Emitter";
            var cloned = toClone.DeepClone();
            Assert.That(cloned, Is.EqualTo(toClone));
        }
    }
}
