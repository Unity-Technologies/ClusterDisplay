using System;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace Unity.ClusterDisplay.MissionControl.LaunchPad
{
    public class ConfigTests
    {
        [Test]
        public void Equal()
        {
            Config launchPadA = new();
            Config launchPadB = new();
            Assert.That(launchPadA, Is.EqualTo(launchPadB));

            launchPadA.Identifier = Guid.NewGuid();
            launchPadB.Identifier = Guid.NewGuid();
            Assert.That(launchPadA, Is.Not.EqualTo(launchPadB));
            launchPadB.Identifier = launchPadA.Identifier;
            Assert.That(launchPadA, Is.EqualTo(launchPadB));

            launchPadA.ClusterNetworkNic = "1.2.3.4";
            launchPadB.ClusterNetworkNic = "5.6.7.8";
            Assert.That(launchPadA, Is.Not.EqualTo(launchPadB));
            launchPadB.ClusterNetworkNic = "1.2.3.4";
            Assert.That(launchPadA, Is.EqualTo(launchPadB));
        }

        [Test]
        public void RoundTrip()
        {
            Config toSerialize = new();
            toSerialize.Identifier = Guid.NewGuid();
            toSerialize.ClusterNetworkNic = "1.2.3.4";

            var jsonString = JsonConvert.SerializeObject(toSerialize, Json.SerializerOptions);
            var deserialized = JsonConvert.DeserializeObject<Config>(jsonString, Json.SerializerOptions);

            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void Deserialize()
        {
            var jsonString = ("{'identifier': 'D4CE702A-2E2B-4B16-BBD7-9C9289250613', " +
                "'clusterNetworkNic': '9.8.7.6'}").Replace('\'', '"');
            var deserialized = JsonConvert.DeserializeObject<Config>(jsonString, Json.SerializerOptions)!;

            Assert.That(deserialized.Identifier, Is.EqualTo(Guid.Parse("D4CE702A-2E2B-4B16-BBD7-9C9289250613")));
            Assert.That(deserialized.ClusterNetworkNic, Is.EqualTo("9.8.7.6"));
        }
    }
}
