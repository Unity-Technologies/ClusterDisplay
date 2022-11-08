using System;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    public class LaunchConfigurationTests
    {
        [Test]
        public void Equal()
        {
            LaunchConfiguration launchConfigA = new();
            LaunchConfiguration launchConfigB = new();
            Assert.That(launchConfigA, Is.EqualTo(launchConfigB));

            launchConfigA.AssetId = Guid.NewGuid();
            launchConfigB.AssetId = Guid.NewGuid();
            Assert.That(launchConfigA, Is.Not.EqualTo(launchConfigB));
            launchConfigB.AssetId = launchConfigA.AssetId;
            Assert.That(launchConfigA, Is.EqualTo(launchConfigB));

            launchConfigA.LaunchComplexes.AddRange(new[] {
                new LaunchComplexConfiguration() {Identifier = Guid.NewGuid()},
                new LaunchComplexConfiguration() {Identifier = Guid.NewGuid()}
            });
            launchConfigB.LaunchComplexes.AddRange(new[] {
                new LaunchComplexConfiguration() {Identifier = Guid.NewGuid()},
                new LaunchComplexConfiguration() {Identifier = Guid.NewGuid()}
            });
            Assert.That(launchConfigA, Is.Not.EqualTo(launchConfigB));
            launchConfigB.LaunchComplexes.Clear();
            launchConfigB.LaunchComplexes.AddRange(new[] {
                new LaunchComplexConfiguration() {Identifier = launchConfigA.LaunchComplexes[0].Identifier},
                new LaunchComplexConfiguration() {Identifier = launchConfigA.LaunchComplexes[1].Identifier}
            });
            Assert.That(launchConfigA, Is.EqualTo(launchConfigB));
        }

        [Test]
        public void RoundTrip()
        {
            LaunchConfiguration toSerialize = new();
            toSerialize.LaunchComplexes.AddRange(new[] {
                new LaunchComplexConfiguration() {Identifier = Guid.NewGuid()},
                new LaunchComplexConfiguration() {Identifier = Guid.NewGuid()}
            });

            var jsonString = JsonConvert.SerializeObject(toSerialize, Json.SerializerOptions);
            var deserialized = JsonConvert.DeserializeObject<LaunchConfiguration>(jsonString, Json.SerializerOptions);

            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void Deserialize()
        {
            var jsonString = ("{'assetId':'cf2024e1-291c-43cb-9a45-76f03d959e4c','launchComplexes':[{'identifier':" +
                    "'0e73e6a2-6462-4c37-857f-12cec20baddd','launchPads':[{'identifier':'59500aca-62cc-440f-8e27-" +
                    "00e4297f0db1','launchableName':'QuadroSyncTests'}]}]}")
                .Replace('\'', '"');
            var deserialized = JsonConvert.DeserializeObject<LaunchConfiguration>(jsonString, Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);

            Assert.That(deserialized.AssetId, Is.EqualTo(Guid.Parse("cf2024e1-291c-43cb-9a45-76f03d959e4c")));
            Assert.That(deserialized.LaunchComplexes.Count, Is.EqualTo(1));
            Assert.That(deserialized.LaunchComplexes[0].Identifier,
                Is.EqualTo(Guid.Parse("0e73e6a2-6462-4c37-857f-12cec20baddd")));
            Assert.That(deserialized.LaunchComplexes[0].LaunchPads.Count, Is.EqualTo(1));
            Assert.That(deserialized.LaunchComplexes[0].LaunchPads[0].Identifier,
                Is.EqualTo(Guid.Parse("59500aca-62cc-440f-8e27-00e4297f0db1")));
        }
    }
}
