# Cluster Display overview

The Unity Cluster Display solution allows multiple machines to display the same Unity Scene synchronously through display clustering (large, multi-display configurations).

## Packages

Two separate packages are available in this shell repository.

| Package/documentation | Description |
|---------|----------------------|
| [Cluster Display](https://github.com/Unity-Technologies/ClusterDisplay/tree/stable/source/com.unity.cluster-display) | This package is **always required** for any Cluster Display setup. |
| [Cluster Display Graphics](https://github.com/Unity-Technologies/ClusterDisplay/tree/stable/source/com.unity.cluster-display.graphics) | Use this additional package only if your Unity project is using High Definition Render Pipeline (**HDRP**). |

## Installation

The Cluster Display packages are currently experimental. You must install them manually: reference them in the user project's `Packages/manifest.json` file using a relative path.

For example:
<br />`com.unity.cluster-display.cluster-display": "file:../../Packages/com.unity.cluster-display.cluster-display`

You can use [Git Submodules](https://git-scm.com/book/en/v2/Git-Tools-Submodules) to download the packages to more easily update.
