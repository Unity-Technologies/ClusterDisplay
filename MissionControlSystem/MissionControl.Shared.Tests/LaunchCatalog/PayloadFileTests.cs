using System;
using System.Text.Json;

namespace Unity.ClusterDisplay.MissionControl.LaunchCatalog
{
    public class PayloadFileTests
    {
        [Test]
        public void Equal()
        {
            PayloadFile fileA = new();
            PayloadFile fileB = new();
            Assert.That(fileA, Is.EqualTo(fileB));

            fileA.Path = "SpaceshipDemo_Data/sharedassets3.assets";
            fileB.Path = "SpaceshipDemo.exe";
            Assert.That(fileA, Is.Not.EqualTo(fileB));
            fileA.Path = "SpaceshipDemo.exe";
            Assert.That(fileA, Is.EqualTo(fileB));

            fileA.Md5 = "bc527343c7ffc103111f3a694b004e2f";
            fileB.Md5 = "f2e400b496a3f111301cff7c343725cb";
            Assert.That(fileA, Is.Not.EqualTo(fileB));
            fileB.Md5 = "bc527343c7ffc103111f3a694b004e2f";
            Assert.That(fileA, Is.EqualTo(fileB));
        }

        [Test]
        public void SerializeDefault()
        {
            PayloadFile toSerialize = new();
            var serializedParameter = JsonSerializer.Serialize(toSerialize, Json.SerializerOptions);
            var deserialized = JsonSerializer.Deserialize<PayloadFile>(serializedParameter, Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void SerializeFull()
        {
            PayloadFile toSerialize = new();
            toSerialize.Path = "SpaceshipDemo_Data/sharedassets3.assets";
            toSerialize.Md5 = "bc527343c7ffc103111f3a694b004e2f";
            var serializedParameter = JsonSerializer.Serialize(toSerialize, Json.SerializerOptions);
            var deserialized = JsonSerializer.Deserialize<PayloadFile>(serializedParameter, Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
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
