using System.Text.Json;

namespace Unity.ClusterDisplay.MissionControl.MissionControl.Tests
{
    public class LaunchComplexTests
    {
        [Test]
        public void Equal()
        {
            Guid theId = Guid.NewGuid();
            LaunchComplex complexA = new(theId);
            LaunchComplex complexB = new(Guid.NewGuid());
            Assert.That(complexA, Is.Not.EqualTo(complexB));
            complexB = new(theId);
            Assert.That(complexA, Is.EqualTo(complexB));

            complexA.Name = "39A";
            complexB.Name = "39B";
            Assert.That(complexA, Is.Not.EqualTo(complexB));
            complexB.Name = "39A";
            Assert.That(complexA, Is.EqualTo(complexB));

            complexA.HangarBay = new HangarBay() { Identifier = theId, Endpoint = new("http://1.2.3.4:8100") };
            complexB.HangarBay = new HangarBay() { Identifier = theId, Endpoint = new("http://1.2.3.4:8200") };
            Assert.That(complexA, Is.Not.EqualTo(complexB));
            complexB.HangarBay = new HangarBay() { Identifier = theId, Endpoint = new("http://1.2.3.4:8100") };
            Assert.That(complexA, Is.EqualTo(complexB));

            complexA.LaunchPads = new[] { new LaunchPad() { Name = "LaunchPadA" }, new LaunchPad() { Name = "LaunchPadB" } };
            complexB.LaunchPads = new[] { new LaunchPad() { Name = "LaunchPadA" }, new LaunchPad() { Name = "LaunchPadC" } };
            Assert.That(complexA, Is.Not.EqualTo(complexB));
            complexB.LaunchPads = new[] { new LaunchPad() { Name = "LaunchPadA" }, new LaunchPad() { Name = "LaunchPadB" } };
            Assert.That(complexA, Is.EqualTo(complexB));
        }

        [Test]
        public void SerializeDefault()
        {
            LaunchComplex toSerialize = new(Guid.NewGuid());
            var serializedParameter = JsonSerializer.Serialize(toSerialize, Json.SerializerOptions);
            var deserialized = JsonSerializer.Deserialize<LaunchComplex>(serializedParameter, Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void SerializeFull()
        {
            LaunchComplex toSerialize = new(Guid.NewGuid());
            toSerialize.Name = "39B";
            toSerialize.HangarBay = new HangarBay() { Identifier = toSerialize.Id, Endpoint = new("http://1.2.3.4:8100") };
            toSerialize.LaunchPads = new[] { new LaunchPad() { Name = "LaunchPadA" }, new LaunchPad() { Name = "LaunchPadB" } };
            var serializedParameter = JsonSerializer.Serialize(toSerialize, Json.SerializerOptions);
            var deserialized = JsonSerializer.Deserialize<LaunchComplex>(serializedParameter, Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
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
            toClone.Name = "39B";
            toClone.HangarBay = new HangarBay() { Identifier = toClone.Id, Endpoint = new("http://1.2.3.4:8100") };
            toClone.LaunchPads = new[] { new LaunchPad() { Name = "LaunchPadA" }, new LaunchPad() { Name = "LaunchPadB" } };
            var cloned = toClone.DeepClone();
            Assert.That(cloned, Is.EqualTo(toClone));

            // Verify that there isn't any sharing
            var serializeClone = JsonSerializer.Deserialize<LaunchComplex>(
                JsonSerializer.Serialize(toClone, Json.SerializerOptions), Json.SerializerOptions);
            Assert.That(serializeClone, Is.EqualTo(cloned));

            toClone.HangarBay.Endpoint = new("http://1.2.3.5:8100");
            Assert.That(serializeClone, Is.EqualTo(cloned));

            toClone.LaunchPads.ElementAt(0).Name = "Will explode";
            Assert.That(serializeClone, Is.EqualTo(cloned));
            toClone.LaunchPads.ElementAt(1).Name = "Please don't use";
            Assert.That(serializeClone, Is.EqualTo(cloned));
        }
    }
}
