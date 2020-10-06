# Cluster Display overview

The Unity Cluster Display solution allows multiple machines to display the same Unity Scene synchronously through display clustering (large, multi-display configurations).

## Packages

Two separate packages are available in this shell repository.

| Folder | Description |
|---------|----------------------|
| [souce/com.unity.cluster-display](source/com.unity.cluster-display/Documentation~/index.md) | This package is **always required** for any Cluster Display setup. |
| [source/com.unity.cluster-display.graphics](source/com.unity.cluster-display.graphics/Documentation~/index.md) | Use this additional package only if your Unity project is using High Definition Render Pipeline (**HDRP**). |
| External | Contains the NvAPI files used to build the Quadro Sync DLLs. |
| GfxPluginQuadroSyncD3D11 | Project containing the files to build the Quadro Sync D3D11 DLL. |
| GfxPluginQuadroSyncD3D12 | Project containing the files to build the Quadro Sync D3D12 DLL. |
| TestProjects | Contains various test projects demontrating the functionality of the packages. |

## Installation

The Cluster Display packages are currently experimental. You must install them manually: reference them in the user project's `Packages/manifest.json` file using a relative path.

For example:
<br />`com.unity.cluster-display.cluster-display": "file:../../Packages/com.unity.cluster-display.cluster-display`

You can use [Git Submodules](https://git-scm.com/book/en/v2/Git-Tools-Submodules) to download the packages to more easily update.

## Quadro Sync

The Quadro Sync feature is still experimental. At the moment, you must build a custom Unity version to have the changes. 
Depending if you want to use Quadro Sync for [**Direct3D11**](https://ono.unity3d.com/unity/unity/pull-request/113317/_/feat/quadro-sync-d3d11) or [**Direct3D12**](https://ono.unity3d.com/unity/unity/pull-request/113690/_/graphics/expose-plugin-callbacks-swapchain-d3d12), you must build the custom Unity on one of the 2 branches.

In order to load the Quadro Sync feature, you must generate 2 DLLs by running the script `build.cmd`. They should be generated in the folder: `source/com.unity.cluster-display/Runtime/Plugins/x86-64`.

To use the Quadro Sync features, you can take the script [GfxPluginQuadroSyncCallbacks](source/com.unity.cluster-display/Runtime/QuadroSync/GfxPluginQuadroSyncCallbacks.cs) as a reference.