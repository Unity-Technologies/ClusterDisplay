using System;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace Unity.ClusterDisplay.MissionControl.LaunchCatalog
{
    public class PayloadFileTests
    {
        [Test]
        public void Equal()
        {
            PayloadFile payloadFileA = new();
            PayloadFile payloadFileB = new();
            Assert.That(payloadFileA, Is.EqualTo(payloadFileB));

            payloadFileA.Path = "MyProject.exe";
            payloadFileB.Path = "SomeOtherFile.dll";
            Assert.That(payloadFileA, Is.Not.EqualTo(payloadFileB));
            payloadFileB.Path = "MyProject.exe";
            Assert.That(payloadFileA, Is.EqualTo(payloadFileB));

            payloadFileA.Md5 = "29DCDB00DADC4445B1B94A2394AC4C4F";
            payloadFileB.Md5 = "DF73DC86471942FCB63B53F36AF9576D";
            Assert.That(payloadFileA, Is.Not.EqualTo(payloadFileB));
            payloadFileB.Md5 = "29DCDB00DADC4445B1B94A2394AC4C4F";
            Assert.That(payloadFileA, Is.EqualTo(payloadFileB));
        }

        [Test]
        public void RoundTrip()
        {
            PayloadFile toSerialize = new();
            toSerialize.Path = "MyProject.exe";
            toSerialize.Md5 = "29DCDB00DADC4445B1B94A2394AC4C4F";

            var jsonString = JsonConvert.SerializeObject(toSerialize, Json.SerializerOptions);
            var deserialized = JsonConvert.DeserializeObject<PayloadFile>(jsonString, Json.SerializerOptions);

            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void Deserialize()
        {
            var jsonString = "{'path': 'MyProject.exe', 'md5': '29DCDB00DADC4445B1B94A2394AC4C4F'}".Replace('\'', '"');
            var deserialized = JsonConvert.DeserializeObject<PayloadFile>(jsonString, Json.SerializerOptions)!;

            Assert.That(deserialized.Path, Is.EqualTo("MyProject.exe"));
            Assert.That(deserialized.Md5, Is.EqualTo("29DCDB00DADC4445B1B94A2394AC4C4F"));
        }

        [Test]
        public void ConvertToForwardSlash()
        {
            PayloadFile payloadFile = new();
            payloadFile.Path = "folder/filename";
            Assert.That(payloadFile.Path, Is.EqualTo("folder/filename"));
            payloadFile.Path = "folder\\filename";
            Assert.That(payloadFile.Path, Is.EqualTo("folder/filename"));
        }
    }
}
