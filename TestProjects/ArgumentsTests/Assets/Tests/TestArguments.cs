using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.ClusterDisplay;

public class TestArguments
{
    [Test]
    public void TestCommonArguments()
    {
        CommandLineParser.CacheArguments();

        Debug.Assert(CommandLineParser.debugFlag.Defined, $"The flag: \"{CommandLineParser.debugFlag.ArgumentName}\" is NOT defined.");
        Debug.Assert(CommandLineParser.debugFlag.Value, $"The flag: \"{CommandLineParser.debugFlag.ArgumentName}\" is NOT true.");

        Debug.Assert(CommandLineParser.replaceHeadlessEmitter.Defined, $"The flag: \"{CommandLineParser.replaceHeadlessEmitter.ArgumentName}\" is NOT defined.");
        Debug.Assert(CommandLineParser.replaceHeadlessEmitter.Value, $"The flag: \"{CommandLineParser.replaceHeadlessEmitter.ArgumentName}\" is NOT true.");

        Debug.Assert(CommandLineParser.delayRepeaters.Defined, $"The flag: \"{CommandLineParser.delayRepeaters.ArgumentName}\" is NOT defined.");
        Debug.Assert(CommandLineParser.delayRepeaters.Value, $"The flag: \"{CommandLineParser.delayRepeaters.ArgumentName}\" is NOT true.");

        Debug.Assert(CommandLineParser.gridSize.Defined, $"The flag: \"{CommandLineParser.gridSize.ArgumentName}\" is NOT defined.");
        Debug.Assert(CommandLineParser.gridSize.Value == new Vector2Int(2, 2), $"The flag: \"{CommandLineParser.gridSize.ArgumentName}\" is NOT 2x2.");

        Debug.Assert(CommandLineParser.bezel.Defined, $"The flag: \"{CommandLineParser.bezel.ArgumentName}\" is NOT defined.");
        Debug.Assert(CommandLineParser.bezel.Value == new Vector2Int(4, 4), $"The flag: \"{CommandLineParser.bezel.ArgumentName}\" is NOT 2x2.");

        Debug.Assert(CommandLineParser.physicalScreenSize.Defined, $"The flag: \"{CommandLineParser.physicalScreenSize.ArgumentName}\" is NOT defined.");
        Debug.Assert(CommandLineParser.physicalScreenSize.Value == new Vector2Int(1920, 1080), $"The flag: \"{CommandLineParser.physicalScreenSize.ArgumentName}\" is NOT 1920x1080.");

        Debug.Assert(CommandLineParser.targetFps.Defined, $"The flag: \"{CommandLineParser.targetFps.ArgumentName}\" is NOT defined.");
        Debug.Assert(CommandLineParser.targetFps.Value == 60, $"The flag: \"{CommandLineParser.targetFps.ArgumentName}\" is NOT 60.");

        Debug.Assert(CommandLineParser.overscan.Defined, $"The flag: \"{CommandLineParser.overscan.ArgumentName}\" is NOT defined.");
        Debug.Assert(CommandLineParser.overscan.Value == 128, $"The flag: \"{CommandLineParser.overscan.ArgumentName}\" is NOT 64.");

        Debug.Assert(CommandLineParser.quadroSyncInitDelay.Defined, $"The flag: \"{CommandLineParser.quadroSyncInitDelay.ArgumentName}\" is NOT defined.");
        Debug.Assert(CommandLineParser.quadroSyncInitDelay.Value == 66, $"The flag: \"{CommandLineParser.quadroSyncInitDelay.ArgumentName}\" is NOT 66.");

        Debug.Assert(CommandLineParser.linesThickness.Defined, $"The flag: \"{CommandLineParser.linesThickness.ArgumentName}\" is NOT defined.");
        Debug.Assert(CommandLineParser.linesThickness.Value == 1.2345f, $"The flag: \"{CommandLineParser.linesThickness.ArgumentName}\" is NOT 1.2345.");

        Debug.Assert(CommandLineParser.linesThickness.Defined, $"The flag: \"{CommandLineParser.linesThickness.ArgumentName}\" is NOT defined.");
        Debug.Assert(CommandLineParser.linesThickness.Value == 1.2345f, $"The flag: \"{CommandLineParser.linesThickness.ArgumentName}\" is NOT 1.2345.");

        Debug.Assert(CommandLineParser.linesScale.Defined, $"The flag: \"{CommandLineParser.linesScale.ArgumentName}\" is NOT defined.");
        Debug.Assert(CommandLineParser.linesScale.Value == 1.2345f, $"The flag: \"{CommandLineParser.linesScale.ArgumentName}\" is NOT 1.2345.");

        Debug.Assert(CommandLineParser.linesShiftSpeed.Defined, $"The flag: \"{CommandLineParser.linesShiftSpeed.ArgumentName}\" is NOT defined.");
        Debug.Assert(CommandLineParser.linesShiftSpeed.Value == 1.2345f, $"The flag: \"{CommandLineParser.linesShiftSpeed.ArgumentName}\" is NOT 1.2345.");

        Debug.Assert(CommandLineParser.linesAngle.Defined, $"The flag: \"{CommandLineParser.linesAngle.ArgumentName}\" is NOT defined.");
        Debug.Assert(CommandLineParser.linesAngle.Value == 1.2345f, $"The flag: \"{CommandLineParser.linesAngle.ArgumentName}\" is NOT 1.2345.");

        Debug.Assert(CommandLineParser.linesRotationSpeed.Defined, $"The flag: \"{CommandLineParser.linesRotationSpeed.ArgumentName}\" is NOT defined.");
        Debug.Assert(CommandLineParser.linesRotationSpeed.Value == 1.2345f, $"The flag: \"{CommandLineParser.linesRotationSpeed.ArgumentName}\" is NOT 1.2345.");

        Debug.Assert(CommandLineParser.adapterName.Defined, $"The flag: \"{CommandLineParser.adapterName.ArgumentName}\" is NOT defined.");
        Debug.Assert(CommandLineParser.adapterName.Value == "Ethernet", $"The flag: \"{CommandLineParser.adapterName.ArgumentName}\" is NOT Ethernet.");

        Debug.Assert(CommandLineParser.handshakeTimeout.Defined, $"The flag: \"{CommandLineParser.handshakeTimeout.ArgumentName}\" is NOT defined.");
        Debug.Assert(CommandLineParser.handshakeTimeout.Value == 5000, $"The flag: \"{CommandLineParser.handshakeTimeout.ArgumentName}\" is NOT 5000.");

        Debug.Assert(CommandLineParser.communicationTimeout.Defined, $"The flag: \"{CommandLineParser.communicationTimeout.ArgumentName}\" is NOT defined.");
        Debug.Assert(CommandLineParser.communicationTimeout.Value == 5000, $"The flag: \"{CommandLineParser.communicationTimeout.ArgumentName}\" is NOT 5000.");
    }

