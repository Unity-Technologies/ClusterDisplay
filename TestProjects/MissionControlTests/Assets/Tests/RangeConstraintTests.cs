using System;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Unity.ClusterDisplay.MissionControl
{
    public class RangeConstraintTests
    {
        [Test]
        public void Equal()
        {
            RangeConstraint constraintA = new();
            RangeConstraint constraintB = new();

            Assert.That(constraintA, Is.EqualTo(constraintB));

            constraintA.Min = 28;
            constraintB.Min = 28.0;
            Assert.That(constraintA, Is.Not.EqualTo(constraintB));
            Assert.That(constraintA.MinInt32, Is.EqualTo(28));
            Assert.That(constraintA.MinSingle, Is.EqualTo(28.0f));
            Assert.That(constraintB.MinInt32, Is.EqualTo(28));
            Assert.That(constraintB.MinSingle, Is.EqualTo(28.0f));

            constraintB.Min = 28;
            Assert.That(constraintA, Is.EqualTo(constraintB));

            constraintA.Max = 42;
            constraintB.Max = 42.0;
            Assert.That(constraintA, Is.Not.EqualTo(constraintB));
            Assert.That(constraintA.MaxInt32, Is.EqualTo(42));
            Assert.That(constraintA.MaxSingle, Is.EqualTo(42.0f));
            Assert.That(constraintB.MaxInt32, Is.EqualTo(42));
            Assert.That(constraintB.MaxSingle, Is.EqualTo(42.0f));

            constraintA.Max = 42.0;
            Assert.That(constraintA, Is.EqualTo(constraintB));

            constraintA.MinExclusive = true;
            constraintB.MinExclusive = false;
            Assert.That(constraintA, Is.Not.EqualTo(constraintB));

            constraintB.MinExclusive = true;
            Assert.That(constraintA, Is.EqualTo(constraintB));

            constraintA.MaxExclusive = true;
            constraintB.MaxExclusive = false;
            Assert.That(constraintA, Is.Not.EqualTo(constraintB));

            constraintB.MaxExclusive = true;
            Assert.That(constraintA, Is.EqualTo(constraintB));
        }

        [Test]
        public void SerializeDefault()
        {
            RangeConstraint toSerialize = new();
            var serializedConstraint = JsonConvert.SerializeObject(toSerialize, Json.SerializerOptions);
            Assert.That(serializedConstraint, Is.EqualTo("{\"type\":\"range\"}"));
            var deserialized = JsonConvert.DeserializeObject<Constraint>(serializedConstraint, Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void SerializeWithMinMaxInt()
        {
            RangeConstraint toSerialize = new();
            toSerialize.Min = 28;
            toSerialize.MinExclusive = true;
            toSerialize.Max = 42;
            toSerialize.MaxExclusive = true;
            var serializedConstraint = JsonConvert.SerializeObject(toSerialize, Json.SerializerOptions);
            var deserialized = JsonConvert.DeserializeObject<Constraint>(serializedConstraint, Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void SerializeWithMinMaxFloat()
        {
            RangeConstraint toSerialize = new();
            toSerialize.Min = 28.42f;
            toSerialize.MinExclusive = false;
            toSerialize.Max = 42.28f;
            toSerialize.MaxExclusive = true;
            var serializedConstraint = JsonConvert.SerializeObject(toSerialize, Json.SerializerOptions);
            var deserialized = JsonConvert.DeserializeObject<Constraint>(serializedConstraint, Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void SerializeWithMinMaxDouble()
        {
            RangeConstraint toSerialize = new();
            toSerialize.Min = 28.42;
            toSerialize.MinExclusive = true;
            toSerialize.Max = 42.28;
            toSerialize.MaxExclusive = false;
            var serializedConstraint = JsonConvert.SerializeObject(toSerialize, Json.SerializerOptions);
            var deserialized = JsonConvert.DeserializeObject<Constraint>(serializedConstraint, Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void SerializeWithMinMaxLong()
        {
            RangeConstraint toSerialize = new();
            toSerialize.Min = 28L;
            toSerialize.MinExclusive = true;
            toSerialize.Max = 42L;
            toSerialize.MaxExclusive = true;
            var serializedConstraint = JsonConvert.SerializeObject(toSerialize, Json.SerializerOptions);
            var deserialized = JsonConvert.DeserializeObject<Constraint>(serializedConstraint, Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void CloneDefault()
        {
            RangeConstraint toClone = new();
            Constraint cloned = toClone.DeepClone();
            Assert.That(cloned, Is.Not.Null);
            Assert.That(cloned, Is.EqualTo(toClone));
        }

        [Test]
        public void CloneFull()
        {
            RangeConstraint toClone = new();
            toClone.Min = 28;
            toClone.MinExclusive = true;
            toClone.Max = 42;
            toClone.MaxExclusive = true;
            Constraint cloned = toClone.DeepClone();
            Assert.That(cloned, Is.Not.Null);
            Assert.That(cloned, Is.EqualTo(toClone));
        }

        [Test]
        public void MinMaxFromBadValues()
        {
            RangeConstraint toSerialize = new();
            Assert.Throws<FormatException>(() => toSerialize.Min = "Patate");
            Assert.Throws<OverflowException>(() => toSerialize.Min = ulong.MaxValue);
            Assert.Throws<InvalidCastException>(() => toSerialize.Min = Guid.NewGuid());
            Assert.Throws<InvalidCastException>(() => toSerialize.Min = new LaunchCatalog.Payload());
        }
    }
}
