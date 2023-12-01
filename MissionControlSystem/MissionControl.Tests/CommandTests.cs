using System.Text.Json;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    public class CommandTests
    {
        [Test]
        public void Shutdown()
        {
            var toSerialize = new ShutdownCommand();
            var serializedCommand = JsonSerializer.Serialize(toSerialize, Json.SerializerOptions);
            var deserialized = JsonSerializer.Deserialize<Command>(serializedCommand, Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void Restart()
        {
            var toSerialize = new RestartCommand();
            toSerialize.TimeoutSec = 42;
            var serializedCommand = JsonSerializer.Serialize(toSerialize, Json.SerializerOptions);
            var deserialized = JsonSerializer.Deserialize<Command>(serializedCommand, Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void ForceState()
        {
            var toSerialize = new ForceStateCommand();
            toSerialize.State = State.Launched;
            toSerialize.KeepLocked = true;
            toSerialize.ControlFile = "Patate.txt";
            var serializedCommand = JsonSerializer.Serialize(toSerialize, Json.SerializerOptions);
            var deserialized = JsonSerializer.Deserialize<Command>(serializedCommand, Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }
    }
}
