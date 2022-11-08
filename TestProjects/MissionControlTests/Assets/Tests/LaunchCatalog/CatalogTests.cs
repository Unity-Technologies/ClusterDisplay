using System;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace Unity.ClusterDisplay.MissionControl.LaunchCatalog
{
    public class CatalogTests
    {
        [Test]
        public void Equal()
        {
            Catalog catalogA = new();
            Catalog catalogB = new();
            Assert.That(catalogA, Is.EqualTo(catalogB));

            catalogA.Payloads.AddRange( new[] {
                new Payload() {Name = "Payload1"},
                new Payload() {Name = "Payload2"} });
            catalogB.Payloads.AddRange( new[] {
                new Payload() {Name = "PayloadA"},
                new Payload() {Name = "PayloadB"} });
            Assert.That(catalogA, Is.Not.EqualTo(catalogB));
            catalogB.Payloads.Clear();
            catalogB.Payloads.AddRange( new[] {
                new Payload() {Name = "Payload1"},
                new Payload() {Name = "Payload2"} });
            Assert.That(catalogA, Is.EqualTo(catalogB));

            catalogA.Launchables.AddRange( new[] {
                new Launchable() {Name = "Launchable1"},
                new Launchable() {Name = "Launchable2"} });
            catalogB.Launchables.AddRange( new[] {
                new Launchable() {Name = "LaunchableA"},
                new Launchable() {Name = "LaunchableB"} });
            Assert.That(catalogA, Is.Not.EqualTo(catalogB));
            catalogB.Launchables.Clear();
            catalogB.Launchables.AddRange( new[] {
                new Launchable() {Name = "Launchable1"},
                new Launchable() {Name = "Launchable2"} });
            Assert.That(catalogA, Is.EqualTo(catalogB));
        }

        [Test]
        public void RoundTrip()
        {
            Catalog toSerialize = new();
            toSerialize.Payloads.AddRange( new[] {
                new Payload() {Name = "Payload1"},
                new Payload() {Name = "Payload2"} });
            toSerialize.Launchables.AddRange( new[] {
                new Launchable() {Name = "Launchable1"},
                new Launchable() {Name = "Launchable2"} });

            var jsonString = JsonConvert.SerializeObject(toSerialize, Json.SerializerOptions);
            var deserialized = JsonConvert.DeserializeObject<Catalog>(jsonString, Json.SerializerOptions);

            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void Deserialize()
        {
            var jsonString = ("{'payloads': [{'name': 'Payload1'}, {'name': 'Payload2'}]," +
                "'launchables': [{'name': 'Launchable1'}, {'name': 'Launchable2'}]" +
                "}").Replace('\'', '"');
            var deserialized = JsonConvert.DeserializeObject<Catalog>(jsonString, Json.SerializerOptions)!;

            Assert.That(deserialized.Payloads.Count, Is.EqualTo(2));
            Assert.That(deserialized.Payloads[0].Name, Is.EqualTo("Payload1"));
            Assert.That(deserialized.Payloads[1].Name, Is.EqualTo("Payload2"));
            Assert.That(deserialized.Launchables.Count, Is.EqualTo(2));
            Assert.That(deserialized.Launchables[0].Name, Is.EqualTo("Launchable1"));
            Assert.That(deserialized.Launchables[1].Name, Is.EqualTo("Launchable2"));
        }
    }
}
