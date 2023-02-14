using System;
using System.Text.Json;
using Unity.ClusterDisplay.MissionControl.LaunchCatalog;

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
            var serializedConstraint = JsonSerializer.Serialize(toSerialize, Json.SerializerOptions);
            Assert.That(serializedConstraint, Is.EqualTo("{\"type\":\"range\"}"));
            var deserialized = JsonSerializer.Deserialize<Constraint>(serializedConstraint, Json.SerializerOptions);
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
            var serializedConstraint = JsonSerializer.Serialize(toSerialize, Json.SerializerOptions);
            var deserialized = JsonSerializer.Deserialize<Constraint>(serializedConstraint, Json.SerializerOptions);
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
            var serializedConstraint = JsonSerializer.Serialize(toSerialize, Json.SerializerOptions);
            var deserialized = JsonSerializer.Deserialize<Constraint>(serializedConstraint, Json.SerializerOptions);
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
            var serializedConstraint = JsonSerializer.Serialize(toSerialize, Json.SerializerOptions);
            var deserialized = JsonSerializer.Deserialize<Constraint>(serializedConstraint, Json.SerializerOptions);
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
            var serializedConstraint = JsonSerializer.Serialize(toSerialize, Json.SerializerOptions);
            var deserialized = JsonSerializer.Deserialize<Constraint>(serializedConstraint, Json.SerializerOptions);
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
            Assert.Throws<InvalidCastException>(() => toSerialize.Min = new Payload());
        }

        [Test]
        public void Validate()
        {
            RangeConstraint constraint = new();

            constraint.Min = 28;
            constraint.Max = 42;
            Assert.That(constraint.Validate(27), Is.False);
            Assert.That(constraint.Validate(28), Is.True);
            Assert.That(constraint.Validate(42), Is.True);
            Assert.That(constraint.Validate(43), Is.False);

            Assert.That(constraint.Validate(27.9f), Is.False);
            Assert.That(constraint.Validate(28.0f), Is.True);
            Assert.That(constraint.Validate(42.0f), Is.True);
            Assert.That(constraint.Validate(42.1f), Is.False);

            constraint.MinExclusive = true;
            Assert.That(constraint.Validate(28), Is.False);
            Assert.That(constraint.Validate(29), Is.True);
            Assert.That(constraint.Validate(42), Is.True);
            Assert.That(constraint.Validate(43), Is.False);

            Assert.That(constraint.Validate(28.0), Is.False);
            Assert.That(constraint.Validate(28.1), Is.True);
            Assert.That(constraint.Validate(42.0), Is.True);
            Assert.That(constraint.Validate(42.1), Is.False);
            constraint.MinExclusive = false;

            constraint.MaxExclusive = true;
            Assert.That(constraint.Validate(27), Is.False);
            Assert.That(constraint.Validate(28), Is.True);
            Assert.That(constraint.Validate(41), Is.True);
            Assert.That(constraint.Validate(42), Is.False);

            Assert.That(constraint.Validate(27.9f), Is.False);
            Assert.That(constraint.Validate(28.0f), Is.True);
            Assert.That(constraint.Validate(41.9f), Is.True);
            Assert.That(constraint.Validate(42.0f), Is.False);
            constraint.MaxExclusive = false;
        }

        [Test]
        public void MinMaxInt32Inclusive()
        {
            RangeConstraint toTest = new();
            Assert.That(toTest.MinInt32Inclusive, Is.EqualTo(int.MinValue));
            toTest.Min = 28;
            Assert.That(toTest.MinInt32Inclusive, Is.EqualTo(28));
            Assert.That(toTest.MaxInt32Inclusive, Is.EqualTo(int.MaxValue));
            toTest.Max = 42;
            Assert.That(toTest.MinInt32Inclusive, Is.EqualTo(28));
            Assert.That(toTest.MaxInt32Inclusive, Is.EqualTo(42));
            toTest.MinExclusive = true;
            Assert.That(toTest.MinInt32Inclusive, Is.EqualTo(29));
            Assert.That(toTest.MaxInt32Inclusive, Is.EqualTo(42));
            toTest.MaxExclusive = true;
            Assert.That(toTest.MinInt32Inclusive, Is.EqualTo(29));
            Assert.That(toTest.MaxInt32Inclusive, Is.EqualTo(41));
        }

        [Test]
        public void MinMaxDecimalInclusive()
        {
            RangeConstraint toTest = new();
            Assert.That(toTest.MinDecimalInclusive, Is.EqualTo(decimal.MinValue));
            toTest.Min = 28;
            Assert.That(toTest.MinDecimalInclusive, Is.EqualTo(28));
            Assert.That(toTest.MaxDecimalInclusive, Is.EqualTo(decimal.MaxValue));
            toTest.Max = 42;
            Assert.That(toTest.MinDecimalInclusive, Is.EqualTo(28));
            Assert.That(toTest.MaxDecimalInclusive, Is.EqualTo(42));
            toTest.MinExclusive = true;
            Assert.That(toTest.MinDecimalInclusive, Is.EqualTo(28.00001m));
            Assert.That(toTest.MaxDecimalInclusive, Is.EqualTo(42));
            toTest.MaxExclusive = true;
            Assert.That(toTest.MinDecimalInclusive, Is.EqualTo(28.00001m));
            Assert.That(toTest.MaxDecimalInclusive, Is.EqualTo(41.99999m));
        }
    }
}
