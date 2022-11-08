using System;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    public class LaunchComplexTests
    {
        [Test]
        public void Equal()
        {
            Guid theId = Guid.NewGuid();
            LaunchComplex launchComplexA = new(theId);
            LaunchComplex launchComplexB = new(Guid.NewGuid());
            Assert.That(launchComplexA, Is.Not.EqualTo(launchComplexB));
            launchComplexB = new(theId);
            Assert.That(launchComplexA, Is.EqualTo(launchComplexB));

            launchComplexA.LaunchPads.AddRange(new[] {
                new LaunchPad() {Identifier = Guid.NewGuid()},
                new LaunchPad() {Identifier = Guid.NewGuid()}
            });
            launchComplexB.LaunchPads.AddRange(new[] {
                new LaunchPad() {Identifier = Guid.NewGuid()},
                new LaunchPad() {Identifier = Guid.NewGuid()},
                new LaunchPad() {Identifier = Guid.NewGuid()}
            });
            Assert.That(launchComplexA, Is.Not.EqualTo(launchComplexB));
            launchComplexB.LaunchPads.Clear();
            launchComplexB.LaunchPads.AddRange(new[] {
                new LaunchPad() {Identifier = launchComplexA.LaunchPads[0].Identifier},
                new LaunchPad() {Identifier = launchComplexA.LaunchPads[1].Identifier}
            });
            Assert.That(launchComplexA, Is.EqualTo(launchComplexB));
        }

        [Test]
        public void RoundTrip()
        {
            LaunchComplex toSerialize = new(Guid.NewGuid());
            toSerialize.LaunchPads.AddRange(new[] {
                new LaunchPad() {Identifier = Guid.NewGuid()},
                new LaunchPad() {Identifier = Guid.NewGuid()}
            });

            var jsonString = JsonConvert.SerializeObject(toSerialize, Json.SerializerOptions);
            var deserialized = JsonConvert.DeserializeObject<LaunchComplex>(jsonString, Json.SerializerOptions);

            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void Deserialize()
        {
            var jsonString = ("{'name':'My Launch Complex','launchPads':[{'identifier':'59500aca-62cc-440f-8e27-" +
                "00e4297f0db1','name':'My LaunchPad','endpoint':'http://127.0.0.1:8200/','suitableFor':" +
                "['clusterNode']}],'hangarBay':{'identifier':'0e73e6a2-6462-4c37-857f-12cec20baddd','endpoint':" +
                "'http://127.0.0.1:8100/'},'id':'0e73e6a2-6462-4c37-857f-12cec20baddd'}").Replace('\'', '"');
            var deserialized = JsonConvert.DeserializeObject<LaunchComplex>(jsonString, Json.SerializerOptions);

            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized.Id, Is.EqualTo(Guid.Parse("0e73e6a2-6462-4c37-857f-12cec20baddd")));
            Assert.That(deserialized.LaunchPads[0].Identifier,
                Is.EqualTo(Guid.Parse("59500aca-62cc-440f-8e27-00e4297f0db1")));
            Assert.That(deserialized.LaunchPads[0].SuitableFor.Count, Is.EqualTo(1));
            Assert.That(deserialized.LaunchPads[0].SuitableFor[0], Is.EqualTo("clusterNode"));
        }

        [Test]
        public void DeepCloneDefault()
        {
            LaunchComplex toClone = new(Guid.NewGuid());
            var cloned = toClone.DeepClone();
            Assert.That(cloned, Is.EqualTo(toClone));
        }

        [Test]
        public void DeepCloneFull()
        {
            LaunchComplex toClone = new(Guid.NewGuid());
            toClone.LaunchPads = new() {
                new LaunchPad() { Identifier = Guid.NewGuid() },
                new LaunchPad() { Identifier = Guid.NewGuid() }
            };
            var cloned = toClone.DeepClone();
            Assert.That(cloned, Is.EqualTo(toClone));

            // Verify that there isn't any sharing
            var serializeClone = JsonConvert.DeserializeObject<LaunchComplex>(
                JsonConvert.SerializeObject(toClone, Json.SerializerOptions), Json.SerializerOptions);
            Assert.That(serializeClone, Is.EqualTo(cloned));

            toClone.LaunchPads[0].Identifier = Guid.NewGuid();
            Assert.That(serializeClone, Is.EqualTo(cloned));
            toClone.LaunchPads[1].Identifier = Guid.NewGuid();
            Assert.That(serializeClone, Is.EqualTo(cloned));
        }
    }
}
