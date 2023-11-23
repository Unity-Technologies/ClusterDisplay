[Contents](TableOfContents.md) | [Home](index.md) > Cluster Display concepts

# Cluster Display concepts

## What is it for?

In practice, you could use a single machine to render to multiple displays and/or high resolution displays (4K+), but the machine's computational power might present a limit to this approach. With Unity Cluster Display, you can scale up to an arbitrary number of machines, therefore an arbitrary number of displays: Unity Cluster Display currently supports up to 64 nodes. However, if you need to increase this number, it is technically possible.

Note that Unity Cluster Display does not prevent the use of multiple displays per machine. The total number of pixels a machine can render to depends both on its hardware capabilities and the user project's complexity.

## Clustering and synchronization

A Cluster Display setup typically consists of **one emitter node**, **several repeater nodes**, and optionally some number of **backup nodes**:

* A single repeater node consists of a workstation and a display output.
    * Each workstation runs a copy of your Unity application with Cluster Display enabled.
    * All the nodes run the same interactive content in lockstep, but each one only renders a subsection of the total display surface.
* The emitter is responsible for synchronizing the state for all repeater nodes.
    * The repeater nodes connect to the emitter node via a **wired** Local Area Network.
    * The emitter node does not technically need to be connected to a display.
* A backup node behaves like a repeater node except:
    * When a failover is triggered, it will take over the role of an emitter or a repeater.

For more details, see [Cluster Display synchronization](synchronization.md).

## Terminology

| Word         | Definition      |
|--------------|-----------------|
| **Node**     | A node is a workstation that runs as part of a cluster.|
| **Cluster**  | A cluster is a collection of nodes that collaborate to render a larger image. |
| **Emitter**  | A emitter is a special node in a cluster that controls and distributes the necessary information for repeaters to be able to render their sections of a larger image. |
| **Repeater** | A repeater is a special node in a cluster that receives the necessary information from an emitter to render their section of a larger image. |
| **Backup**   | A node that can take the place of an emitter or repeater when a failover condition is triggered. Needs to be launched at the same time as the rest of the cluster. |
