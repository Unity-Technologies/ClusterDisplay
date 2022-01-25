# Troubleshooting

## QuadroSync is not Working
Quadro Sync can be difficult to setup correctly. See [this](quadro-sync.md) page for trouble shooting steps.

## Screen is Black in URP
You may need to perform the following:
1. **If you are using URP**, verify that the following is toggled on your **"Universal Render Pipeline Asset"**:

    ![URP Texture Settings](images/urp-texture-setting.png)

2. **If you are using URP**, add the following rendering feature to your **"Forward Renderer"**

    ![URP Render Feature](images/urp-render-feature.png)

3. Find the camera you want to use in the scene and add a **ClusterCamera** component to it:

    ![Cluster Display Prefab](images/cluster-camera.png)

4. If your camera was disabled, enable it and it will start rendering after it disables itself.

## Compile Time Errors in GeneratedInspectors.cs 
This can be a common issue while changing Unity versions, updating packages or removing packages, and you can read about the solution here:
* [How to Generate Them](network-events#how-to-generate-them)