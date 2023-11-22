# Cluster Display for Unity Editor

Use the Unity Cluster Display packages (`com.unity.cluster-display`) to display synchronously the same Unity Scene across multiple machines through display clustering.

This repository contains all packages, resources and sample projects related with Unity Cluster Display.

## Get started

To learn about the Unity Cluster Display package (concepts, features, and workflows) read the [Cluster Display package documentation](source/com.unity.cluster-display/Documentation~/index.md) in this repository.  

### Requirements

* Unity 2023.1 or newer
* Windows 10

### Check out the licensing model

The Cluster Display package is licensed under the [Apache License, Version 2.0](LICENSE.md).

### Contribution and maintenance

We appreciate your interest in contributing to the Unity OSC Protocol Support package.  
It's important to note that **this package is provided as is, without any maintenance or release plan.**  
Therefore, we are unable to monitor bug reports, accept feature requests, or review pull requests for this package.

However, we understand that users may want to make improvements to the package.  
In that case, we recommend that you fork the repository. This will allow you to make changes and enhancements as you see fit.

## Cluster Display packages and projects

### Access the Cluster Display package folders

| Package | Description |
|:---|:---|
| **[com.unity.cluster-display](source/com.unity.cluster-display)** | The core Cluster Display package which allows Unity applications to run on multiple machines and simulate the same scene in-sync with each other. |
| **[com.unity.cluster-display.graphics](source/com.unity.cluster-display.graphics)** | Package that contains a toolkit providing rendering features for Cluster Display, such as non-standard projections and overscan. |
| **[com.unity.cluster-display.rpc](source/com.unity.cluster-display.rpc)** | Package that allows making builds that run on multiple machines and simulate the same scene in-sync with each other. |

### Test the Cluster Display package

Use these sample projects to manually test the Cluster Display solution in different contexts:

| Project | Description |
|:---|:---|
| **[ClusterRenderTest](TestProjects/ClusterRenderTest)** | Unity project to test a cluster render. |
| **[ClusterSyncTest](TestProjects/ClusterSyncTests)** | Unity project to test a cluster sync. |
| **[GraphicsDemoProject](TestProjects/GraphicsDemoProject)** | Unity project to test Cluster Display with graphics. |
| **[GraphicsTestsHDRP](TestProjects/GraphicsTestsHDRP)** | Unity project to test Cluster Display with graphics in HDRP. |
| **[GraphicsTestsURP](TestProjects/GraphicsTestsURP)** | Unity project to test Cluster Display with graphics in URP. |
| **[LiveEditingTests](TestProjects/LiveEditingTests)** | Unity project to test Cluster Display and live editing. |
| **[MissionControlTests](TestProjects/MissionControlTests)** | Unity project to test Cluster Display with Mission Control. |
| **[Mosys](TestProjects/Mosys)** | Unity project to test Cluster Display with Mosys. |
| **[QuadroSyncTest](TestProjects/QuadroSyncTest)** | Unity project to test Cluster Display with a QuadroSync board. |
| **[RPCTests](TestProjects/RPCTests)** | Unity project to test Cluster Display with RPC. |
| **[VirtualCameraTest](TestProjects/VirtualCameraTest)** | Unity project to test Cluster Display with a Virtual Camera. |

See also [Sample Projects](source/com.unity.cluster-display/Documentation~/sample-projects.md) page for more details.

## Cluster Display Mission Control

Use [Cluster Display Mission Control](MissionControl-v2) to
* Manage multiple computers working together to form a Cluster Display.
* Load, start and stop executables built with Unity for display on the clustered screens.
