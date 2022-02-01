# About Unity Cluster Display

The Unity Cluster Display solution allows multiple machines to display the same Unity Scene synchronously through display clustering. This feature enables you, for example, to deploy your Unity project to large, multi-display configurations.

## Multiple displays

In practice, you could use a single machine to render to multiple displays and/or high resolution displays (4K+), but the machine's computational power might present a limit to this approach. With Unity Cluster Display, you can scale up to an arbitrary number of machines, therefore an arbitrary number of displays: Unity Cluster Display currently supports up to 64 nodes. However, if you need to increase this number, it is technically possible.

Note that Unity Cluster Display does not prevent the use of multiple displays per machine. The total number of pixels a machine can render to depends both on its hardware capabilities and the user project's complexity.

## Clustering and synchronization

A Cluster Display setup typically consists of **one master node** and **several client nodes**:

-   A single client node consists of a workstation and a display output.

    -   Each workstation runs a copy of your Unity application with Cluster Display enabled.

    -   All the nodes run the same interactive content in lockstep, but each one only renders a subsection of the total display surface.

-   The master is responsible for synchronizing the state for all client nodes.

    -   The client nodes connect to the master node via a **wired** Local Area Network.

    -   The master node does not technically need to be connected to a display, unless you configure it to also take the role of a client node.

**Recommended setup:** To optimize the use of your hardware, you can set up one of the nodes to take both the roles of master and client. This does not affect the cluster functionality.

![](images/cluster-display-setup-example.png)
<br />*Cluster Display setup example for 2x2 matrix display: 4 nodes, one of which is also the master.*

## Documentation overview

Before you start, take a look at the [package technical details and requirements](#package-technical-details).

To have a Unity project experience running through Cluster Display, you must:

1.  [Prepare your Unity project](project-setup.md) to be able to use it in the context of Cluster Display.

2.  [Set up your hardware](hardware-setup.md) in a cluster configuration according to Cluster Display requirements.

3.  [Start up and manage your cluster](cluster-operation.md) to play your Unity project on your multi-display setup.

You can optionally look at the [Reference](reference.md) section of this documentation to get:
-   More information about cluster synchronization and communication.
-   The description of all Unity Editor components involved in Cluster Display.
-   The list of tested hardware.

## Disclaimer

### Licensing

Making cluster-enabled builds with Unity requires a special license. [Contact a Unity sales representative](https://create.unity3d.com/unity-sales) for more information.

### Experimental packages

The packages required to set up Unity Cluster Display are currently available as experimental packages, so they are not ready for production use. The features and documentation in these packages will change before they are verified for release.

## Package technical details

### Requirements

#### Unity Editor

This version of Unity Cluster Display is compatible with the following versions of the Unity Editor:

- Unity 2020.1 or higher (if not using Swap Barriers)

When installing add **Windows Build Support (IL2CPP)**.

`Note: to use NVIDIA Swap Barriers, a custom build of the Unity Editor is required. There are also some additional requirements detailed below in the Swap Barriers section.`

#### Operating system

Unity Cluster Display is currently only compatible with:

-   Windows 10

#### Hardware

To set up your Unity Cluster Display solution, you must ensure that **each node** of your cluster minimally has the following hardware configuration:

-   [NVIDIA Quadro Sync II](https://www.nvidia.com/en-us/design-visualization/solutions/quadro-sync/) card.

-   NVIDIA Quadro-compatible graphics card physically connected to the Sync II card.

-   Enterprise-grade network switch (recommended).

To get more context, you can also look at the [hardware setup tested by Unity](reference.md#tested-hardware).

### Known limitations

The Unity Cluster Display solution currently has some feature limitations due to either of the following:

-   Reliance on screen-space or viewport data (e.g. Bloom post processing).

-   Dependence on data that cannot be reliably or efficiently synchronized over the network (e.g. physics).

As such:

-   There is currently no support for synchronizing events from the latest Input System package.

-   HDRP auto-exposure is not supported

-   Many HDRP post-processing effects are not supported and require custom support.

-   Physics is not fully supported as simulations are not deterministic across machines.

-   UGUI, IMGUI and UI Elements are not supported for display or interaction across displays.

-   Video Player playback is not supported across displays.

### Installation

This package is currently experimental and not publicly available through Unity's Package Manager. [See the documentation for installing a package from a local folder](https://docs.unity3d.com/Manual/upm-ui-local.html).

### Swap Barriers

Swap Barriers provide the the ability to acheive Frame Lock + Genlock across nodes and displays when using NVIDIA Quadro Sync II boards alongisde a Quadro GPU. There are several prerequisites for leverage Swap Barriers.

- Custom Unity Editor build
- Only supported on DirectX 11 or DirectX 12.
- Requires one or more [NVIDIA Quadro GPU](https://www.nvidia.com/en-us/design-visualization/quadro/)s.
- Requires one or more [NVIDIA Quadro Sync II](https://www.nvidia.com/en-us/design-visualization/solutions/quadro-sync/) boards.
