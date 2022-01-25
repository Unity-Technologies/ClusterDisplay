# Quadro Sync
NVIDIA's NvAPI provides the capability to synchronize the buffer swaps of a group of DirectX swap chains when using Quadro Sync II boards. This extension also provides the capability to synchronize the buffer swaps of different swaps groups, which may reside on distributed systems on a network using a swap barrier. It’s essential to coordinate the back buffer swap between nodes, so it can stay perfectly synchronized (Frame Lock + Genlock) for a large number of displays.

## Setup

You can setup Quadro Sync to synchronize through the network, or through a Quadro Sync card.

Add the **GfxPluginQuadroSyncCallbacks** component to your scene to enable Swap Barriers.

**OS**

-   Windows 10 Enterprise version 1903

**Network**

Every server is connected to two NICs (Network Interface Controller):

-   Build management (VM, copying and executing scripts)

-   Dedicated Cluster Display sync network

**Graphics Card**

-   Nvidia driver: 451.77

-   Resolution set to 1080p60.

-   Nvidia Sync Emitter set to the same machine as Cluster Display emitter cluster node

-   **NVIDIA Control Panel** settings set to recommended settings for NVIDIA Sync:

    -   **3D Settings > Manage 3D Settings > Global Preset** set to **Workstation App–Dynamic Streaming**

    -   **Vertical Sync** set to **Use the 3D Application Settings**. Note: must set Unity project to use V Sync

-   For the Nvidia Sync card setup, in the **Nvidia Control Pane**l:

    -   **Synchronize Display** set to **On this System** for the emitter sync server and set to **On Another System** on all clients

    -   Verify the display to sync is set correctly (currently only one display is used on all clients)

    -   Set the frame rate to 60Hz

-   For the ethernet sync connection, Nvidia recommends the following connection diagram:

    ![](images/connection-diagram.png)

-   Note that **daisy-chaining all servers from one port is not recommended**

-   Do not connect the NVIDIA Quadro Sync port to a switch; connections need to be machine-to-machine

**Multiviewer**

-   **Server Settings** in the **Nvidia Control Panel** of the emitter server set to **An External House Sync Signal**

>**Note:** Since both the Multiviewer and Nvidia Quadro Sync have reference input capability, we are using a tri-level sync generator from Black Magic to feed the reference signal to both the Multiviewer and Sync card. The tri-level sync is set to 59.94Hz.


![](images/synchronize-displays.png)

>**Important:** To initialize the sync, or reinitialize after a reboot, all clients MUST be rebooted BEFORE the emitter sync server

-   Since we use a Multiviewer, every server is output through DisplayPort and then converted to HDMI. The HDMI signal then goes to a Black Magic Design mini-converter and converted to SDI which goes directly in the Multiviewer.

## Operating system overlays – frame delay

Whenever you have operating system managed overlays (e.g. Windows Taskbar, TeamViewer windows, Windows File Explorer) on top of your Fullscreen Unity application, this may introduce a one-frame delay causing cluster synchronization artefacts.

## Framerate drops – screen tearing

Framerate drops cause temporary loss of synchronization, which leads to temporary screen tearing until the framerate is back to the targeted one. For this reason, you should design your project experiences so that the framerate never drops.

## Tested Hardware

We have tested the solution with the hardware and configuration detailed below.

### Components

-   4 x [Supermicro SuperServer 1019GP-TT](https://www.supermicro.com/en/products/system/1U/1019/SYS-1019GP-TT.cfm)
    with:

    -   NVIDIA Quadro P6000 GPU

    -   NVIDIA Quadro Sync II Kit for Pascal Quadro

    -   1x Intel Xeon Gold 5122 3.6GHz Quad-Core Server CPU

    -   4x 16GB DDR4 2666Mhz ECC (64GB total)

    -   1x Samsung 970 Pro 512GB m.2 SSD

-   1x [Blackmagic Design MultiView 16](https://www.blackmagicdesign.com/ca/products/multiview/techspecs/W-MVW-01)

-   16x [Blackmagic Design Mini Converter SDI to HDMI 6G](https://www.blackmagicdesign.com/ca/products/miniconverters/techspecs/W-CONM-27https://www.blackmagicdesign.com/ca/products/miniconverters/techspecs/W-CONM-27)

-   16x [DisplayPort to HDMI cables](https://www.accellww.com/products/displayport-1-2-to-hdmi-2-0-adapter) (6ft)

-   16x SDI cables (6ft)

-   8x Cat 5 ethernet cables (6ft)

-   1x HDMI cable

-   1x Sony 55" 4K UHD HDR OLED Android Smart TV (XBR55A9G)
