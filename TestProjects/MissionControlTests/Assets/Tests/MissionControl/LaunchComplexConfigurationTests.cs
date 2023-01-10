using System;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    public class LaunchComplexConfigurationTests
    {
        [Test]
        public void Equal()
        {
            LaunchComplexConfiguration launchComplexConfigA = new();
            LaunchComplexConfiguration launchComplexConfigB = new();
            Assert.That(launchComplexConfigA, Is.EqualTo(launchComplexConfigB));

            launchComplexConfigA.Identifier = Guid.NewGuid();
            launchComplexConfigB.Identifier = Guid.NewGuid();
            Assert.That(launchComplexConfigA, Is.Not.EqualTo(launchComplexConfigB));
            launchComplexConfigB.Identifier = launchComplexConfigA.Identifier;
            Assert.That(launchComplexConfigA, Is.EqualTo(launchComplexConfigB));

            launchComplexConfigA.LaunchPads.AddRange(new[] {
                new LaunchPadConfiguration() {Identifier = Guid.NewGuid()},
                new LaunchPadConfiguration() {Identifier = Guid.NewGuid()}
            });
            launchComplexConfigB.LaunchPads.AddRange(new[] {
                new LaunchPadConfiguration() {Identifier = Guid.NewGuid()},
                new LaunchPadConfiguration() {Identifier = Guid.NewGuid()}
            });
            Assert.That(launchComplexConfigA, Is.Not.EqualTo(launchComplexConfigB));
            launchComplexConfigB.LaunchPads.Clear();
            launchComplexConfigB.LaunchPads.AddRange(new[] {
                new LaunchPadConfiguration() {Identifier = launchComplexConfigA.LaunchPads[0].Identifier},
                new LaunchPadConfiguration() {Identifier = launchComplexConfigA.LaunchPads[1].Identifier}
            });
            Assert.That(launchComplexConfigA, Is.EqualTo(launchComplexConfigB));
        }

        [Test]
        public void RoundTrip()
        {
            LaunchComplexConfiguration toSerialize = new();
            toSerialize.Identifier = Guid.NewGuid();
            toSerialize.LaunchPads.AddRange(new[] {
                new LaunchPadConfiguration() {Identifier = Guid.NewGuid()},
                new LaunchPadConfiguration() {Identifier = Guid.NewGuid()}
            });

            var jsonString = JsonConvert.SerializeObject(toSerialize, Json.SerializerOptions);
            var deserialized = JsonConvert.DeserializeObject<LaunchComplexConfiguration>(jsonString, Json.SerializerOptions);

            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void Deserialize()
        {
            var jsonString = ("{'identifier':'0e73e6a2-6462-4c37-857f-12cec20baddd','launchPads':[" +
                    "{'identifier':'59500aca-62cc-440f-8e27-00e4297f0db1','launchableName':'QuadroSyncTests'}]}")
                .Replace('\'', '"');
            var deserialized = JsonConvert.DeserializeObject<LaunchComplexConfiguration>(jsonString, Json.SerializerOptions);

            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized.Identifier, Is.EqualTo(Guid.Parse("0e73e6a2-6462-4c37-857f-12cec20baddd")));
            Assert.That(deserialized.LaunchPads.Count, Is.EqualTo(1));
            Assert.That(deserialized.LaunchPads[0].Identifier, Is.EqualTo(Guid.Parse("59500aca-62cc-440f-8e27-00e4297f0db1")));
        }
    }
}
