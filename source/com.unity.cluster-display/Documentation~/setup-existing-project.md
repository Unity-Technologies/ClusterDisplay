[Contents](TableOfContents.md) | [Home](index.md) > Use an Existing Project with Cluster Display

# Quick Start: Use an Existing Project with Cluster Display

This guide describes how to enable Cluster Display in an existing Unity project.

> **_NOTE:_** Cluster Display rendering requires URP or HDRP 14.0.3 or newer.

1. Navigate to your project's **Packages\manifest.json**.

2. Add the cluster display packages:

    ```json
        "com.unity.cluster-display": "file:{path to package}/source/com.unity.cluster-display",
        "com.unity.cluster-display.graphics": "file:{path to package}/source/com.unity.cluster-display.graphics"
    ```

3. You should now see in your Project Settings a section for **Cluster Display**. Check the **Enable On Play** option.
   ![Cluster Display Settings](images/cluster-settings.png)

4. Select the **Cluster Rendering** subsection. Click the **Set up Cluster Renderer** button. This will create a new Cluster Renderer component in your scene,
   as well as add a Cluster Camera component to existing cameras. You can edit your projection settings from the Settings window or in the Inspector.
   ![Cluster Rendering Settings](images/rendering-settings.png)

5. Edit your **Project Settings** as per the following recommendations:

    - In **Quality > Other**, set **VSync Count** to **Every V Blank**.

    ![Set vsync](images/vsync.png)

    - ~~In **Player > Other Settings > Configuration**, set **Scripting Backend** to **IL2CPP**.~~ **(Currently broken, use managed)**

    - In **Player > Other Settings > Configuration**, enable the **Use Incremental GC** option (checkbox) to help avoiding framerate jitters caused by [garbage collection](https://blogs.unity3d.com/2018/11/26/feature-preview-incremental-garbage-collection/).

    - In **Player > Resolution and Presentation > Resolution**, set **Fullscreen Mode** to **FullScreen Window** (see [Standalone Player Settings](https://docs.unity3d.com/Manual/playersettings-windows.html) for more details).

    ![Fullscreen Exclusive](images/fullscreen-window.png)

    - Verify that your Unity project has **Run in Background** set to **true**

    ![Run in background](images/run-in-background.png)

6. To run your Cluster Display-enabled game, create a standalone build and follow the directions for [Mission Control](mission-control.md).
