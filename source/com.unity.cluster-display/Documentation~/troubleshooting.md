[Contents](TableOfContents.md) | [Home](index.md) > Troubleshooting

# Troubleshooting
- [VFX Graph Particles - reseed issues](#vfx-graph-particles-reseed-issues)
- [Screen is Black in URP](#screen-is-black-in-urp)
- [QuadroSync is not Working](#quadrosync-is-not-working)
- [I Need to Debug Something](#i-need-to-debug-something)
- [Cluster Timesout After Period](#cluster-timesout-after-period)
- [Post-processing effects don't look right in the player when using Tiled Projection Policy](#post-processing-effects-dont-look-right-in-the-player-when-using-tiled-projection-policy)

## VFX Graph Particles - reseed issues

If you are using VFX Graph Particles effects in HDRP, make sure to *disable* the **Reseed on play** option (checkbox) on the **VisualEffect** component, otherwise each node may end up with differently seeded random number generators, leading to visual artefacts.

![](images/component-visual-effect.png)

## Screen is Black in URP
You may need to perform the following:
1. **If you are using URP**, verify that the following is toggled on your **"Universal Render Pipeline Asset"**:

    ![URP Texture Settings](images/urp-texture-setting.png)

2. **If you are using URP**, add the following rendering feature to your **"Forward Renderer"**

    ![URP Render Feature](images/urp-render-feature.png)

3. Find the camera you want to use in the scene and add a **ClusterCamera** component to it:

    ![Cluster Display Prefab](images/cluster-camera.png)

4. If your camera was disabled, enable it and it will start rendering after it disables itself.

## QuadroSync is not Working
Quadro Sync can be difficult to setup correctly. See [this](quadro-sync.md) page for trouble shooting steps.

## I Need to Debug Something
Include the **CLUSTER_DISPLAY_VERBOSE_LOGGING** scripting define symbol in the player settings to get verbose logging. Refer to the [Unity Editor documentation](https://docs.unity3d.com/Manual/LogFiles.html) to know where those logs are located.

![Verbose Logging](images/verbose-logging.png)

## Cluster Times Out After Period
Routers & switches periodically propagate membership query to all members of a multicast group. This is done to determine whether a multicast group should expire or not. Some enterprise routers and switches will NOT do this by default and automatically expire multicast group after a period of time essentially preventing nodes inside the cluster from communicating with each other.

In order to resolve this, you will need to configure your [IGMP](https://en.wikipedia.org/wiki/Internet_Group_Management_Protocol) settings in your router/switch so it will not expire those multicast groups. These settings are different for every switch manufacturer, so you will need to search your hardware's manual and settings interface for these settings.

## Post-processing effects don't look right in the player when using Tiled Projection Policy

Certain post-processing effects, such as Vignette, Lens Distortion, and Chromatic Abberation, use "screen coord override" shader variants to account the cluster grid. Make sure that "Strip Screen Coord Override Variants" is disabled in your graphics settings.

![](images/shader-stripping.png)
