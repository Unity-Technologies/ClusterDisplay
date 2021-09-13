# Cluster Display Samples
We've provided a set of sample projects to demonstrate how you may use Cluster Display in your own projects, the samples include:

## How to Test
There are two methods you can use to test:
1. Open the scenes from two separate instances of the Unity editor to play.
2. Stage the scenes and build from one instance of the Unity editor and start two instances of the resulting executable.

### Setup for Testing in the Editor
1. Run **CreateNewProject.ps1** and request the script to create a **Samples** project:

    ![Scene Composition Manager](images/create-samples-project.png)

2. Open the created projects from the Unity Hub:

    ![Scene Composition Manager](images/samples-open-hub.png)

3. Use Start + (Right/Left) arrow to place both editor instances side by side.

    ![Scene Composition Manager](images/cluster-display-layout-1.gif)

4. By default, the project will be opened in the cluster display layout. However, you may need to drag the game window to the other side of the editor window and scale it accordingly.

    ![Scene Composition Manager](images/cluster-display-layout-2.gif)

5. You can open each sample via the scene composition manager:

    ![Scene Composition Manager](images/scene-composition-manager.png)

6. Press "Play as Emitter" on the emitter editor instance and "Play as Repeater" on the repeater editor instance:

    ![Scene Composition Manager](images/samples-test-play-0.png)
    ![Scene Composition Manager](images/samples-test-play-1.png)

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