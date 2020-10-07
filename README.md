# Cluster Display overview

The Unity Cluster Display solution allows multiple machines to display the same Unity Scene synchronously through display clustering (large, multi-display configurations).

## Prerequisites

If not using **Swap Barriers**:

* Unity 2020.1 or higher

When installing add **Windows Build Support (IL2CPP)**.

If using **Swap Barriers**:

* Build and install one of the Unity versions below in the [NVIDIA Quadro Sync Swap Barrer Support section](#nvidia-quadro-sync-swap-barrier-support) section.
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
| TestProjects | Contains various test projects demontrating the functionality of the packages. |

## Installation

The Cluster Display packages are currently experimental and must be installed manually. [See the documentation for installing a package from a local folder](https://docs.unity3d.com/Manual/upm-ui-local.html).

It is recommended to use [Git Submodules](https://git-scm.com/book/en/v2/Git-Tools-Submodules) for development.

### NVIDIA Quadro Sync Swap Barrier support

NVIDIA's NvAPI provides the capability to synchronize the buffer swaps of a group of DirectX swap chains when using Quadro Sync II boards. This extension also provides the capability to synchronize the buffer swaps of different swaps groups, which may reside on distributed systems on a network using a swap barrier. It’s essential to coordinate the back buffer swap between nodes, so it can stay perfectly synchronized (Frame Lock + Genlock) for a large number of displays.

Swap Barrier support is still experimental and relies on a custom Unity build. There are two separate builds depending on the desired graphics API:

* Unity [**Direct3D11**](https://ono.unity3d.com/unity/unity/pull-request/113317/_/feat/quadro-sync-d3d11) Build Ono PR
* Unity [**Direct3D12**](https://ono.unity3d.com/unity/unity/pull-request/113690/_/graphics/expose-plugin-callbacks-swapchain-d3d12) Build Ono PR

To install the Quadro Sync feature, generate 2 DLLs by running `build.cmd`. They must be generated in the folder: `source/com.unity.cluster-display/Runtime/Plugins/x86-64`.

See the [GfxPluginQuadroSyncCallbacks.cs](source/com.unity.cluster-display/Runtime/QuadroSync/GfxPluginQuadroSyncCallbacks.cs) script for an example of usage.
