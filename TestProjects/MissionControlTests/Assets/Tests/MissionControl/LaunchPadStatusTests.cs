using System;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;

using LaunchPadState = Unity.ClusterDisplay.MissionControl.LaunchPad.State;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    public class LaunchPadStatusTests
    {
        [Test]
        public void Equal()
        {
            Guid theId = Guid.NewGuid();
            LaunchPadStatus statusA = new(theId);
            LaunchPadStatus statusB = new(Guid.NewGuid());
            Assert.That(statusA, Is.Not.EqualTo(statusB));
            statusB = new(theId);
            Assert.That(statusA, Is.EqualTo(statusB));

            statusA.State = LaunchPadState.Launched;
            statusB.State = LaunchPadState.Over;
            Assert.That(statusA, Is.Not.EqualTo(statusB));
            statusB.State = LaunchPadState.Launched;
            Assert.That(statusA, Is.EqualTo(statusB));

            statusA.IsDefined = true;
            statusB.IsDefined = false;
            Assert.That(statusA, Is.Not.EqualTo(statusB));
            statusB.IsDefined = true;
            Assert.That(statusA, Is.EqualTo(statusB));

            statusA.DynamicEntries = new[] { new LaunchPadReportDynamicEntry() { Name = "ValueA" } };
            statusB.DynamicEntries = new[] { new LaunchPadReportDynamicEntry() { Name = "ValueB" } };
            Assert.That(statusA, Is.Not.EqualTo(statusB));
            statusB.DynamicEntries = new[] { new LaunchPadReportDynamicEntry() { Name = "ValueA" } };
            Assert.That(statusA, Is.EqualTo(statusB));
        }

        [Test]
        public void RoundTrip()
        {
            LaunchPadStatus toSerialize = new(Guid.NewGuid());
            toSerialize.State = LaunchPadState.Launched;
            toSerialize.IsDefined = true;
            toSerialize.DynamicEntries = new LaunchPadReportDynamicEntry[] { new () { Name = "Value1" }, new () { Name = "Value2" } };

            var jsonString = JsonConvert.SerializeObject(toSerialize, Json.SerializerOptions);
            var deserialized = JsonConvert.DeserializeObject<LaunchPadStatus>(jsonString, Json.SerializerOptions);

            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void Deserialize()
        {
            var jsonString = ("{'id':'59500aca-62cc-440f-8e27-00e4297f0db1','isDefined':true,'updateError':''," +
                "'dynamicEntries':[{'name':'Role','value':'Emitter'},{'name':'Node id','value':0}," +
                "{'name':'Render node id','value':0}],'version':'1.0.0.0'," +
                "'startTime':'2023-01-16T11:35:04.654262-05:00','state':'launched','pendingRestart':false," +
                "'lastChanged':'2023-01-16T15:18:36.0126009-05:00','statusNumber':151}")
                .Replace('\'', '"');
            var deserialized = JsonConvert.DeserializeObject<LaunchPadStatus>(jsonString, Json.SerializerOptions);

            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized.Id, Is.EqualTo(Guid.Parse("59500aca-62cc-440f-8e27-00e4297f0db1")));
            Assert.That(deserialized.State, Is.EqualTo(LaunchPadState.Launched));
            Assert.That(deserialized.IsDefined, Is.True);
            Assert.That(deserialized.DynamicEntries.Count(), Is.EqualTo(3));
            Assert.That(deserialized.DynamicEntries.ElementAt(0).Name, Is.EqualTo("Role"));
            Assert.That(deserialized.DynamicEntries.ElementAt(0).Value, Is.EqualTo("Emitter"));
            Assert.That(deserialized.DynamicEntries.ElementAt(1).Name, Is.EqualTo("Node id"));
            Assert.That(deserialized.DynamicEntries.ElementAt(1).Value, Is.EqualTo(0));
            Assert.That(deserialized.DynamicEntries.ElementAt(2).Name, Is.EqualTo("Render node id"));
            Assert.That(deserialized.DynamicEntries.ElementAt(2).Value, Is.EqualTo(0));
        }

        [Test]
        public void DeepCloneDefault()
        {
            LaunchPadStatus toClone = new(Guid.NewGuid());
            var cloned = toClone.DeepClone();
            Assert.That(cloned, Is.EqualTo(toClone));
        }

        [Test]
        public void DeepCloneFull()
        {
            LaunchPadStatus toClone = new(Guid.NewGuid());
            toClone.State = LaunchPadState.Launched;
            toClone.IsDefined = true;
            toClone.DynamicEntries = new LaunchPadReportDynamicEntry[] { new () { Name = "Value1" }, new () { Name = "Value2" } };
            var cloned = toClone.DeepClone();
            Assert.That(cloned, Is.EqualTo(toClone));
        }
    }
}
