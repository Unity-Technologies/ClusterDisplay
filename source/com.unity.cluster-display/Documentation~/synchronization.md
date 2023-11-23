[Contents](TableOfContents.md) | [Home](index.md) > [Cluster Display concepts](concepts.md) > Cluster Display Synchronization

# Cluster Display Synchronization

## Synchronization method – Lockstep

This synchronization method is known as frame locking, or “lockstep” synchronization. This means the emitter node will propagate an “update” signal with its internal state to all client nodes, signaling them to render a frame. The next “update” is not sent unless all clients reported the completion of their previous workload.

In some cases, the emitter needs to perform a frame update ahead of all clients (for example, when using the `com.unity.cluster-display.rpc` package). To ensure pixel perfect frame synchronization between the emitter and repeater nodes, the emitter node will automatically queue the rendered frame so it can be presented on the next frame when the repeater nodes render their frames.

The graph below illustrates the lockstep rendered frame timings between nodes in the cluster display network when the emitter is one frame ahead.
![Lockstep Synchronization](images/cluster-display-synchronization-lockstep.png)

## Data Payload Synchronization

The emitter node automatically synchronizes the following data across the network:

- TimeManager data: Time.deltaTime, Time.unscaledDeltaTime, etc.
    > **Note:** `Time.realTimeSinceStartup`` is not synchronized. You should avoid using it.
- Random number internal state
- Input events from the emitter: keyboard, mouse, etc.
- Other custom data, e.g. RPC events (requires the `com.unity.cluster-display.rpc` package).

## Communication Phases and Timeouts

The communication between the emitter and the clients happens in 2 phases:

1. Initial handshake

2. Ongoing synchronization

### Handshake

- The initial handshake occurs at startup between the emitter and each node:

  - When the emitter node starts, it waits for the specified number of clients to connect.
  - When the client nodes start, they advertise their presence and wait until a emitter accepts them into the cluster.
  - The emitter node waits up to a predetermined timeout for nodes to register and continues with the current set of nodes.
  - The emitter node quits if no client nodes are present by the end of the timeout.
  - The client nodes quit if no emitter node accepted them in a cluster after this amount of time.

### Synchronization

- After the initial handshake occurs, all nodes switch into a regular lockstep rendering phase that lasts until the end of the simulation.

  - The emitter node waits up a predetermined timeout before kicking out an unresponsive node from the cluster.
  - The client nodes wait up a predetermined timeout for an unresponsive emitter node before quitting.
