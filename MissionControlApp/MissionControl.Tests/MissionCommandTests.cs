using System;
using System.Text.Json;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    public class MissionCommandTests
    {
        [Test]
        public void SaveEqual()
        {
            SaveMissionCommand commandA = new();
            SaveMissionCommand commandB = new();
            Assert.That(commandA, Is.EqualTo(commandB));

            commandA.Identifier = Guid.NewGuid();
            commandB.Identifier = Guid.NewGuid();
            Assert.That(commandA, Is.Not.EqualTo(commandB));
            commandB.Identifier = commandA.Identifier;
            Assert.That(commandA, Is.EqualTo(commandB));

            commandA.Description.Name = "A name";
            commandB.Description.Name = "Another name";
            Assert.That(commandA, Is.Not.EqualTo(commandB));
            commandB.Description.Name = "A name";
            Assert.That(commandA, Is.EqualTo(commandB));

            commandA.Description.Details = "Some details";
            commandB.Description.Details = "Another set of details";
            Assert.That(commandA, Is.Not.EqualTo(commandB));
            commandB.Description.Details = "Some details";
            Assert.That(commandA, Is.EqualTo(commandB));
        }

        [Test]
        public void SaveSerialize()
        {
            SaveMissionCommand toSerialize = new();
            toSerialize.Identifier = Guid.NewGuid();
            toSerialize.Description.Name = "Some name";
            toSerialize.Description.Details = "Some details";
            var serializedCommand = JsonSerializer.Serialize(toSerialize, Json.SerializerOptions);
            var deserialized = JsonSerializer.Deserialize<SaveMissionCommand>(serializedCommand, Json.SerializerOptions);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void LoadEqual()
        {
            LoadMissionCommand commandA = new();
            LoadMissionCommand commandB = new();
            Assert.That(commandA, Is.EqualTo(commandB));

            commandA.Identifier = Guid.NewGuid();
            commandB.Identifier = Guid.NewGuid();
            Assert.That(commandA, Is.Not.EqualTo(commandB));
            commandB.Identifier = commandA.Identifier;
            Assert.That(commandA, Is.EqualTo(commandB));
        }

        [Test]
        public void LoadSerialize()
        {
            LoadMissionCommand toSerialize = new();
            toSerialize.Identifier = Guid.NewGuid();
            var serializedCommand = JsonSerializer.Serialize(toSerialize, Json.SerializerOptions);
            var deserialized = JsonSerializer.Deserialize<LoadMissionCommand>(serializedCommand, Json.SerializerOptions);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void LaunchSerialize()
        {
            LaunchMissionCommand toSerialize = new();
            var serializedCommand = JsonSerializer.Serialize(toSerialize, Json.SerializerOptions);
            var deserialized = JsonSerializer.Deserialize<LaunchMissionCommand>(serializedCommand, Json.SerializerOptions);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void StopSerialize()
        {
            StopMissionCommand toSerialize = new();
            var serializedCommand = JsonSerializer.Serialize(toSerialize, Json.SerializerOptions);
            var deserialized = JsonSerializer.Deserialize<StopMissionCommand>(serializedCommand, Json.SerializerOptions);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }
    }
}
