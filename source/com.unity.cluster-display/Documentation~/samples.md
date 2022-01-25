# Cluster Display Samples
We've provided a set of sample projects to demonstrate how you may use Cluster Display in your own projects.

## Setup for Testing in the Editor
Follow [these](new-project-setup.md)) instructions to setup a new project from the samples template:

[New Project Setup](new-project-setup.md)

If you want to setup the samples with an existing project, follow [this](setup-existing-project.md) guide:

[Setup an Existing Project](setup-existing-project.md)

1. If you did not create a samples project through the **CreateNewProject.ps1** script, then insert this dependency in your manifest.json:
```
        "com.unity.cluster-display.samples": "file:{path to package}/source/com.unity.cluster-display.samples",
```

2. You can open each sample via the scene composition manager:

    ![Scene Composition Manager](images/scene-composition-manager.png)

3. Open the **Cluster Display Manager**

    ![Cluster Display Manager](images/cluster-display-manager-0.png)

11. Hit **Play as Emitter** on the first editor instance and **Play as Repeater** on the second editor instance:

    ![Cluster Display Manager](images/cluster-display-manager-4.gif)

# Samples

## Audio Reactive
This sample propagate the audio spectrum from a audio input device for visualization across the cluster display.

![AudioReactive Gif](images/audioreactive-sample.gif)

Details on this implementation can be found here:

![AudioReactive Details](images/audioreactive-sample-details.png)

## FPS
Sample that uses Unity's [MicroFPS sample](https://learn.unity.com/project/fps-template) to demonstrate gameplay across the cluster display.

![MicroFPS Gif](images/microfps-sample.gif)

## Fish
This sample demonstrates how you can implement a custom determinsitic physics based particle system using DOTS. In this case you can add fish to a boid system and feed them food.

![Fish GIF](images/fish-sample.gif)

## Skinned VFX

![Skinned VFX Gif](images/skinnedvfx-sample.gif)

## Timeline Sync

![TimlineSync Gif](images/timelinesync-sample.gif)

## UI

![UI GIF](images/ui-sample.gif)

## Transform Hierarchy Streaming

![UI GIF](images/transformhierarchystreaming-sample.gif)