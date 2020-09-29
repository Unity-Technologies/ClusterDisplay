# About Unity Cluster Display

The Unity Cluster Display solution allows multiple machines to run the same Unity Scene synchronously through display clustering. This feature enables you, for example, to deploy your Unity project to large, multi-display configurations.

## Multiple displays

In practice, you could use a single machine to feed multiple displays, but this machine's computational power might represent a limit to this approach. With Unity Cluster Display, you can scale up to an arbitrary number of machines, therefore an arbitrary number of displays: Unity Cluster Display currently supports up to 64 nodes. However, if you need to increase this number, this is technically possible.

Note that Unity Cluster Display does not prevent the use of multiple displays per machine. The number of displays a machine can feed depends both on its hardware capabilities and the user project's complexity.

## Clustering and synchronization

A Cluster Display setup typically consists of **one master node** and **several client nodes**.

![](images/cluster-display-setup-example.png)
<br />*Cluster Display setup example for 2x2 matrix display: 4 nodes, one of which is also the master.*

### Details

-   A single client node consists of a workstation and a display output.

    -   Each workstation runs a copy of your Unity application with Cluster Display enabled.

    -   All the nodes run the same interactive content in lockstep, but each one only renders a subsection of the total display surface.

-   The master is responsible of synchronizing all nodes.

    -   The client nodes connect to the master node via a **wired** Local Area Network.

    -   You can set up one of the client nodes to take the role of the master to optimize the use of your hardware without affecting the cluster functionality.

## Documentation overview

**Note:** This documentation provides all Cluster Display setup information for **Unity projects that don’t use High Definition Render Pipeline (HDRP)**. For more information about specifically using Cluster Display with HDRP, see the [Cluster Display Graphics](https://github.com/Unity-Technologies/ClusterDisplay/blob/dev/source/com.unity.cluster-display.graphics/Documentation~/index.md) package documentation.

Before you start, take a look at the [package technical details and requirements](#package-technical-details).

To have a Unity project experience running through Cluster Display, you must:

1.  [Prepare your Unity project](project-setup.md) to be able to use it in the context of Cluster Display.

2.  [Set up your hardware](hardware-setup.md) in a cluster configuration according to Cluster Display requirements.

3.  [Start up and manage your cluster](cluster-operation.md) to play your Unity project on your multi-display setup.

You can optionally look at the [Reference](reference.md) section of this documentation to get:
-   More information about cluster synchronization and communication.
-   The description of all Unity Editor components involved in Cluster Display.
-   The list of hardware tested by Unity for quality assurance and support purposes.

## Disclaimer

### Licensing

Making cluster-enabled builds with Unity requires a special license. Contact a Unity sales representative for more information.

### Experimental packages

The packages required to set up Unity Cluster Display are currently available as experimental packages, so they are not ready for production use. The features and documentation in these packages *will* change before they are verified for release.

## Package technical details

### Requirements

#### Unity Editor

This version of Unity Cluster Display is compatible with the following versions of the Unity Editor:

-   2020.1 and later (recommended)

#### Packages

Two separate packages are available in the [Cluster Display](https://github.com/Unity-Technologies/ClusterDisplay) shell repository on GitHub, under the source folder:

-   The [Cluster Display](https://github.com/Unity-Technologies/ClusterDisplay/tree/stable/source/com.unity.cluster-display) package is **always required**.

-   If you are using High Definition Render Pipeline (**HDRP**) in your Unity project, you must use the additional [Cluster Display Graphics](https://github.com/Unity-Technologies/ClusterDisplay/tree/stable/source/com.unity.cluster-display.graphics) package.

#### Operating system

Unity Cluster Display is currently only compatible with:

-   Windows 10

#### Hardware

To set up your Unity Cluster Display solution, you must ensure that **each node** of your cluster minimally has the following hardware configuration:

-   NVIDIA Quadro Sync II card.

-   NVIDIA Quadro-compatible graphic card physically connected to the Sync II card.

-   Enterprise-grade network switch.

To get more context, you can also look at the [hardware setup tested by Unity](reference.md#tested-hardware).

### Known limitations

The Unity Cluster Display solution currently has some feature limitations due to either of the following:

-   Reliance on screen-space or viewport data (e.g. Bloom post processing).

-   Dependence on data that cannot be reliably or efficiently synchronized over the network (e.g. physics).

As such:

-   There is currently no support for synchronizing events from the latest Input System package.

-   HDRP auto-exposure is not supported

-   Many HDRP post-processing effects are not supported and require special support. See the [Cluster Display Graphics package documentation](https://github.com/Unity-Technologies/com.unity.cluster-display.graphics/blob/develop/Documentation~/index.md) for more details.

-   Physics is not fully supported as simulations are not deterministic across machines.

-   UGUI, IMGUI and UI Elements are not supported for display or interaction across displays.

-   Video Player playback is not supported across displays.

### Installation

To set up and build a Unity project in order to use it for Cluster Display, you must install some [specific packages](#packages) according to your needs. These packages are currently [experimental](#experimental-packages), and as such, are not publicly available through Unity's Package Manager. Therefore, you must get them from GitHub and install them manually.

To do so, you can reference them in the user project's Packages/manifest.json file using a relative path such as, for example:

"com.unity.cluster-display.cluster-display": "file:../../Packages/com.unity.cluster-display.cluster-display"

Unity suggests using [Git Submodules](https://git-scm.com/book/en/v2/Git-Tools-Submodules) to download the package to more easily update.
