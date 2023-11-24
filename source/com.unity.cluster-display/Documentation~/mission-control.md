[Contents](TableOfContents.md) | [Home](index.md) > Cluster Display Mission Control

# Cluster Display Mission Control

## Overview

Mission Control system allows you to manage multiple computers working together to form a Cluster Display. It can be used to:

* Define the computers that are part of the cluster
* Manage a list of assets (built scenes, games, levels, ... (depending how you want to call them))
* Configure the execution of assets cluster
* Save and load those configurations
* Start / Stop execution of the asset on the cluster
* Monitor the state of the nodes forming the cluster

It consists of four parts:

1) **Hangar Bay**: headless program that runs in background, responsible for keeping a local cache of recently used asset files.
2) **Launchpad**: headless program that runs in background, responsible for launching and stopping the assets.
3) **Mission Control**: headless program that runs in the background and is responsible for managing the cluster.  It can be installed on any node of the cluster or on a dedicated computer with a fast connection to the nodes of the cluster.  Sufficient storage should also be available as it will store all the assets that are to be made available on the cluster.
4) **Mission Control UI Server**: The Mission Control's user interface is in fact a web application (using WebAssembly) that has to be served.  The actual UI server will generally be located on the same computer as Mission Control (and is fairly lightweight).

## Setup

### Common

* Every computer running one of the previously listed four parts will need to have the .Net Core 6.0 runtime installed: <https://dotnet.microsoft.com/en-us/download/dotnet/6.0>.
  > ℹ️ _**Remarks:**_ There are multiple components that can be download from that page, what is needed is the "ASP.NET Core Runtime" Hosting Bundle.
* The listed setup steps below assumes that TCP ports 8000 - 9000 are free and can be used by services to listen for HTTP requests (to simplify the procedure).

### High level instructions

1) Install a [Hangar Bay](#hangar-bay) on each computer of the cluster that is to display the assets
2) Install a [Launchpad](#launchpad) on each computer of the cluster that is to display the assets
3) Install a [Mission Control](#mission-control) on one of the computers in the cluster (can be a computer that also display content or a dedicated computer)
4) Install a [Mission Control UI Server](#mission-control-ui-server) on one of the computers in the cluster (can be a computer that also display content or a dedicated computer)

### Hangar Bay

The Hangar Bay is responsible for keeping a local cache of recently used asset files on each of the nodes of the cluster.  As such, there will normally be one per computer in the cluster.  To install it:

1) Extract HangarBay.zip in the folder in which you want to store the HangarBay
2) Start *YourFolder*/bin/HangarBay.exe
    * This will open a console application that should show various messages during the execution of the process
    * The console should not contain any error messages
    * If prompted for Firewall, allow it to access all the networks
3) Optional: By default, the local cache will be stored in current user's *MyDocuments\Unity\Mission Control Cache* folder with a maximum size of half the disk free space on the first startup
    1) This can be changed by stopping the hangar bay by pressing Ctrl-C in the console
    2) Open *YourFolder*/config.json in your favorite json editor
    3) Change the path and maximum size (in bytes) of the storage folder to the desired one
        * Important to remember if using a text editor, \ has to be escaped in json, so don't forget to double your back slashes or use forward slashes
    4) Save the modified config.json
    5) Restart the hangar bay (by executing *YourFolder*/bin/HangarBay.exe)

### Launchpad

The Launchpad is responsible for launching and stopping the assets.  There will normally be one per node (computer) in the cluster, but we could imagine having multiple if that computer is using multiple GPUs to output to multiple displays (not yet supported).  To install it:

1) Extract LaunchPad.zip in the folder in which you want to store the LaunchPad
2) Start *YourFolder*/bin/LaunchPad.exe
    * This will open a console application that should show various messages during the execution of the process
    * The console should not contain any error messages
    * If prompted for Firewall, allow it to access all the networks
3) Stop the just started launchpad by pressing Ctrl-C in the console
4) Open *YourFolder*/config.json in your favorite json editor
5) Validate that clusterNetworkNic has been set to the name of the network adapter (as found in the *Network Connections* windows of Windows) used for broadcasting the state from the emitter node to the repeater nodes (this network has to support UDP multicast traffic)
6) Save your changes (if any)
7) Restart the launch pad (by executing *YourFolder*/bin/LaunchPad.exe)

