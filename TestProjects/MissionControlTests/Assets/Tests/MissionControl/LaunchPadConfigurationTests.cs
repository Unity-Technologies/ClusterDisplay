using System;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    public class LaunchPadConfigurationTests
    {
        [Test]
        public void Equal()
        {
            LaunchPadConfiguration launchPadConfigA = new();
            LaunchPadConfiguration launchPadConfigB = new();
            Assert.That(launchPadConfigA, Is.EqualTo(launchPadConfigB));

            launchPadConfigA.Identifier = Guid.NewGuid();
            launchPadConfigB.Identifier = Guid.NewGuid();
            Assert.That(launchPadConfigA, Is.Not.EqualTo(launchPadConfigB));
            launchPadConfigB.Identifier = launchPadConfigA.Identifier;
            Assert.That(launchPadConfigA, Is.EqualTo(launchPadConfigB));

            launchPadConfigA.LaunchableName = "Some name";
            launchPadConfigB.LaunchableName = "Some other name";
            Assert.That(launchPadConfigA, Is.Not.EqualTo(launchPadConfigB));
            launchPadConfigB.LaunchableName = "Some name";
            Assert.That(launchPadConfigA, Is.EqualTo(launchPadConfigB));
        }

        [Test]
        public void RoundTrip()
        {
            LaunchPadConfiguration toSerialize = new();
            toSerialize.Identifier = Guid.NewGuid();

            var jsonString = JsonConvert.SerializeObject(toSerialize, Json.SerializerOptions);
            var deserialized = JsonConvert.DeserializeObject<LaunchPadConfiguration>(jsonString, Json.SerializerOptions);

            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void Deserialize()
        {
            var jsonString = "{'identifier':'59500aca-62cc-440f-8e27-00e4297f0db1','launchableName':'QuadroSyncTests'}"
                .Replace('\'', '"');
            var deserialized = JsonConvert.DeserializeObject<LaunchPadConfiguration>(jsonString, Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);

            Assert.That(deserialized.Identifier, Is.EqualTo(Guid.Parse("59500aca-62cc-440f-8e27-00e4297f0db1")));
            Assert.That(deserialized.LaunchableName, Is.EqualTo("QuadroSyncTests"));
        }
    }
}
