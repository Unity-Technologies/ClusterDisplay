using System;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    public class LaunchableTests
    {
        [Test]
        public void Equal()
        {
            Launchable launchableA = new();
            Launchable launchableB = new();
            Assert.That(launchableA, Is.EqualTo(launchableB));

            launchableA.Name = "Some name";
            launchableB.Name = "Some other name";
            Assert.That(launchableA, Is.Not.EqualTo(launchableB));
            launchableB.Name = "Some name";
            Assert.That(launchableA, Is.EqualTo(launchableB));

            launchableA.Type = LaunchCatalog.Launchable.ClusterNodeType;
            launchableB.Type = LaunchCatalog.Launchable.CapcomType;
            Assert.That(launchableA, Is.Not.EqualTo(launchableB));
            launchableB.Type = LaunchCatalog.Launchable.ClusterNodeType;
            Assert.That(launchableA, Is.EqualTo(launchableB));
        }

        [Test]
        public void SerializeDefault()
        {
            Launchable toSerialize = new();
            var serializedParameter = JsonConvert.SerializeObject(toSerialize, Json.SerializerOptions);
            var deserialized = JsonConvert.DeserializeObject<Launchable>(serializedParameter,
                Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void SerializeFull()
        {
            Launchable toSerialize = new();
            toSerialize.Name = "Something";
            toSerialize.Type = LaunchCatalog.Launchable.ClusterNodeType;
            var serializedParameter = JsonConvert.SerializeObject(toSerialize, Json.SerializerOptions);
            var deserialized = JsonConvert.DeserializeObject<Launchable>(serializedParameter,
                Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void DeepCloneDefault()
        {
            Launchable toClone = new();
            var cloned = toClone.DeepClone();
            Assert.That(cloned, Is.EqualTo(toClone));
        }

        [Test]
        public void DeepCloneFull()
        {
            Launchable toClone = new();
            toClone.Name = "Something";
            toClone.Type = LaunchCatalog.Launchable.ClusterNodeType;
            var cloned = toClone.DeepClone();
            Assert.That(cloned, Is.EqualTo(toClone));
        }
    }
}
