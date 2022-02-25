# Graphics
Cluster display supports both URP (Universal Render Pipeline) and HDRP (High Definition Render Pipeline).

Until our teams contributions are accepted into the trunk of [Unity's Graphics repository](https://github.com/Unity-Technologies/Graphics), you will be using a [branch](https://github.com/Unity-Technologies/Graphics/tree/cluster-display/etienne%2Fupgrade-test) of this repository.

The repository for cluster display should already contain copies of these branched graphics packages and you can follow the following instructions to setup references to these packages in an existing project [here](setup-existing-project.md).


## Cluster Display Renderer Features
### Overscan
Overscan renders a larger portion of your camera so that post processing can bleed properly beyond the viewport then crops down the render so that the render overlaps correctly between nodes.

![Overscan](images/overscan.png)

## Supported (HDRP/URP) Features

### Post Processing
Most prost processing features are supported. However, you will need to use the overscan setting to tweak your overlaps. 

Most features throughout both render pipelines are supported. However, there are a few noteable edge cases.

## HDRP

* The following post processing effects are unsupported:
    * Automatic camera exposure.
    * Motion Blur
    * Volumetric Clouds

## URP
* The following post processing effects are unsupported:
    * Motion Blur

Both HDRP and URP are large and complex. Therefore, if you discover additional broken features that you feel should be supported, contact us!