# Running and Testing
The ideal project setup allows you to test your Cluster Display project between two editor instances. The way we do this is by creating two Unity projects that point to the same set of packages, these shared packages include:
* The cluster display package.
* The HDRP/URP packages.
* Your project package.

Instead of putting your project assets in the Asset folder, you place all of your assets in a package that's referenced by two projects.

![](images/project-structure.png)

Here is a demonstration of the [FPS sample](samples.md#FPS) running across two instances of the Unity editor:
![MicroFPS Gif](images/microfps-sample.gif)

## Setup
There are two methods you can use to test:
1. Open the scenes from two separate instances of the Unity editor to play.

2. Stage the scenes and build from one instance of the Unity editor and start two instances of the resulting executable.

3. Lets start by loading the cluster display layout from disk from **each** editor instance.

    ![Scene Composition Manager](images/cluster-display-layout-0.png)

4. Navigate to where the layout is located: **{path to package}/com.unity.cluster-display/Editor/ClusterDisplayEditorTestingLayout.wlt**

    ![Scene Composition Manager](images/cluster-display-layout-1.png)

5. Now select the loaded cluster display layout from **each** editor instance.

    ![Scene Composition Manager](images/cluster-display-layout-2.png)

6. Use Start + (Right/Left) arrow to place both editor instances side by side.

    ![Scene Composition Manager](images/cluster-display-layout-0.gif)

7. Drag the game window to the other side of the editor window and scale it accordingly.

    ![Scene Composition Manager](images/cluster-display-layout-1.gif)

8. Open the **Cluster Display Manager**

    ![Cluster Display Manager](images/cluster-display-manager-0.png)

9. Acknowledge the **Cluster Display Manager** window:

    ![Cluster Display Manager](images/cluster-display-manager-1.png)

10. Configure your command line arguments for cluster display while running in the editor:

    ![Cluster Display Manager](images/cluster-display-manager-2.png)

11. Hit **Play as Emitter** or **Play as Repeater** on each editor instance and they will connect to each other:

    ![Cluster Display Manager](images/cluster-display-manager-3.png)

    ![Cluster Display Manager](images/cluster-display-manager-4.gif)

