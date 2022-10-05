using System.Text.Json;

namespace Unity.ClusterDisplay.MissionControl.MissionControl.Tests
{
    public class HangarBayTests
    {
        [Test]
        public void Equal()
        {
            HangarBay hangarBayA = new();
            HangarBay hangarBayB = new();
            Assert.That(hangarBayA, Is.EqualTo(hangarBayB));

            hangarBayA.Identifier = Guid.NewGuid();
            hangarBayB.Identifier = Guid.NewGuid();
            Assert.That(hangarBayA, Is.Not.EqualTo(hangarBayB));
            hangarBayB.Identifier = hangarBayA.Identifier;
            Assert.That(hangarBayA, Is.EqualTo(hangarBayB));

            hangarBayA.Endpoint = new("http://1.2.3.4:8100");
            hangarBayB.Endpoint = new("http://1.2.3.5:8100");
            Assert.That(hangarBayA, Is.Not.EqualTo(hangarBayB));
            hangarBayB.Endpoint = new("http://1.2.3.4:8100");
            Assert.That(hangarBayA, Is.EqualTo(hangarBayB));
        }

        [Test]
        public void SerializeDefault()
        {
            HangarBay toSerialize = new();
            var serializedParameter = JsonSerializer.Serialize(toSerialize, Json.SerializerOptions);
            var deserialized = JsonSerializer.Deserialize<HangarBay>(serializedParameter, Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void SerializeFull()
        {
            HangarBay toSerialize = new();
            toSerialize.Identifier = Guid.NewGuid();
            toSerialize.Endpoint = new("http://1.2.3.6:8100");
            var serializedParameter = JsonSerializer.Serialize(toSerialize, Json.SerializerOptions);
            var deserialized = JsonSerializer.Deserialize<HangarBay>(serializedParameter, Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void DeepCloneDefault()
        {
            HangarBay toClone = new();
            var cloned = toClone.DeepClone();
            Assert.That(cloned, Is.EqualTo(toClone));
        }

        [Test]
        public void DeepCloneFull()
        {
            HangarBay toClone = new();
            toClone.Identifier = Guid.NewGuid();
            toClone.Endpoint = new("http://1.2.3.6:8100");
            var cloned = toClone.DeepClone();
            Assert.That(cloned, Is.EqualTo(toClone));
        }
    }
}
