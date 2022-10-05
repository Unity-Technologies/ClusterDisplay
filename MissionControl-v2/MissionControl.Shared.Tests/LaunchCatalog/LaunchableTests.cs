using System.Text.Json;

namespace Unity.ClusterDisplay.MissionControl.LaunchCatalog.Tests
{
    public class LaunchableTests
    {
        [Test]
        public void Equal()
        {
            Launchable launchableA = new();
            Launchable launchableB = new();
            Assert.That(launchableA, Is.EqualTo(launchableB));

            launchableA.Name = "Cluster Node";
            launchableB.Name = "Live Edit";
            Assert.That(launchableA, Is.Not.EqualTo(launchableB));
            launchableB.Name = "Cluster Node";
            Assert.That(launchableA, Is.EqualTo(launchableB));

            launchableA.Type = "clusterNode";
            launchableB.Type = "liveEdit";
            Assert.That(launchableA, Is.Not.EqualTo(launchableB));
            launchableB.Type = "clusterNode";
            Assert.That(launchableA, Is.EqualTo(launchableB));

            launchableA.Data = new { StringProperty = "StringValue", NumberProperty = 42 };
            Assert.That(launchableA, Is.Not.EqualTo(launchableB));
            launchableB.Data = launchableA.Data;
            launchableA.Data = null;
            Assert.That(launchableA, Is.Not.EqualTo(launchableB));
            launchableA.Data = new { OtherStringProperty = "OtherStringValue", OtherNumberProperty = 28 };
            Assert.That(launchableA, Is.Not.EqualTo(launchableB));
            launchableA.Data = new { StringProperty = "StringValue", NumberProperty = 42.0 };
            Assert.That(launchableA, Is.EqualTo(launchableB));

            launchableA.GlobalParameters = new[] { new LaunchParameter() { Name = "Global parameter", Type = LaunchParameterType.Integer, DefaultValue = 42 } };
            launchableB.GlobalParameters = new[] { new LaunchParameter() { Name = "Other global parameter", Type = LaunchParameterType.Float, DefaultValue = 42.28 } };
            Assert.That(launchableA, Is.Not.EqualTo(launchableB));
            launchableB.GlobalParameters = new[] { new LaunchParameter() { Name = "Global parameter", Type = LaunchParameterType.Integer, DefaultValue = 42 } };
            Assert.That(launchableA, Is.EqualTo(launchableB));

            launchableA.LaunchComplexParameters = new[] { new LaunchParameter() { Name = "Launch complex parameter", Type = LaunchParameterType.Integer, DefaultValue = 42 } };
            launchableB.LaunchComplexParameters = new[] { new LaunchParameter() { Name = "Other launch complex parameter", Type = LaunchParameterType.Float, DefaultValue = 42.28 } };
            Assert.That(launchableA, Is.Not.EqualTo(launchableB));
            launchableB.LaunchComplexParameters = new[] { new LaunchParameter() { Name = "Launch complex parameter", Type = LaunchParameterType.Integer, DefaultValue = 42 } };
            Assert.That(launchableA, Is.EqualTo(launchableB));

            launchableA.LaunchPadParameters = new[] { new LaunchParameter() { Name = "Launchpad parameter", Type = LaunchParameterType.Integer, DefaultValue = 42 } };
            launchableB.LaunchPadParameters = new[] { new LaunchParameter() { Name = "Other launchpad parameter", Type = LaunchParameterType.Float, DefaultValue = 42.28 } };
            Assert.That(launchableA, Is.Not.EqualTo(launchableB));
            launchableB.LaunchPadParameters = new[] { new LaunchParameter() { Name = "Launchpad parameter", Type = LaunchParameterType.Integer, DefaultValue = 42 } };
            Assert.That(launchableA, Is.EqualTo(launchableB));

            launchableA.PreLaunchPath = "prelaunch.ps1";
            launchableB.PreLaunchPath = "somethingelse.exe";
            Assert.That(launchableA, Is.Not.EqualTo(launchableB));
            launchableB.PreLaunchPath = "prelaunch.ps1";
            Assert.That(launchableA, Is.EqualTo(launchableB));

            launchableA.LaunchPath = "QuadroSyncTests.exe";
            launchableB.LaunchPath = "SpaceshipDemo.exe";
            Assert.That(launchableA, Is.Not.EqualTo(launchableB));
            launchableB.LaunchPath = "QuadroSyncTests.exe";
            Assert.That(launchableA, Is.EqualTo(launchableB));

            launchableA.Payloads = new[] { "Payload1", "Payload2" };
            launchableB.Payloads = new[] { "Payload3" };
            Assert.That(launchableA, Is.Not.EqualTo(launchableB));
            launchableB.Payloads = new[] { "Payload1", "Payload2" };
            Assert.That(launchableA, Is.EqualTo(launchableB));
        }

