# About Cluster Display Graphics Tests

Graphics tests typically rely on reference images. For Cluster Display, we do not use reference images. We render multiple frames with different settings and compare results against each other.

## Existing Tests

* `001_FullScreenUV`: validates our cluster space approach by comparing vanilla output with stitcher output. The idea being that using the cluster renderer and the stitcher, you should see the same thing as if there was no cluster rendering at all.

* `002_OverscanDoesNotDistort`: validates that overscan does not cause distortion or stretching on the visible output.

* `003_OverscanCropsProperly`: we turn overscan to zero and add a border along the edges of the overscanned target. We then increase overscan, and increase the border so that its visible part in the final cropped output remains the same.

* `004_BezelDoesNotDistort`: compares vanilla output to stitched output with a bezel. There will be black borders on the stitched output, representing the bezel. Outside of these borders, all other pixels should match.

## Next Steps

We have introduced some pixel processing methods who would benefit from being `burst compiled`:

* `IsMonochromeStochastic`: whether or not an image is monochrome based on a provided number of random samples. Intended to validate an image is not monochromatic, best suited for outputs with gradients.

* `IsNotMonochrome`: more expensive than the above method, will process pixels until a color change has been detected. Cheaper to compute than `IsMonochrome` which would imply always processing all pixels.

* `NonZeroPixelsAreEqual`: as the name suggests, performs an image comparison ignoring empty pixels. Useful for validating bezels.


