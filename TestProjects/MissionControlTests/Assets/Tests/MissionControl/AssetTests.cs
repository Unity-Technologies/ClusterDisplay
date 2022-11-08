using System;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    public class AssetTests
    {
        [Test]
        public void Equal()
        {
            Guid theId = Guid.NewGuid();
            Asset assetA = new(theId);
            Asset assetB = new(Guid.NewGuid());
            Assert.That(assetA, Is.Not.EqualTo(assetB));
            assetB = new(theId);
            Assert.That(assetA, Is.EqualTo(assetB));

            assetA.Launchables.AddRange(new[] {
                new Launchable() {Name = "Launchable A"},
                new Launchable() {Name = "Launchable B"}
            });
            assetB.Launchables.AddRange(new[] {
                new Launchable() {Name = "Launchable C"},
                new Launchable() {Name = "Launchable D"},
            });
            Assert.That(assetA, Is.Not.EqualTo(assetB));
            assetB.Launchables.Clear();
            assetB.Launchables.AddRange(new[] {
                new Launchable() {Name = "Launchable A"},
                new Launchable() {Name = "Launchable B"}
            });
            Assert.That(assetA, Is.EqualTo(assetB));
        }

        [Test]
        public void RoundTrip()
        {
            Asset toSerialize = new(Guid.NewGuid());
            toSerialize.Launchables.AddRange(new[] {
                new Launchable() {Name = "Launchable A"},
                new Launchable() {Name = "Launchable B"}
            });

            var jsonString = JsonConvert.SerializeObject(toSerialize, Json.SerializerOptions);
            var deserialized = JsonConvert.DeserializeObject<Asset>(jsonString, Json.SerializerOptions);

            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void Deserialize()
        {
            var jsonString = ("{'id':'ba50c53f-fe41-4016-bb02-7494a007d908','launchables':[{'payloads':[" +
                "'fa5920ef-1406-4b23-ae6b-d6083a244626','3933c785-e930-4f90-b8bc-3c0b1ca595cd'],'name':" +
                "'ClusterDisplay Capcom','type':'capcom','globalParameters':[],'launchComplexParameters':[]," +
                "'launchPadParameters':[],'preLaunchPath':'','launchPath':'assemblyrun://QuadroSyncTests_Data%2f" +
                "Managed%2fUnity.ClusterDisplay.MissionControl.Capcom.dll/Unity.ClusterDisplay.MissionControl.Capcom." +
                "MainClass/Main','landingTimeSec':0},{'payloads':['b3df2aea-9c1f-4375-b441-470088be8c0c'," +
                "'3933c785-e930-4f90-b8bc-3c0b1ca595cd'],'name':'QuadroSyncTests','type':'clusterNode'," +
                "'globalParameters':[{'name':'','group':'','id':'RepeaterCount','description':'','type':'integer'," +
                "'constraint':{'min':0,'max':255,'type':'range'},'defaultValue':0,'toBeRevisedByCapcom':true," +
                "'hidden':true},{'name':'Multicast address','group':'','id':'MulticastAddress','description':'IPv4 " +
                "multicast UDP address used for inter-node communication (state propagation).','type':'string'," +
                "'constraint':{'regularExpression':'^((25[0-5]|(2[0-4]|1\\\\d|[1-9]|)\\\\d)\\\\.?\\\\b){4}$','type':" +
                "'regularExpression'},'defaultValue':'224.0.1.0','toBeRevisedByCapcom':false,'hidden':false},{'name':" +
                "'Multicast port','group':'','id':'MulticastPort','description':'Multicast UDP port used for inter-" +
                "node communication (state propagation).','type':'integer','constraint':{'min':1,'max':65535,'type':" +
                "'range'},'defaultValue':25690,'toBeRevisedByCapcom':false,'hidden':false},{'name':'Target frame " +
                "rate','group':'','id':'TargetFrameRate','description':'Target frame per seconds at which the cluster " +
                "should run.  Set to 0 for unlimited.','type':'integer','constraint':{'min':0,'max':240,'type':" +
                "'range'},'defaultValue':60,'toBeRevisedByCapcom':false,'hidden':false},{'name':'Delay repeaters'," +
                "'group':'','id':'DelayRepeaters','description':'Delay rendering of repeaters by one frame, to be " +
                "used when repeaters depends on state computed during the frame processing of the emitter.  " +
                "Increases latency of the system by one frame.','type':'boolean','defaultValue':false," +
                "'toBeRevisedByCapcom':false,'hidden':false},{'name':'Handshake timeout (seconds)','group':'','id':" +
                "'HandshakeTimeout','description':'Timeout for a starting node to perform handshake with the other " +
                "nodes during cluster startup.','type':'float','constraint':{'min':0,'minExclusive':true,'type':" +
                "'range'},'defaultValue':30,'toBeRevisedByCapcom':false,'hidden':false},{'name':'Communication " +
                "timeout (seconds)','group':'','id':'CommunicationTimeout','description':'Timeout for communication " +
                "once the cluster is started.','type':'float','constraint':{'min':0,'minExclusive':true,'type':" +
                "'range'},'defaultValue':5,'toBeRevisedByCapcom':false,'hidden':false},{'name':'Enable hardware " +
                "synchronization','group':'','id':'EnableHardwareSync','description':'Does the cluster tries to use " +
                "hardware synchronization?','type':'boolean','defaultValue':true,'toBeRevisedByCapcom':false," +
                "'hidden':false}],'launchComplexParameters':[],'launchPadParameters':[{'name':'Node identifier'," +
                "'group':'','id':'NodeId','description':'Unique identifier among the nodes of the cluster, keep " +
                "default value for an automatic assignment based on the order of the launchpad in the launch " +
                "configuration.','type':'integer','constraint':{'min':-1,'max':255,'type':'range'},'defaultValue':-1," +
                "'toBeRevisedByCapcom':true,'hidden':false},{'name':'Node role','group':'','id':'NodeRole'," +
                "'description':'Role of the node in the cluster.  One node is to be configured as the Emitter while " +
                "the other ones should be configured as a Repeater.','type':'string','constraint':{'choices':[" +
                "'Unassigned','Emitter','Repeater'],'type':'list'},'defaultValue':'Unassigned','toBeRevisedByCapcom':" +
                "true,'hidden':false},{'name':'Multicast adapter name','group':'','id':'MulticastAdapterName'," +
                "'description':'Network adapter name (or ip) identifying the network adapter to use for inter-node " +
                "communication (state propagation).  Default adapter defined in the launchpads configuration will be " +
                "used if not specified.','type':'string','defaultValue':'','toBeRevisedByCapcom':false,'hidden':" +
                "false}],'preLaunchPath':'','launchPath':'QuadroSyncTests.exe','landingTimeSec':0}],'storageSize':" +
                "29731938,'name':'Spinning Cube - CD-206','description':''}").Replace('\'', '"');
            var deserialized = JsonConvert.DeserializeObject<Asset>(jsonString, Json.SerializerOptions);

            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized.Id, Is.EqualTo(Guid.Parse("ba50c53f-fe41-4016-bb02-7494a007d908")));
            Assert.That(deserialized.Launchables.Count, Is.EqualTo(2));
            Assert.That(deserialized.Launchables[0].Name, Is.EqualTo("ClusterDisplay Capcom"));
            Assert.That(deserialized.Launchables[0].Type, Is.EqualTo("capcom"));
            Assert.That(deserialized.Launchables[1].Name, Is.EqualTo("QuadroSyncTests"));
            Assert.That(deserialized.Launchables[1].Type, Is.EqualTo("clusterNode"));
        }

        [Test]
        public void DeepCloneDefault()
        {
            Asset toClone = new(Guid.NewGuid());
            var cloned = toClone.DeepClone();
            Assert.That(cloned, Is.EqualTo(toClone));
        }

        [Test]
        public void DeepCloneFull()
        {
            Asset toClone = new(Guid.NewGuid());
            toClone.Launchables.AddRange(new[] {
                new Launchable() {Name = "Launchable A"},
                new Launchable() {Name = "Launchable B"}
            });
            var cloned = toClone.DeepClone();
            Assert.That(cloned, Is.EqualTo(toClone));

            // Verify that there isn't any sharing
            var serializeClone = JsonConvert.DeserializeObject<Asset>(
                JsonConvert.SerializeObject(toClone, Json.SerializerOptions), Json.SerializerOptions);
            Assert.That(cloned, Is.EqualTo(serializeClone));

            toClone.Launchables[0].Name = "Launchable 1";
            toClone.Launchables[1].Name = "Launchable 2";
            Assert.That(cloned, Is.EqualTo(serializeClone));
        }
    }
}
