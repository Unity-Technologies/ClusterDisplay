using System;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    public class LaunchPadTests
    {
        [Test]
        public void Equal()
        {
            LaunchPad launchPadA = new();
            LaunchPad launchPadB = new();
            Assert.That(launchPadA, Is.EqualTo(launchPadB));

            launchPadA.Identifier = Guid.NewGuid();
            launchPadB.Identifier = Guid.NewGuid();
            Assert.That(launchPadA, Is.Not.EqualTo(launchPadB));
            launchPadB.Identifier = launchPadA.Identifier;
            Assert.That(launchPadA, Is.EqualTo(launchPadB));

            launchPadA.Name = "Top Left";
            launchPadB.Name = "Bottom Right";
            Assert.That(launchPadA, Is.Not.EqualTo(launchPadB));
            launchPadB.Name = "Top Left";
            Assert.That(launchPadA, Is.EqualTo(launchPadB));

            launchPadA.Endpoint = new("http://1.2.3.4:8200");
            launchPadB.Endpoint = new("http://1.2.3.5:8200");
            Assert.That(launchPadA, Is.Not.EqualTo(launchPadB));
            launchPadB.Endpoint = new("http://1.2.3.4:8200");
            Assert.That(launchPadA, Is.EqualTo(launchPadB));

            launchPadA.SuitableFor.AddRange(new[]{"clusterNode", "liveEditor"});
            launchPadB.SuitableFor.AddRange(new[]{"plowingSnow", "applyingSunScreen"});
            Assert.That(launchPadA, Is.Not.EqualTo(launchPadB));
            launchPadB.SuitableFor.Clear();
            launchPadB.SuitableFor.AddRange(new[]{"clusterNode", "liveEditor"});
            Assert.That(launchPadA, Is.EqualTo(launchPadB));
        }

        [Test]
        public void RoundTrip()
        {
            LaunchPad toSerialize = new();
            toSerialize.Identifier = Guid.NewGuid();
            toSerialize.Name = "Top Left";
            toSerialize.Endpoint = new("http://1.2.3.6:8200");
            toSerialize.SuitableFor.AddRange(new[]{"clusterNode", "liveEditor"});

            var jsonString = JsonConvert.SerializeObject(toSerialize, Json.SerializerOptions);
            var deserialized = JsonConvert.DeserializeObject<LaunchPad>(jsonString, Json.SerializerOptions);

            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void Deserialize()
        {
            var jsonString = ("{'identifier':'59500aca-62cc-440f-8e27-00e4297f0db1','name':'My LaunchPad','endpoint':" +
                    "'http://127.0.0.1:8200/','suitableFor':['clusterNode']}").Replace('\'', '"');
            var deserialized = JsonConvert.DeserializeObject<LaunchPad>(jsonString, Json.SerializerOptions);

            Assert.That(deserialized!.Identifier, Is.EqualTo(Guid.Parse("59500aca-62cc-440f-8e27-00e4297f0db1")));
            Assert.That(deserialized.Name, Is.EqualTo("My LaunchPad"));
            Assert.That(deserialized.Endpoint, Is.EqualTo(new Uri("http://127.0.0.1:8200/")));
            Assert.That(deserialized.SuitableFor.Count, Is.EqualTo(1));
            Assert.That(deserialized.SuitableFor[0], Is.EqualTo("clusterNode"));
        }

        [Test]
        public void DeepCloneDefault()
        {
            LaunchPad toClone = new();
            var cloned = toClone.DeepClone();
            Assert.That(cloned, Is.EqualTo(toClone));
        }

        [Test]
        public void DeepCloneFull()
        {
            LaunchPad toClone = new();
            toClone.Identifier = Guid.NewGuid();
            toClone.Name = "My Launchpad!!!";
            toClone.Endpoint = new("http://1.2.3.6:8200");
            toClone.SuitableFor = new() { "clusterNode" };
            var cloned = toClone.DeepClone();
            Assert.That(cloned, Is.EqualTo(toClone));
        }
    }
}
