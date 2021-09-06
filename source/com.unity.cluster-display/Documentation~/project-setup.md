# Unity project preparation

To be able to use your Unity project in the context of Cluster Display, you must:

1.  Be aware of some [project experience requirements](#project-experience-requirements-and-optimization) to avoid unexpected issues.

2.  [Set up your project](#project-setup) with a few required components and parameters.

3.  [Build your project](#building-your-project) as a Windows standalone player.

## Project experience requirements and optimization

This section gives you a few guidelines to build a project experience that complies with Unity Cluster Display functionality to prevent from getting unexpected issues.

### Operating system overlays – frame delay

Whenever you have operating system managed overlays (e.g. Windows Taskbar, TeamViewer windows, Windows File Explorer) on top of your Fullscreen Unity application, this may introduce a one-frame delay causing cluster synchronization artefacts.

### Framerate drops – screen tearing

Framerate drops cause temporary loss of synchronization, which leads to temporary screen tearing until the framerate is back to the targeted one. For this reason, you should design your project experiences so that the framerate never drops.

### VFX Graph Particles – reseed issues

If you are using VFX Graph Particles effects in HDRP, make sure to *disable* the **Reseed on play** option (checkbox) on the **VisualEffect** component, otherwise each node may end up with differently seeded random number generators, leading to visual artefacts.

![](images/component-visual-effect.png)

## Project setup

1.  Open your project in the Unity Editor.

2. In the cluster display graphics package, find the ClusterDisplay prefab in: **Cluster Display Graphics/Rutnime/Prefabs/ClusterDisplay.prefab** and drag it into your desired scene.

![Cluster Display Prefab](images/cluster-display-prefab.png)

3.  Edit your **Project Settings** as per the following recommendations:

    -  In **Quality > Other**, set **VSync Count** to **Every V Blank**.

    -  In **Player > Other Settings > Configuration**, set **Scripting Backend** to **IL2CPP**.

    -  In **Player > Other Settings > Configuration**, enable the **Use Incremental GC** option (checkbox) to help avoiding framerate jitters caused by [garbage collection](https://blogs.unity3d.com/2018/11/26/feature-preview-incremental-garbage-collection/).

    -  In **Player > Resolution and Presentation > Resolution**, set **Fullscreen Mode** to **Exclusive Fullscreen** (see [Standalone Player Settings](https://docs.unity3d.com/Manual/class-PlayerSettingsStandalone.html) for more details).

## Building your Project

To be able to use your Unity project in Cluster Display context, you must build it as a [Windows standalone player](https://docs.unity3d.com/Manual/BuildSettingsStandalone.html) from a Unity editor with the proper license.

Once built, the standalone player includes the Cluster-enabled features according to your project setup.

> **Important:** This standalone player is the one that you will need to make available to all cluster nodes through a further step. You must always ensure to use the exact same copy of it across the cluster.
