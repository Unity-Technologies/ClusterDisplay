# Graphics
Cluster Display supports both Universal Render Pipeline 14.0.3 (URP) and High Definition Render Pipeline 14.0.3 (HDRP). They can be installed from the Package Manager in Unity 2022.2 and newer.

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
    * Automatic camera exposure
    * Volumetric Clouds

Both HDRP and URP are large and complex. Therefore, if you discover additional broken features that you feel should be supported, contact us!