# About Cluster Display

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

## Recommendations

* Managed switch/router with access to [IGMP](https://en.wikipedia.org/wiki/Internet_Group_Management_Protocol) settings (See [Cluster Times Out After Period](source/com.unity.cluster-display/Documentation~/troubleshooting.md#cluster-times-out-after-period)).
* Choose a motherboard that supports [IPMI](https://en.wikipedia.org/wiki/Intelligent_Platform_Management_Interface) so you can remotely shutdown, restart and boot your nodes without needing physical access to the machines.
* If you are using Quadro Sync the following hardware is required:
    * Requires one or more [NVIDIA Quadro GPU](https://www.nvidia.com/en-us/design-visualization/quadro/)s.
    * Requires one or more [NVIDIA Quadro Sync II](https://www.nvidia.com/en-us/design-visualization/solutions/quadro-sync/) boards.

## Terminology

| Word         | Definition      |
|--------------|-----------------|
| **Node**     | A node is a workstation that runs as part of a cluster.|
| **Cluster**  | A cluster is a collection of nodes that collaborate to render a larger image. |
| **Emitter**  | A emitter is a special node in a cluster that controls and distributes the necessary information for repeaters to be able to render their sections of a larger image. |
| **Repeater** | A repeater is a special node in a cluster that receives the necessary information from an emitter to render their section of a larger image. |
| **Backup**   | A node that can take the place of an emitter or repeater when a failover condition is triggered. Needs to be launched at the same time as the rest of the cluster. |

## Get more verbose logs

Include the **CLUSTER_DISPLAY_VERBOSE_LOGGING** scripting define symbol in the player settings to get verbose logging and send those logs to us if something is broken. You can find where those logs are located by reading this [page](https://docs.unity3d.com/Manual/LogFiles.html).

![Verbose Logging](images/verbose-logging.png)

## Also in this documentation

| Section                                                                                                                  | Description                                                           |
|:-------------------------------------------------------------------------------------------------------------------------|:----------------------------------------------------------------------|
| [Network Configuration](source/com.unity.cluster-display/Documentation~/network-configuration.md)                        | Set up your physical network according to your network configuration. |
| [Setup Cluster Display with Existing Project](source/com.unity.cluster-display/Documentation~/setup-existing-project.md) | Set-up an existing project.                                           |
| [Set up Quadro Sync](source/com.unity.cluster-display/Documentation~/quadro-sync.md)                                     | Instruction to set-up a Quadro sync (Optional).                       |
| [Setup Mission Control](MissionControl-v2/README.md)                                                                     | Set-up Mission Control to run your cluster content..                  |
