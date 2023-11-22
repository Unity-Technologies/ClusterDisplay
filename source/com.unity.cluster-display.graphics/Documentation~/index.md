# Unity Cluster Display Graphics package

This package is used in conjunction with the main `cluster-display` package to control how your scene is rendered across multiple displays.

See the [documentation](../../../source/com.unity.cluster-display/Documentation~/index.md) for general information, installation, and use of Cluster Display.

## Requirements

Cluster Display Graphics depends on URP or HDRP 14.0.3 (Unity 2022.2 and above). Install either "Universal RP" or "High Definition RP" from the package manager.

## Using Unity.ClusterDisplay.Graphics

Cluster Display Graphics features are enabled by adding an instance of the `ClusterRenderer` component to your scene. This component allows you to define a **Projection Policy**. See [this doc](./projection-policies.md) for more details on the various projection policies.
