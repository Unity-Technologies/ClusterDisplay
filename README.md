# Cluster Display overview

The Unity Cluster Display solution allows multiple machines to display the same Unity Scene synchronously through display clustering (large, multi-display configurations).

## Prerequisites

* Unity 2020.1 or higher.
* Unity 2020.2 or higher if using DX12 and Swap Barriers.

When installing add **Windows Build Support (IL2CPP)**.

If using **Swap Barriers**:

* Only supported on Windows Vista and higher.
* Only supported on DirectX 11 or DirectX 12.
* Requires an [NVIDIA Quadro GPU](https://www.nvidia.com/en-us/design-visualization/quadro/).
* Requires one or more [NVIDIA Quadro Sync II](https://www.nvidia.com/en-us/design-visualization/solutions/quadro-sync/) boards.

### Hardware
Required hardware is detailed in the [Cluster Display documentation](source/com.unity.cluster-display/Documentation~/index.md) in the 

## Contents

This repository contains in-development packages, tests and test projects, as well as files for building the NvAPI plugin for Swap Barrier support.

| Folder | Description |
|---------|----------------------|
| [souce/com.unity.cluster-display](source/com.unity.cluster-display/Documentation~/index.md) | This package is **always required** for any Cluster Display setup. |
| [source/com.unity.cluster-display.graphics](source/com.unity.cluster-display.graphics/Documentation~/index.md) | Use this additional package only if your Unity project is using High Definition Render Pipeline (**HDRP**). |
| External | Contains the NvAPI files used to build the Quadro Sync DLLs. |
| GfxPluginQuadroSyncD3D11 | Project containing the files to build the Quadro Sync D3D11 DLL. |
| GfxPluginQuadroSyncD3D12 | Project containing the files to build the Quadro Sync D3D12 DLL. |
| TestProjects/ClusterRenderTest | Test project built based on the legacy renderer used to test synchronization. |
| [TestProjects/GraphicsDemoProject](TestProjects/GraphicsDemoProject/README.md) | Test project with a simple demo scene to demonstrate some HDRP features in a cluster environment. |
| [TestProjects/GraphicsTestProject](TestProjects/GraphicsTestProject/README.md) | Test project to render multiple frames with different settings and compare the results against each other. |
| TestProjects/QuadroSyncTest | Test project to validate NVIDIA Swap Barrier support. |

## Installation

The Cluster Display packages are currently experimental and must be installed manually. [See the documentation for installing a package from a local folder](https://docs.unity3d.com/Manual/upm-ui-local.html).

It is recommended to use [Git Submodules](https://git-scm.com/book/en/v2/Git-Tools-Submodules) for development.

### NVIDIA Quadro Sync Swap Barrier support

NVIDIA's NvAPI provides the capability to synchronize the buffer swaps of a group of DirectX swap chains when using Quadro Sync II boards. This extension also provides the capability to synchronize the buffer swaps of different swaps groups, which may reside on distributed systems on a network using a swap barrier. It’s essential to coordinate the back buffer swap between nodes, so it can stay perfectly synchronized (Frame Lock + Genlock) for a large number of displays.

See [GfxPluginQuadroSyncCallbacks.cs](source/com.unity.cluster-display/Runtime/QuadroSync/GfxPluginQuadroSyncCallbacks.cs) for example usage.
