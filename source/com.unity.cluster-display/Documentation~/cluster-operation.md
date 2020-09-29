# Cluster operation

## Deploying your application across the cluster

You must ensure that each client node of the Cluster Display can run the same version of your application at the same time.

**Note:** Before doing this, you must have properly [set up and built](project-setup.md) your Unity project into a standalone application according to the Cluster Display requirements.

To make your application available across your cluster, you can:

-   Make several copies of your application and deploy them on each client node,
    <br />OR
-   Make a single copy of your application accessible to all client nodes from a shared network location.

## Starting the cluster

To start a Cluster Display group:

1.  Start the master and all client machines and wait until they all finished starting.

2.  Make sure that your application is properly [deployed across your client nodes](#deploying-your-application-across-the-cluster).

3.  Launch your application on each machine with the appropriate [command line-arguments](#command-line-and-mandatory-arguments) to define the master and client relationship.
    <br />**Note:** You can use *PSExec* to run the command from an external machine.

### Command line and mandatory arguments

Use a command line with the following arguments on each node of your cluster to start your standalone built project:

`<binaryName> -<nodeType> <nodeIndex> <number of clients> <multicast address>:<outgoing port>,<incoming port>`

**Note:** All these arguments are mandatory, unless otherwise specified in the table below.

| **Argument** | **Description** |
|--------------|-----------------|
| `<binaryName>` | The path of your application executable file. |
| `<nodeType>` | The type of node on which you are launching the application.<br />Use `-masterNode` for the master, or `-node` for a client node. |
| `<nodeIndex>` | The node index |
| `<number of clients>` | Number of client nodes in the cluster (excluding the master).<br />This argument is mandatory for the master node and you should omit it for the client nodes. |
| `<multicast address>` | Multicast address used by Cluster Sync messages. |
| `<outgoing port>` | Port used by Client to send ACK messages. |
| `<incoming port>` | Port used by Cluster to receive sync messages |

#### Command line examples

Master Node with 3 client nodes:

`Demo.exe -masterNode 0 3 224.0.1.0:25689,25690`

First client node:

`Demo.exe -node 1 224.0.1.0:25690,25689`

Sample of all commands used on a 4-server cluster (Master + 3 clients):

`Demo.exe -masterNode 0 3 224.0.1.0:25689,25690`

`Demo.exe -node 1 224.0.1.0:25690,25689`

`Demo.exe -node 2 224.0.1.0:25690,25689`

`Demo.exe -node 3 224.0.1.0:25690,25689`

### Timeout arguments

You can optionally add arguments to the command line to control the timeout values for the [communication phases](reference.md#communication-phases-and-timeouts) between the master and the clients.

| **Argument** | **Description** |
|--------------|-----------------|
| **handshakeTimeout \<value\>** | Timeout (in *milliseconds*) for the handshake phase.<br />The default value is 30000 (30 seconds). |
| **communicationTimeout \<value\>** | Timeout (in *milliseconds*) for the regular lockstep rendering phase.<br />The default value is 5000 (5 seconds).|

>**Note:** Set the communication timeout to a lower value on the server node (e.g. 4000) compared to the client nodes (e.g. 5000) to prevent an avalanche quit phenomenon where the server node needs to kick out unresponsive nodes before the client node timeout occurs.

#### Command line examples

Master Node with 3 clients:

`Demo.exe -masterNode 0 3 224.0.1.0:25689,25690 -handshakeTimeout 30000 -communicationTimeout 5000`

First client node:

`Demo.exe -node 1 224.0.1.0:25690,25689 -handshakeTimeout 30000 -communicationTimeout 6000`


### Screen settings arguments

As previous resolutions and full screen settings are persistent from one run to another through the Windows Registry, you might need to force these settings through command line arguments to ensure to have the same expected ones across your cluster.

For example:

`.\\ClusterRenderTest.exe -window-mode exclusive -screen-width 1920 -screen-height 1080 -screen-fullscreen 1`


See [StandaloneBuildArguments](https://docs.unity3d.com/Manual/CommandLineArguments.html) for a list of accepted arguments (The Unity standalone Player section)

## Splitting the displays across multiple machines

With all client nodes synchronized to the master node in the cluster, you must organize the nodes so that each one is rendering an offset portion of the overall view. The synchronization data does not include information about the Camera, so you are free to manipulate the camera individually in each node. Since all nodes are simulating the same scene, the key is to manipulate the camera properties so that each node is rendering the correct portion of the
overall display.

How to achieve this depends on how the multiple physical screens are set up, but the basic approach is to assign a different projection matrix to each node’s camera. See
[Camera.projectionMatrix](https://docs.unity3d.com/560/Documentation/ScriptReference/Camera-projectionMatrix.html) for more information on how to do this.

## Quitting the cluster

To manually quit the cluster and stop displaying your Unity project on your multi-display setup:

1.  Use a keyboard connected to the master node.

2.  Press **Q** and **K** at the same time.

**Note:** The nodes also automatically quit the cluster if the [cluster communication times out](reference.md#communication-phases-and-timeouts).
