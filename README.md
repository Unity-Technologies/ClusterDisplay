# Cluster Display overview

The Unity Cluster Display solution allows multiple machines to display the same Unity Scene synchronously through display clustering (large, multi-display configurations).

## Packages

Two separate packages are available in this shell repository.

| Folder | Description |
|---------|----------------------|
| [souce/com.unity.cluster-display](source/com.unity.cluster-display/Documentation~/index.md) | This package is **always required** for any Cluster Display setup. |
| [source/com.unity.cluster-display.graphics](source/com.unity.cluster-display.graphics/Documentation~/index.md) | Use this additional package only if your Unity project is using High Definition Render Pipeline (**HDRP**). |
| External | Contains the files to build the NvAPI DLL. |
| GfxPluginQuadroSyncD3D11 |  |
| GfxPluginQuadroSyncD3D12 |  |
| TestProjects | Contains various test projects demontrating the functionality of the packages. |

## Installation

The Cluster Display packages are currently experimental. You must install them manually: reference them in the user project's `Packages/manifest.json` file using a relative path.

For example:
<br />`com.unity.cluster-display.cluster-display": "file:../../Packages/com.unity.cluster-display.cluster-display`

You can use [Git Submodules](https://git-scm.com/book/en/v2/Git-Tools-Submodules) to download the packages to more easily update.
