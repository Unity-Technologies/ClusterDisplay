# Setting Up Existing Project with Cluster Display

1. Refactor your project's assets, scripts, scenes, etc into a package using the following guide:

    https://docs.unity3d.com/Manual/CustomPackages.html

    Explanation here: [Editor Testing](./editor-testing.md)

2. Navigate to your project's **Packages\manifest.json**.
2. Add the cluster display packages:
    ```
        "com.unity.cluster-display": "file:{path to package}/source/com.unity.cluster-display",
        "com.unity.cluster-display.graphics": "file:{path to package}/source/com.unity.cluster-display.graphics",
        "com.unity.cluster-display.rpc": "file:{path to package}/source/com.unity.cluster-display.rpc",
        "com.unity.cluster-display.helpers": "file:{path to package}/source/com.unity.cluster-display.helpers",
    ```
4. Add the following graphics package dependencies:
    ```
        "com.unity.render-pipelines.core": "file:{path to package}/com.unity.render-pipelines.core",
        "com.unity.render-pipelines.high-definition-config": "file:{path to package}/com.unity.render-pipelines.high-definition-config",
        "com.unity.shadergraph": "file:{path to package}/com.unity.shadergraph",
        "com.unity.visualeffectgraph": "file:{path to package}/com.unity.visualeffectgraph",
    ```
5. Then add **ONE** of the following to import the custom branch of either URP **OR** HDRP:
    ```
        "com.unity.render-pipelines.universal": "file:{path to package}/com.unity.render-pipelines.universal",
        "com.unity.render-pipelines.high-definition": "file:{path to package}/com.unity.render-pipelines.high-definition",
        "com.unity.render-pipelines.high-definition-config": "file:{path to package}/com.unity.render-pipelines.high-definition-config",
        "com.unity.shadergraph": "file:{path to package}/com.unity.shadergraph",
        "com.unity.visualeffectgraph": "file:{path to package}/com.unity.visualeffectgraph",
    ```
6. Now add a reference to your project package:
    ```
        "{com.{your package id}}": "file:{path to package}/com.{your package id}",
    ```
7.  Open your project in the Unity Editor and if you modified your manifest.json correctly, the project should boot without errors.

8.  Edit your **Project Settings** as per the following recommendations:

    -  In **Quality > Other**, set **VSync Count** to **Every V Blank**.

    ![](images/vsync.png)

    -  ~~In **Player > Other Settings > Configuration**, set **Scripting Backend** to **IL2CPP**.~~ **(Currently broken, use managed)**

    -  In **Player > Other Settings > Configuration**, enable the **Use Incremental GC** option (checkbox) to help avoiding framerate jitters caused by [garbage collection](https://blogs.unity3d.com/2018/11/26/feature-preview-incremental-garbage-collection/).

    -  In **Player > Resolution and Presentation > Resolution**, set **Fullscreen Mode** to **Exclusive Fullscreen** (see [Standalone Player Settings](https://docs.unity3d.com/Manual/class-PlayerSettingsStandalone.html) for more details).

    ![](images/fullscreen-exclusive.png)

    - Verify that your Unity project has **Run in Background** set to **true**

    ![](images/run-in-background.png)


9. **If you are using URP**, verify that the following is toggled on your **"Universal Render Pipeline Asset"**:

    ![URP Texture Settings](images/urp-texture-setting.png)

10. **If you are using URP**, add the following rendering feature to your **"Forward Renderer"**

    ![URP Render Feature](images/urp-render-feature.png)

11. In the cluster display graphics package, find the ClusterDisplay prefab in: **Cluster Display Graphics/Rutnime/Prefabs/ClusterDisplay.prefab** and drag it into your desired scene.

    ![Cluster Display Prefab](images/cluster-display-setup-menu.png)

12. Add the "ClusterCamera" component to the camera you want to render across your cluster:

    ![Cluster Display Prefab](images/cluster-camera.png)

13. Now make a copy of your project to use as your second project instance and you should be ready to go.