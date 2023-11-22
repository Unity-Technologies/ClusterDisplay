[Contents](TableOfContents.md) | [Home](index.md) > [Sample projects](sample-projects.md) > Mo-Sys

# Mo-Sys

This sample shows how to set up a Mo-Sys Star Tracker device to run on a cluster. The following scripts are required to integrate the Mo-Sys plugin with the cluster, and the same principles can be applied to set up any `LiveCaptureDevice`, including (Vcam, Face Capture, Vicon, etc.).

**Live Capture Connection Bridge**: Initiates the Live Capture Connection at runtime if running as an Emitter node. Input devices should connect to the Emitter node's IP address.

![](images/live-capture-bridge.png)

**Cluster Replication**: Added to the GameObject that contains the camera parameters controlled by Mo-Sys plugin. The properties of interest have been added to the list, as shown.

![](images/cluster-replication-mosys.png)

## Testing with the Mo-Sys simulator

1. Deploy and run the project to the cluster as usual.
2. You can run the simulator on a machine on the same network as the cluster nodes. The simulator and test data are found in `TestProjects/Mosys/f4-simulator`.
3. Edit `RunTestData.bat` and change the IP address portion to the IP of the Emitter.
4. Run `RunTestData.bat`.

> **Troubleshooting**:  The scripts are currently unable to handle multiple network interfaces, so the connection will be created on the first available interface. In the lab, this means that the simulator must be executed on machine on the `Cluster_Sync` network (i.e. one of the cluster nodes), and the target IP in `RunTestData.bat` must be the IP address of the Emitter node on the `Cluster_Sync` network (not the corporate network).

> **Tip**:  Currently, we can only run Live Capture connections on their default ports. In the case of the Mo-Sys Star Tracker, this is 8001.
