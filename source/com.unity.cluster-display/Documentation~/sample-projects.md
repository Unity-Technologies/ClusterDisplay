# Sample Projects

## QuadroSyncTest

This sample shows a spinning cube on a background with moving lines, while introducing random "freezes".

It tests the following features:
* Network synchronization
* Swap barriers (if Quadro Sync is present)
* Time synchronization (spinning animation uses the Timeline)
* Recovery from random hangs

TODO: Screenshots, how to run

## Mosys

This sample shows how to set up a Mo-Sys Star Tracker device to run on a cluster. The following scripts are required to integrate the Mo-Sys plugin with the cluster, and the same principles can be applied to set up any `LiveCaptureDevice`, including (Vcam, Face Capture, Vicon, etc.).

**Live Capture Connection Bridge**: Initiates the Live Capture Connection at runtime if running as an Emitter node. Input devices should connect to the Emitter node's IP address.

![](images/live-capture-bridge.png)

**Cluster Replication**: Added to the GameObject that contains the camera parameters controlled by Mo-Sys plugin. The properties of interest have been added to the list, as shown.

![](images/cluster-replication-mosys.png)

### Testing with the Mo-Sys simulator

1. Deploy and run the project to the cluster as usual.
2. You can run the simulator on a machine on the same network as the cluster nodes. The simulator and test data are found in `TestProjects/Mosys/f4-simulator`.
3. Edit `RunTestData.bat` and change the IP address portion to the IP of the Emitter.
4. Run `RunTestData.bat`.

> **Troubleshooting**:  The scripts are currently unable to handle multiple network interfaces, so the connection will be created on the first available interface. In the lab, this means that the simulator must be executed on machine on the `Cluster_Sync` network (i.e. one of the cluster nodes), and the target IP in `RunTestData.bat` must be the IP address of the Emitter node on the `Cluster_Sync` network (not the corporate network).

> **Tip**:  Currently, we can only run Live Capture connections on their default ports. In the case of the Mo-Sys Star Tracker, this is 8001.

## VirtualCameraTest

This sample shows how you can use camera tracking data to achieve an MR effect (e.g. virtual set extension on an LED wall). Although you would not use an iPhone camera on a real production, you can in principle replicate this with any `LiveCapture` input device.

![](images/livecapture-tracking.gif)

It tests the following features:
* Component property replication
* Tracked perspective projection policy

### Camera and screen space setup

The MR effect requires that the camera poses are tracked with respect to a known origin in the real world. An image marker is used to define the origin of the _tracking space_ in the real world, and the "ClusterDisplay" object in the scene hierarchy defines the tracking space in the game.

#### Virtual Camera Companion App

A customization of the Virtual Camera Companion App can be found in the `VirtualProduction` git repository on the branch `wen/experimental/vcam-marker-origin`. This version of the of the app shows the ARKit camera feed and reports camera poses in the tracking space (i.e. with respect to the image marker).

You can customize the image marker by modifying the `XR/ReferenceImageLibrary` asset. Print out the image used by this asset.

![](images/ref-image-library.png)

> **_IMPORTANT_**:  The "Physical Size" setting must match the printed version.

#### Scene Setup

Open the `SampleScene` in the `VirtualCameraTest` project. Place printed image marker near the displays. Note the orientation of the local image axes (_x_: right, _y_: out of the page, _z_: up).

![](images/tracking-origin-world.png)

> **TIP**:  Place the marker somewhere convenient so that you can measure the positions of the displays relative to it. To avoid measuring angles, keep the image axis-aligned with the display surfaces.

Use the `ClusterRenderer` editor to enter the physical sizes and positions of the projection surfaces (in meters) relative to the image marker. Note that "Local Position" specifies the _center_ of the surfaces. Also note the local axes of the "ClusterDisplay" game object. These axes should align with the axes of the printed image marker.

![](images/tracking-origin-scene.png)

> **TIP**:  You can specify a single project surface if you don't want to run the demo in a cluster (run in fullscreen in the Editor or standalone without arguments).
 
The game is now ready to build and run.

#### Running the Demo

Run the standalone player according to the instructions given in the [Cluster Deployment](cluster-operation.md) doc, or using [Mission Control](../../../MissionControl/README.md). 

Build and run customized Virtual Camera app on a supported iOS device. Connect to the emitter host. Point the camera at the marker image until the 3D axes appear (this could take a few seconds). You should now see the game responding to movements of the phone.

> **NOTE**: The axes may be clipped (not drawn) if you're really close to the marker. Try pulling back a bit if you're not seeing them.
