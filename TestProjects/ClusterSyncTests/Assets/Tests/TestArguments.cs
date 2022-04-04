using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.ClusterDisplay;

namespace Unity.ClusterDisplay.Tests
{
    public class TestArguments
    {
        [Test]
        public void TestCommonArguments()
        {
            CommandLineParser.CacheArguments();

            Assert.That(CommandLineParser.debugFlag.Defined, $"The flag: \"{CommandLineParser.debugFlag.ArgumentName}\" is NOT defined.");
            Assert.That(CommandLineParser.debugFlag.Value, $"The flag: \"{CommandLineParser.debugFlag.ArgumentName}\" is NOT true.");

            Assert.That(CommandLineParser.replaceHeadlessEmitter.Defined, $"The flag: \"{CommandLineParser.replaceHeadlessEmitter.ArgumentName}\" is NOT defined.");
            Assert.That(CommandLineParser.replaceHeadlessEmitter.Value, $"The flag: \"{CommandLineParser.replaceHeadlessEmitter.ArgumentName}\" is NOT true.");

            Assert.That(CommandLineParser.delayRepeaters.Defined, $"The flag: \"{CommandLineParser.delayRepeaters.ArgumentName}\" is NOT defined.");
            Assert.That(CommandLineParser.delayRepeaters.Value, $"The flag: \"{CommandLineParser.delayRepeaters.ArgumentName}\" is NOT true.");

            Assert.That(CommandLineParser.gridSize.Defined, $"The flag: \"{CommandLineParser.gridSize.ArgumentName}\" is NOT defined.");
            Assert.That(CommandLineParser.gridSize.Value == new Vector2Int(2, 2), $"The flag: \"{CommandLineParser.gridSize.ArgumentName}\" is NOT 2x2.");

            Assert.That(CommandLineParser.bezel.Defined, $"The flag: \"{CommandLineParser.bezel.ArgumentName}\" is NOT defined.");
            Assert.That(CommandLineParser.bezel.Value == new Vector2Int(4, 4), $"The flag: \"{CommandLineParser.bezel.ArgumentName}\" is NOT 2x2.");

            Assert.That(CommandLineParser.physicalScreenSize.Defined, $"The flag: \"{CommandLineParser.physicalScreenSize.ArgumentName}\" is NOT defined.");
            Assert.That(CommandLineParser.physicalScreenSize.Value == new Vector2Int(1920, 1080), $"The flag: \"{CommandLineParser.physicalScreenSize.ArgumentName}\" is NOT 1920x1080.");

            Assert.That(CommandLineParser.targetFps.Defined, $"The flag: \"{CommandLineParser.targetFps.ArgumentName}\" is NOT defined.");
            Assert.That(CommandLineParser.targetFps.Value == 60, $"The flag: \"{CommandLineParser.targetFps.ArgumentName}\" is NOT 60.");

            Assert.That(CommandLineParser.overscan.Defined, $"The flag: \"{CommandLineParser.overscan.ArgumentName}\" is NOT defined.");
            Assert.That(CommandLineParser.overscan.Value == 128, $"The flag: \"{CommandLineParser.overscan.ArgumentName}\" is NOT 64.");

            Assert.That(CommandLineParser.quadroSyncInitDelay.Defined, $"The flag: \"{CommandLineParser.quadroSyncInitDelay.ArgumentName}\" is NOT defined.");
            Assert.That(CommandLineParser.quadroSyncInitDelay.Value == 66, $"The flag: \"{CommandLineParser.quadroSyncInitDelay.ArgumentName}\" is NOT 66.");

            Assert.That(CommandLineParser.linesThickness.Defined, $"The flag: \"{CommandLineParser.linesThickness.ArgumentName}\" is NOT defined.");
            Assert.That(CommandLineParser.linesThickness.Value == 1.2345f, $"The flag: \"{CommandLineParser.linesThickness.ArgumentName}\" is NOT 1.2345.");

            Assert.That(CommandLineParser.linesThickness.Defined, $"The flag: \"{CommandLineParser.linesThickness.ArgumentName}\" is NOT defined.");
            Assert.That(CommandLineParser.linesThickness.Value == 1.2345f, $"The flag: \"{CommandLineParser.linesThickness.ArgumentName}\" is NOT 1.2345.");

            Assert.That(CommandLineParser.linesScale.Defined, $"The flag: \"{CommandLineParser.linesScale.ArgumentName}\" is NOT defined.");
            Assert.That(CommandLineParser.linesScale.Value == 1.2345f, $"The flag: \"{CommandLineParser.linesScale.ArgumentName}\" is NOT 1.2345.");

            Assert.That(CommandLineParser.linesShiftSpeed.Defined, $"The flag: \"{CommandLineParser.linesShiftSpeed.ArgumentName}\" is NOT defined.");
            Assert.That(CommandLineParser.linesShiftSpeed.Value == 1.2345f, $"The flag: \"{CommandLineParser.linesShiftSpeed.ArgumentName}\" is NOT 1.2345.");

            Assert.That(CommandLineParser.linesAngle.Defined, $"The flag: \"{CommandLineParser.linesAngle.ArgumentName}\" is NOT defined.");
            Assert.That(CommandLineParser.linesAngle.Value == 1.2345f, $"The flag: \"{CommandLineParser.linesAngle.ArgumentName}\" is NOT 1.2345.");

            Assert.That(CommandLineParser.linesRotationSpeed.Defined, $"The flag: \"{CommandLineParser.linesRotationSpeed.ArgumentName}\" is NOT defined.");
            Assert.That(CommandLineParser.linesRotationSpeed.Value == 1.2345f, $"The flag: \"{CommandLineParser.linesRotationSpeed.ArgumentName}\" is NOT 1.2345.");

            Assert.That(CommandLineParser.adapterName.Defined, $"The flag: \"{CommandLineParser.adapterName.ArgumentName}\" is NOT defined.");
            Assert.That(CommandLineParser.adapterName.Value == "Ethernet", $"The flag: \"{CommandLineParser.adapterName.ArgumentName}\" is NOT Ethernet.");

            Assert.That(CommandLineParser.handshakeTimeout.Defined, $"The flag: \"{CommandLineParser.handshakeTimeout.ArgumentName}\" is NOT defined.");
            Assert.That(CommandLineParser.handshakeTimeout.Value == 5000, $"The flag: \"{CommandLineParser.handshakeTimeout.ArgumentName}\" is NOT 5000.");

            Assert.That(CommandLineParser.communicationTimeout.Defined, $"The flag: \"{CommandLineParser.communicationTimeout.ArgumentName}\" is NOT defined.");
            Assert.That(CommandLineParser.communicationTimeout.Value == 5000, $"The flag: \"{CommandLineParser.communicationTimeout.ArgumentName}\" is NOT 5000.");
        }

