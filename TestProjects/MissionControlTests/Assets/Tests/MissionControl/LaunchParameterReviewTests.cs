using System;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    public class LaunchParameterReviewTests
    {
        [Test]
        public void Equal()
        {
            Guid theId = Guid.NewGuid();
            LaunchParameterForReview parameterA = new(theId);
            LaunchParameterForReview parameterB = new(Guid.NewGuid());
            Assert.That(parameterA, Is.Not.EqualTo(parameterB));
            parameterB = new(theId);
            Assert.That(parameterA, Is.EqualTo(parameterB));

            parameterA.LaunchPadId = Guid.NewGuid();
            parameterB.LaunchPadId = Guid.NewGuid();
            Assert.That(parameterA, Is.Not.EqualTo(parameterB));
            parameterB.LaunchPadId = parameterA.LaunchPadId;
            Assert.That(parameterA, Is.EqualTo(parameterB));

            parameterA.Value = new LaunchParameterValue() { Id = "MyParameter", Value = 42 };
            parameterB.Value = new LaunchParameterValue() { Id = "SomeOtherParameter", Value = 28 };
            Assert.That(parameterA, Is.Not.EqualTo(parameterB));
            parameterB.Value = new LaunchParameterValue() { Id = "MyParameter", Value = 42 };
            Assert.That(parameterA, Is.EqualTo(parameterB));

            parameterA.Ready = true;
            parameterB.Ready = false;
            Assert.That(parameterA, Is.Not.EqualTo(parameterB));
            parameterB.Ready = true;
            Assert.That(parameterA, Is.EqualTo(parameterB));

            parameterA.ReviewComments = "I don't like that number";
            parameterB.ReviewComments = "Two nodes have the same value";
            Assert.That(parameterA, Is.Not.EqualTo(parameterB));
            parameterB.ReviewComments = "I don't like that number";
            Assert.That(parameterA, Is.EqualTo(parameterB));
        }

        [Test]
        public void SerializeDefault()
        {
            LaunchParameterForReview toSerialize = new(Guid.NewGuid());
            var serializedParameter = JsonConvert.SerializeObject(toSerialize, Json.SerializerOptions);
            var deserialized = JsonConvert.DeserializeObject<LaunchParameterForReview>(serializedParameter, Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void SerializeFull()
        {
            LaunchParameterForReview toSerialize = new(Guid.NewGuid());
            toSerialize.LaunchPadId = Guid.NewGuid();
            toSerialize.Value = new LaunchParameterValue() { Id = "MyParameter", Value = 42 };
            toSerialize.Ready = true;
            toSerialize.ReviewComments = "Two nodes have the same value";
            var serializedParameter = JsonConvert.SerializeObject(toSerialize, Json.SerializerOptions);
            var deserialized = JsonConvert.DeserializeObject<LaunchParameterForReview>(serializedParameter, Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void DeepCloneDefault()
        {
            LaunchParameterForReview toClone = new(Guid.NewGuid());
            var cloned = toClone.DeepClone();
            Assert.That(cloned, Is.EqualTo(toClone));
        }

        [Test]
        public void DeepCloneFull()
        {
            LaunchParameterForReview toClone = new(Guid.NewGuid());
            toClone.LaunchPadId = Guid.NewGuid();
            toClone.Value = new LaunchParameterValue() { Id = "MyParameter", Value = 42 };
            toClone.Ready = true;
            toClone.ReviewComments = "Two nodes have the same value";
            var cloned = toClone.DeepClone();
            Assert.That(cloned, Is.EqualTo(toClone));

            // Verify that there isn't any sharing
            var serializeClone = JsonConvert.DeserializeObject<LaunchParameterForReview>(
                JsonConvert.SerializeObject(toClone, Json.SerializerOptions), Json.SerializerOptions);
            Assert.That(cloned, Is.EqualTo(serializeClone));

            toClone.Value.Id = "SomeOtherParameter";
            toClone.Value.Value = 28;
            Assert.That(cloned, Is.EqualTo(serializeClone));
        }
    }
}
