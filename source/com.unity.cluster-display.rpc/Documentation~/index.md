# Cluster Display RPC support (package concepts)

This package is used in conjunction with the main `cluster-display` package to support Remote Procedural Calls (RPCs) in your projects.

See the [Cluster Display documentation](../../../source/com.unity.cluster-display/Documentation~/index.md) for general information, installation, and use of Cluster Display.

# Network Events
Cluster Display has a multicast UDP general networking library with the following features:
- Remote Procedural Call (RPC) support.
  - Primitive method parameters such as float, int, byte, etc.
  - Struct or valuetype method parameters.
  - Array method parameters.
  - string method parameters.

# Remote Procedure Calls (RPCs)
[RPCs](https://en.wikipedia.org/wiki/Remote_procedure_call) are a common networking pattern for propagating network events and for Cluster Display we use them extensively. In Cluster Display's networking implementation you can flag a method to be an RPC using two different approaches:
- Cluster Display Inspector UI **(Only available in com.unity.cluster-dislay.helpers)**
- **[ClusterRPC]** C# attribute.

# IL Post Processing
In order to intercept method an properties calls for propagation to repeater nodes, we inject IL into your compiled assembly after your assembly is compiled. Generally the following is injected:
- Intercept Instructions:
  - These instructions intercept method & property calls, interprets their arguments into bytes and propagates the call and arguments to repeater nodes.
- Execution Instructions
  - Cluster display will dynamically generate classes that converts the propagated arguments from bytes into your method/property arguments, then executes them.

It's important to note that some assemblies **are** IL post processable, and some are **not**. Any assemblies that **do not** compile into **`{Project Path}/Library/ScriptAssemblies`** are **not** IL post processable. Such as:
- UnityEngine.CoreModule
- Unity.Timeline
- Any DLLs in the Unity installation folder.

Only assemblies in **`{Project Path}/Library/ScriptAssemblies`** folder are IL post processable. Therefore, if you want to propgate events associated with a non IL post processable assembly, you will need to use [wrappers](#Wrappers).

## Debugging IL Post Processing
IL post processing occurs in a completely separate process. IL post processing logging for cluster display is located here:

**{Project Path}\Temp\ClusterDisplay\Logs\ClusterDisplay-ILPostProcessingLog.txt**

### Inspecting Assemblies
One way to verify whether an assembly was properly IL post processed is to inspect it using [ILSpy](https://github.com/icsharpcode/ILSpy). With this tool, you can open compiled C# DLLs and inspect the IL. There are some key signatures to identify whether an assembly was properly post processed:
* The assembly contains the **Unity.ClusterDisplay.Generated** namespace.
    * The **Unity.ClusterDisplay.Generated** namespace contains a single RPCIL class with the following methods:
      * **ExecuteQueuedRPC**
      * **OnTryCallInstance**
      * **OnTryStaticCallInstance**
    * Each of the methods should contain IL code that switches on a hash and executes the target RPC directly.
* Each method designated as an RPC should have some injected IL code that intercepts the method call and arguments.

Here is an example of a method flagged as an RPC:
![Execution Stage Timings](./images/ilspy-0.png)

Here is an example of the generated RPCIL class:
![Execution Stage Timings](./images/ilspy-1.png)

# **[ClusterRPC]** Attribute
The ClusterRPC attribute is a handy way of flagging a method as an RPC, and you can declaring it above or before your method declaration:
```
[ClusterRPC]
public void TestMethod () {}
// OR
[ClusterRPC] public void TestMethod () {}
```
This attribute also provides several optional but handy arguments:
```
public ClusterRPC (RPCExecutionStage rpcExecutionStage = RPCExecutionStage.Automatic, int rpcId = -1, string formarlySerializedAs = "")
```

## **RPCExecutionStage**
By default, cluster display will automatically determine the order which the RPC event was called within the [order of events](https://docs.unity3d.com/Manual/ExecutionOrder.html). You can override the order of **when** this RPC gets executed by the repeater nodes within the frame by using this optional argument. Here is a diagram of the execution stage timings:

![Execution Stage Timings](./images/execution-stage-graph-0.png)

When an RPC has it's execution stage set to **RPCExecutionStage.Automatic**, cluster display will know when the RPC was invoked. This is determined through **RPCExecutor.cs** which inserts decorates several callbacks throughout the player loop to:
1. The current state of the frame within the player loop.
2. To execute queued RPCs on the repeater nodes throughout the player loop.

### Why RPCExecutionStage?
Keeping deterministic behaviour between the nodes is challenging, and often even simple differences in execution timing can cause logical forks between the emitter and repeater nodes. Consider the following issues:
- I want a network event to manipulate physics objects. Therefore, I need to make sure I stage the received data before FixedUpdate so that FixedUpdate can use the data.
- I have a GUI event invoking a network event. Therefore, I need to make sure that the network event executes after LateUpdate on repeater nodes which is where those types of events are invoked.

## **rpcId**
Cluster display usually automatically determines the IDs of RPCs for you. However, this option allows you to override that ID for whatever reason.

## **formarlySerializedAs**
When you rename a method cluster display will not be able to deserialize the method's signature and setting this string to what the method was previously named as will allow cluster display to deserialize the renamed method.

# RPC Method Parameters
Cluster Display's networking library will automatically determine if your target method can be used as an RPC. If you attempt to flag a incompatible method as an RPC. Cluster Display will log a error explaining the problem.

## Supported Arguments
### Primitive Method Arguments
All [C# primitive types](!https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/built-in-types) can be used as RPC parameters.
```
[ClusterRPC]
public void TestMethod (float valueA, int valueB) {}
```
### Boolean Method Arguments
C# `Boolean` types are supported. However, in managed memory they are considered as 1 byte. Whereas when they are marshalled by `Marshal` they are implicitly converted into 4 bytes. Therefore `bool` arguments are communicated as 4 bytes to to the repater nodes. If you really need `bool` arguments to be communicated as 1 bytes you can wrap it in a struct and setup the struct in the following way: [Struct Boolean Field Members](#struct-boolean-field-members)

### Struct & ValueType Method Arguments
[C# structs](!https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/struct) can be used as RPC parameters as long as **all** of the struct members and nested members are primitive types:
```
[StructLayout(LayoutKind.Explicit, Size = 8)]
public struct NestedContainerType
{
    [FieldOffset(0)] public double valueC;
}

[StructLayout(LayoutKind.Explicit, Size = 28)]
public struct ContainerType
{
    [FieldOffset(0)] public float valueA;
    [FieldOffset(4)] public int valueB

    // Nested struct instances are supported.
    [FieldOffset(8)] public Vector3 vector; // Common Unity structs are supported.
    [FieldOffset(20)] public NestedContainerType nested; // Custom structs are supported.
}

[ClusterRPC]
public void TestMethod (ContainerType container, Vector3 vector) {}
```
**You MUST declare the struct layout if your using a custom struct as a RPC argument.** Otherwise Cluster Display may interpret the struct incorrectly when converting the structure into bytes.

You can use either [`[StructLayout(LayoutKind.Sequential)]` or `[StructLayout(LayoutKind.Explicit)]`](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.structlayoutattribute?view=net-5.0) to define the struct's byte layout. With `LayoutKind.Sequential`. The struct's byte layout will be determined by the struct's field definition order. Whereas `LayoutKind.Explicit` you will need to define the exact byte offset of each field using the `[FieldOffset]` attribute.

### Struct Boolean Field Members
If you have a struct with a `bool` field as a RPC argument, Cluster Display will convert it to 4 bytes to communicate this field to repeater nodes. This is due to the following:

C# `Boolean`s are only 1 byte, however C#'s Marshal implicitly converts C# booleans to Window SDK `BOOL` which typdefs as an unmanaged int of 4 bytes. If you want to explicitly communicate a `bool` as 1 byte, you can use the [`[MarshalAs(UnmanagedType.I1)]`](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.marshalasattribute?view=net-5.0) field attribute just before the `bool` field definition in the struct like so:
```
[StructLayout(LayoutKind.Sequential)]
public struct Test
{
    public int number;
    [MarshalAs(UnmanagedType.I1)]
    public bool testBool;
}
```

### Array Method Arguments
C# arrays are supported as long as the element type is a primitive or struct type:

```
[StructLayout(LayoutKind.Explicit, Size = 8)]
public struct NestedContainerType
{
    public double valueC;
}

[StructLayout(LayoutKind.Explicit, Size = 28)]
public struct ContainerType
{
    [FieldOffset(0)] public float valueA;
    [FieldOffset(4)] public int valueB

    // Nested struct instances are supported.
    [FieldOffset(8)] public Vector3 vector; // Common Unity structs are supported.
    [FieldOffset(20)] public NestedContainerType nested; // Custom structs are supported.
}

[ClusterRPC]
public void TestMethod (ContainerType[] containers, float[] floats) {}
```

### String Method Arguments
String parameters are supported.
```
[ClusterRPC]
public void TestMethod (string messageStr) {}
```
