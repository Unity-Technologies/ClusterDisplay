# How Do I?

## How do I setup audio reactive experiences?
There are many ways to approach this

## How do I setup OSC?
There are a multitude of OSC libraries & packages out there to choose from that work with Unity. Here are some recommendations:

- [Keijiro's OSCJack](https://github.com/keijiro/OscJack)
- [Keijiro's Visual Scripting OSCJack Extensions](https://github.com/keijiro/OscJackVS)
- [Unity OSC](https://thomasfredericks.github.io/UnityOSC/) - This library is pretty straight forward, the OSC implementation is written in a single file: [OSC.cs](https://github.com/thomasfredericks/UnityOSC/blob/master/Assets/OSC/OSC.cs).
- [extOSC](https://github.com/Iam1337/extOSC)
- [OscCore](https://github.com/stella3d/OscCore)
- [Rug.OSC](https://bitbucket.org/rugcode/rug.osc/src/master/) - This is a fully featured C# library. However it does not have components for Unity. However, you can download the [nuget](https://www.nuget.org/packages/Rug.Osc/), extract the DLL and copy the DLL into your project.

Since the emitter already has a connection to repeater nodes you will want to send and receive OSC events between the emitter node and your external source, then setup up some RPCs to propagate what you need to repeater nodes.

### MaxMSP
TODO

### Ableton Live
TODO

### TouchOSC
TODO