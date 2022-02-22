# Cluster Display Synchronization

## Synchronization method – Lockstep
This synchronization method is known as frame locking, or “lockstep” synchronization. This means the emitter node will propagate an “update” signal with its internal state to all client nodes, signaling them to render a frame. The next “update” is not sent unless all clients reported the completion of their previous workload.

**The emitter node is always one frame ahead of ALL of the repeater nodes**. However, to insure pixel perfect frame synchronization between the emitter and repeater nodes, the emitter node will automatically queue the rendered frame so it can be presented on the next frame when the repeater nodes render their frames.

The graph below demonstrates the lockstep rendered frame timings between nodes in the cluster display network:
![Lockstep Synchronization](images/cluster-display-synchronization-lockstep.png)

## Data Payload Synchronization
The emitter node automatically synchronizes the following data across the network:

-   TimeManager data: Time.deltaTime, Time.unscaledDeltaTime, etc.
    <br />**Note:** Time.realTimeSinceStartup is not synchronized. You should avoid using it.

-   Random number internal state

-   Input events from the emitter: keyboard, mouse, etc.

-   RPC events.

## Communication Phases and Timeouts
The communication between the emitter and the clients happens in 2 phases:

1.  Initial handshake

2.  Ongoing synchronization

### Handshake
-   The initial handshake occurs at startup between the emitter and each node:

    -   When the emitter node starts, it waits for the specified number of clients to connect.

    -   When the client nodes start, they advertise their presence and wait until a emitter accepts them into the cluster.

    -   The emitter steps forward and renders one frame.

-   The handshake has a timeout of 30 seconds by default. You can change this value through a [command line argument](cluster-operation.md#timeout-arguments) when launching your application.

    -   The emitter node waits at most this amount of time for nodes to register and continues with the current set of nodes.

    -   The emitter node quits if no client nodes are present by the end of the timeout.

    -   The client nodes quit if no emitter node accepted them in a cluster after this amount of time.

### Synchronization
-   After the initial handshake occurs, all nodes switch into a regular lockstep rendering phase that lasts until the end of the simulation.

-   The regular lockstep rendering phase has a timeout of 5 seconds by default. You can change this value through a [command line argument](cluster-operation.md#timeout-arguments) when launching your application.

    -   The emitter node waits this amount of time before kicking out an unresponsive node from the cluster.

    -   The client nodes wait this amount of time for an unresponsive emitter node before quitting.

    -   Typically, you should set the communication timeout a bit lower on the emitter node compared to the client nodes to prevent an avalanche quit phenomenon where the emitter node needs to kick out unresponsive nodes faster than the client node’s timeout.