        // A Test behaves as an ordinary method
        [Test]
        public void TestEmitterArguments()
        {
            CommandLineParser.CacheArguments(overrideIsEmitter: true);

            Assert.That(CommandLineParser.emitterSpecified.Defined, $"The flag: \"{CommandLineParser.emitterSpecified.ArgumentName}\" is NOT defined.");
            Assert.That(CommandLineParser.emitterSpecified.Value, $"The flag: \"{CommandLineParser.emitterSpecified.ArgumentName}\" is NOT true.");

            Assert.That(CommandLineParser.nodeID.Defined, $"The node ID is NOT defined.");
            Assert.That(CommandLineParser.nodeID.Value == 0, $"The node ID is NOT 0.");

            Assert.That(CommandLineParser.repeaterCount.Defined, $"The repeater count is NOT defined.");
            Assert.That(CommandLineParser.repeaterCount.Value == 4, $"The repeater count is NOT 4.");

            Assert.That(CommandLineParser.multicastAddress.Defined, $"The flag: \"{CommandLineParser.multicastAddress.ArgumentName}\" is NOT defined.");
            Assert.That(CommandLineParser.multicastAddress.Value == "224.0.1.0", $"The flag: \"{CommandLineParser.multicastAddress.ArgumentName}\" is NOT 224.0.1.0.");

            Assert.That(CommandLineParser.rxPort.Defined, $"The flag: \"{CommandLineParser.rxPort.ArgumentName}\" is NOT defined.");
            Assert.That(CommandLineParser.rxPort.Value == 25691, $"The flag: \"{CommandLineParser.rxPort.ArgumentName}\" is NOT 25691.");

            Assert.That(CommandLineParser.txPort.Defined, $"The flag: \"{CommandLineParser.txPort.ArgumentName}\" is NOT defined.");
            Assert.That(CommandLineParser.txPort.Value == 25692, $"The flag: \"{CommandLineParser.txPort.ArgumentName}\" is NOT 25692.");
        }

        [Test]
        public void TestRepeaterArguments()
        {
            CommandLineParser.CacheArguments(overrideIsEmitter: false);

            Assert.That(CommandLineParser.repeaterSpecified.Defined, $"The flag: \"{CommandLineParser.repeaterSpecified.ArgumentName}\" is NOT defined.");
            Assert.That(CommandLineParser.repeaterSpecified.Value, $"The flag: \"{CommandLineParser.repeaterSpecified.ArgumentName}\" is NOT true.");

            Assert.That(CommandLineParser.nodeID.Defined, $"The node ID is NOT defined.");
            Assert.That(CommandLineParser.nodeID.Value == 1, $"The node ID is NOT 1.");

            Assert.That(CommandLineParser.multicastAddress.Defined, $"The flag: \"{CommandLineParser.multicastAddress.ArgumentName}\" is NOT defined.");
            Assert.That(CommandLineParser.multicastAddress.Value == "224.0.1.0", $"The flag: \"{CommandLineParser.multicastAddress.ArgumentName}\" is NOT 224.0.1.0.");

            Assert.That(CommandLineParser.rxPort.Defined, $"The flag: \"{CommandLineParser.rxPort.ArgumentName}\" is NOT defined.");
            Assert.That(CommandLineParser.rxPort.Value == 25692, $"The flag: \"{CommandLineParser.rxPort.ArgumentName}\" is NOT 25692.");

            Assert.That(CommandLineParser.txPort.Defined, $"The flag: \"{CommandLineParser.txPort.ArgumentName}\" is NOT defined.");
            Assert.That(CommandLineParser.txPort.Value == 25691, $"The flag: \"{CommandLineParser.txPort.ArgumentName}\" is NOT 25691.");
        }
    }
}