### Mission Control

Mission Control is responsible to orchestrate everything in the cluster (with the help of the [Hangar Bays](#hangar-bay) and [Launchpads](#launchpad) on each of the cluster nodes).

Depending on the amount of assets that is to be preserved to be easily re-selected and re-launched, mission control might need a significant amount of storage.  Also it is possible to import a new asset while another asset is running which can result in higher CPU / Memory and Disk usage which could disrupt the currently launched asset.  Because of all those reasons Mission Control can be installed on a dedicated computer for those heavier use-cases.  But for light usage it can simply be installed on one of the nodes of the cluster.

1) Extract MissionControl.zip in the folder in which you want to store mission control
2) Start *YourFolder*/bin/MissionControl.exe
    * This will open a console application that should show various messages during the execution of the process
    * The console should not contain any error messages
    * If prompted for Firewall, allow it to access all the networks
3) Stop the just started mission control by pressing Ctrl-C in the console
4) Open *YourFolder*/config.json in your favorite json editor
5) Optional: Change the path and maximum size (in bytes) of the storage folder to one (or many) folders where you want to store the assets managed by mission control
    * Folders given in this list must be empty (and will then be filled by mission control)
    * A folder in that list cannot be removed from the list if it contains any file or otherwise assets managed by mission control will be corrupted
    * Important to remember if using a text editor, \ has to be escaped in json, so don't forget to double your back slashes or use forward slashes
6) Change launchPadsEntry from `http://127.0.0.1:8000/` to `http://nic address:8000/` where *nic address* is the local address of the network that can be used by the computers running the launchpad to access mission control
7) Save the modified config.json
8) Restart the hangar bay (by executing *YourFolder*/bin/MissionControl.exe)

### Mission Control UI Server

Mission Control UI Server is responsible for serving the web applications making mission control's features available to the user.  It will normally be installed on the same computer as [Mission Control](#mission-control).

1) Extract UI.zip in the folder in which you want to store the mission control's UI server
2) Open *YourFolder*/wwwroot/appsettings.json in your favorite json editor
3) Change localhost from `http://localhost:8000` to an address that can be accessed by any computer using the UI
4) Save the modified appsettings.json
5) Start *YourFolder*/MissionControl.EngineeringUI.Server.exe
    * This will open a console application that should show various messages during the execution of the process
    * The console should not contain any error messages
    * If prompted for Firewall, allow it to access all the networks

## Initial Configuration

Once all the processes are running on each node of the cluster you will need to inform mission control of the existence of those node.  For that, open mission control's user interface in your web browser by navigating to `http://mission control ui server address:9000` where you replace *mission control ui server address* by the actual ip address or DNS name of the server hosting mission control's user interface.  Then:

1) Go in the Devices section (in the left menu)
2) Add a new Launch Complex for each node of the cluster where a [Hangar Bay](#hangar-bay) is running
    1) Specify a descriptive name
    2) Give the address of the hangar bay service, it should be `http://address of the computer:8100/`
    3) Add a new [Launchpad](#launchpad) for each launchpad on that node (at the moment there should be only one)
        1) Specify a descriptive name
        2) Give the address of the launch pad service, it should be `http://address of the computer:8200/`

## Importing an asset to be launched

Assets (built scenes, games, levels, ... (depending how you want to call them)) to be executed with mission control first need to be added to the list of assets managed by mission control.  To do this:

1) Go in the Assets section (in the left menu)
2) Click the Add New button
3) Give it a name and description
4) Give the path where the asset (compiled Unity project) is to be imported from
    * This needs to be improved at some point, but at the moment this must be a path that is accessible from the Mission Control computer, not from the computer running the web application

## Launching an asset

1) Go in the Configuration section (in the left menu)
2) Select the asset in the list (at the top)
3) Click the Launch button (at the top-left)

> ℹ️ _**Remarks:**_ Many options can be customized but the default ones should be good to allow launching of the asset.