    // A Test behaves as an ordinary method
    [Test]
    public void TestEmitterArguments()
    {
        CommandLineParser.CacheArguments(overrideIsEmitter: true);

        Debug.Assert(CommandLineParser.emitterSpecified.Defined, $"The flag: \"{CommandLineParser.emitterSpecified.ArgumentName}\" is NOT defined.");
        Debug.Assert(CommandLineParser.emitterSpecified.Value, $"The flag: \"{CommandLineParser.emitterSpecified.ArgumentName}\" is NOT true.");

        Debug.Assert(CommandLineParser.nodeID.Defined, $"The node ID is NOT defined.");
        Debug.Assert(CommandLineParser.nodeID.Value == 0, $"The node ID is NOT 0.");

        Debug.Assert(CommandLineParser.repeaterCount.Defined, $"The repeater count is NOT defined.");
        Debug.Assert(CommandLineParser.repeaterCount.Value == 4, $"The repeater count is NOT 4.");

        Debug.Assert(CommandLineParser.multicastAddress.Defined, $"The flag: \"{CommandLineParser.multicastAddress.ArgumentName}\" is NOT defined.");
        Debug.Assert(CommandLineParser.multicastAddress.Value == "224.0.1.0", $"The flag: \"{CommandLineParser.multicastAddress.ArgumentName}\" is NOT 224.0.1.0.");

        Debug.Assert(CommandLineParser.rxPort.Defined, $"The flag: \"{CommandLineParser.rxPort.ArgumentName}\" is NOT defined.");
        Debug.Assert(CommandLineParser.rxPort.Value == 25691, $"The flag: \"{CommandLineParser.rxPort.ArgumentName}\" is NOT 25691.");

        Debug.Assert(CommandLineParser.txPort.Defined, $"The flag: \"{CommandLineParser.txPort.ArgumentName}\" is NOT defined.");
        Debug.Assert(CommandLineParser.txPort.Value == 25692, $"The flag: \"{CommandLineParser.txPort.ArgumentName}\" is NOT 25692.");
    }

    [Test]
    public void TestRepeaterArguments()
    {
        CommandLineParser.CacheArguments(overrideIsEmitter: false);

        Debug.Assert(CommandLineParser.repeaterSpecified.Defined, $"The flag: \"{CommandLineParser.repeaterSpecified.ArgumentName}\" is NOT defined.");
        Debug.Assert(CommandLineParser.repeaterSpecified.Value, $"The flag: \"{CommandLineParser.repeaterSpecified.ArgumentName}\" is NOT true.");

        Debug.Assert(CommandLineParser.nodeID.Defined, $"The node ID is NOT defined.");
        Debug.Assert(CommandLineParser.nodeID.Value == 1, $"The node ID is NOT 1.");

        Debug.Assert(CommandLineParser.multicastAddress.Defined, $"The flag: \"{CommandLineParser.multicastAddress.ArgumentName}\" is NOT defined.");
        Debug.Assert(CommandLineParser.multicastAddress.Value == "224.0.1.0", $"The flag: \"{CommandLineParser.multicastAddress.ArgumentName}\" is NOT 224.0.1.0.");

        Debug.Assert(CommandLineParser.rxPort.Defined, $"The flag: \"{CommandLineParser.rxPort.ArgumentName}\" is NOT defined.");
        Debug.Assert(CommandLineParser.rxPort.Value == 25692, $"The flag: \"{CommandLineParser.rxPort.ArgumentName}\" is NOT 25692.");

        Debug.Assert(CommandLineParser.txPort.Defined, $"The flag: \"{CommandLineParser.txPort.ArgumentName}\" is NOT defined.");
        Debug.Assert(CommandLineParser.txPort.Value == 25691, $"The flag: \"{CommandLineParser.txPort.ArgumentName}\" is NOT 25691.");
    }
}
