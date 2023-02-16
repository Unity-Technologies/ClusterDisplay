# Cluster Rendering Projection Policies

## Tiled

The camera frustum is divided into a rectangular grid.

![](images/grid-demo.gif)

## Mesh Warp

The camera frustum is projected onto the specified geometry. Use this projection to generate a perspective-correct view
on a curved display surface (e.g. LED wall).

![](images/mesh-warp.gif)

## Tracked Perspective

Same as Mesh Warp, above, but provides better image quality if your displays are flat.

![](../com.unity.cluster-display/Documentation~/images/livecapture-tracking.gif)

## 
