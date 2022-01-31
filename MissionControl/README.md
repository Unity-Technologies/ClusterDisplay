Cluster Display Mission Control
===============================

Overview
--------

Mission Control system allows you to administer Cluster Display system remotely. It current supports the following operations:

* Discovering nodes on the network and displaying their statuses
* Synchronizing player files (builds) from a shared directory
* Starting and stopping a player executable across the cluster
* Setting Node IDs
* Deleting registry entries for a player (for fixing exclusive fullscreen problems)

It consists of three parts:

1) **Listener**: headless program that runs in background, responsible to announcing the node status to the Server and handling commands issued by the Server; maintains a local copy of the player files.
2) **Server**: maintains a list of active nodes and provides a C# API for interacting with the nodes.
3) **UI for Unity Editor**: A frontend for the Server that runs in the Unity Editor.

Setup
-----

### Listener

#### Prerequisites:

- .NET Core 5.0+
- ability to receive UDP broadcasts
- shared network directory or network drive accessible by all nodes 

#### Running

1. Copy the Listener's binary folder to the node's local filesystem.
2. Run `ClusterListener.exe` in a command prompt.

### Server and UI

Prerequisites: Unity 2022.1+

1. Copy the `Editor` folder to any Unity's project's `Assets`.
2. Open the Mission Control window by selecting **Cluster Display | Mission Control** from the main menu.
3. If the machine running the UI is not able to broadcast to the nodes (e.g. you are on a VPN), you need to enter the IP address of one of the nodes in the **Broadcast Proxy Address** field. Otherwise, leave this field blank.

Running a player build
----------------------

1. Copy the player build folder to a shared network folder.
2. Enter the path of the shared network directory in the **Shared Folder** field, i.e. `\\<hostname>\<SharedFolder>` or `<drive>:\<SharedFolder>`.
3. Select one of the **Available Players** and click **Run Selected Player**.

#### Note:

You can force stop of the current operation by clicking **Stop All**.

The nodes will sync and run the latest version of the build in the shared folder.