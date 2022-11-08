using System;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace Unity.ClusterDisplay.MissionControl.LaunchCatalog
{
    public class PayloadTests
    {
        [Test]
        public void Equal()
        {
            Payload payloadA = new();
            Payload payloadB = new();
            Assert.That(payloadA, Is.EqualTo(payloadB));

            payloadA.Name = "ForClusterNodes";
            payloadB.Name = "ForLiveEditor";
            Assert.That(payloadA, Is.Not.EqualTo(payloadB));
            payloadB.Name = "ForClusterNodes";
            Assert.That(payloadA, Is.EqualTo(payloadB));

            payloadA.Files.AddRange( new[] {
                new PayloadFile() { Path = "SpaceshipDemo_Data/sharedassets3.assets" },
                new PayloadFile() { Path = "SpaceshipDemo.exe" } } );
            payloadB.Files.AddRange( new[] {
                new PayloadFile() { Path = "SpaceshipDemo_Data/sharedassets3.assets" } } );
            Assert.That(payloadA, Is.Not.EqualTo(payloadB));
            payloadB.Files.Clear();
            payloadB.Files.AddRange( new[] {
                new PayloadFile() { Path = "SpaceshipDemo_Data/sharedassets3.assets" },
                new PayloadFile() { Path = "SpaceshipDemo.exe" } } );
            Assert.That(payloadA, Is.EqualTo(payloadB));
        }

        [Test]
        public void RoundTrip()
        {
            Payload toSerialize = new();
            toSerialize.Name = "ForClusterNodes";
            toSerialize.Files.AddRange( new[] {
                new PayloadFile() { Path = "SpaceshipDemo_Data/sharedassets3.assets" },
                new PayloadFile() { Path = "SpaceshipDemo.exe" } } );

            var jsonString = JsonConvert.SerializeObject(toSerialize, Json.SerializerOptions);
            var deserialized = JsonConvert.DeserializeObject<Payload>(jsonString, Json.SerializerOptions);

            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void Deserialize()
        {
            var jsonString = ("{'name': 'ForClusterNodes', 'files': [" +
                    "{'path': 'MyProject.exe', 'md5': '29DCDB00DADC4445B1B94A2394AC4C4F'}," +
                    "{'path': 'SomeOtherFile.dll', 'md5': 'DF73DC86471942FCB63B53F36AF9576D'}" +
                "]}").Replace('\'', '"');
            var deserialized = JsonConvert.DeserializeObject<Payload>(jsonString, Json.SerializerOptions)!;

            Assert.That(deserialized.Name, Is.EqualTo("ForClusterNodes"));
            Assert.That(deserialized.Files.Count, Is.EqualTo(2));
            Assert.That(deserialized.Files[0], Is.EqualTo(
                new PayloadFile(){Path = "MyProject.exe", Md5 = "29DCDB00DADC4445B1B94A2394AC4C4F"}));
            Assert.That(deserialized.Files[1], Is.EqualTo(
                new PayloadFile(){Path = "SomeOtherFile.dll", Md5 = "DF73DC86471942FCB63B53F36AF9576D"}));
        }
    }
}