        [Test]
        public void SerializeDefault()
        {
            Launchable toSerialize = new();
            var serializedParameter = JsonSerializer.Serialize(toSerialize, Json.SerializerOptions);
            var deserialized = JsonSerializer.Deserialize<Launchable>(serializedParameter, Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void SerializeFull()
        {
            Launchable toSerialize = new();
            toSerialize.Name = "Cluster Node";
            toSerialize.Type = "clusterNode";
            toSerialize.Data = new { StringProperty = "StringValue", NumberProperty = 42 };
            toSerialize.GlobalParameters = new[] { new LaunchParameter() { Name = "Global parameter", Type = LaunchParameterType.Integer, DefaultValue = 42 } };
            toSerialize.LaunchComplexParameters = new[] { new LaunchParameter() { Name = "Launch complex parameter", Type = LaunchParameterType.Integer, DefaultValue = 42 } };
            toSerialize.LaunchPadParameters = new[] { new LaunchParameter() { Name = "Launchpad parameter", Type = LaunchParameterType.Integer, DefaultValue = 42 } };
            toSerialize.PreLaunchPath = "prelaunch.ps1";
            toSerialize.LaunchPath = "QuadroSyncTests.exe";
            toSerialize.Payloads = new[] { "Payload1", "Payload2" };
            var serializedParameter = JsonSerializer.Serialize(toSerialize, Json.SerializerOptions);
            var deserialized = JsonSerializer.Deserialize<Launchable>(serializedParameter, Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void ShallowCopy()
        {
            Launchable toCopy = new();
            toCopy.Name = "Cluster Node";
            toCopy.Type = "clusterNode";
            toCopy.Data = new { StringProperty = "StringValue", NumberProperty = 42 };
            toCopy.GlobalParameters = new[] { new LaunchParameter() { Name = "Global parameter", Type = LaunchParameterType.Integer, DefaultValue = 42 } };
            toCopy.LaunchComplexParameters = new[] { new LaunchParameter() { Name = "Launch complex parameter", Type = LaunchParameterType.Integer, DefaultValue = 42 } };
            toCopy.LaunchPadParameters = new[] { new LaunchParameter() { Name = "Launchpad parameter", Type = LaunchParameterType.Integer, DefaultValue = 42 } };
            toCopy.PreLaunchPath = "prelaunch.ps1";
            toCopy.LaunchPath = "QuadroSyncTests.exe";
            toCopy.Payloads = new[] { "Payload1", "Payload2" };

            Launchable copied = new();
            copied.ShallowCopy(toCopy);

            Assert.That(copied, Is.EqualTo(toCopy));
            Assert.That(copied.Data, Is.SameAs(toCopy.Data));
            Assert.That(copied.GlobalParameters, Is.SameAs(toCopy.GlobalParameters));
            Assert.That(copied.LaunchComplexParameters, Is.SameAs(toCopy.LaunchComplexParameters));
            Assert.That(copied.LaunchPadParameters, Is.SameAs(toCopy.LaunchPadParameters));
            Assert.That(copied.Payloads, Is.SameAs(toCopy.Payloads));
        }
    }
}
