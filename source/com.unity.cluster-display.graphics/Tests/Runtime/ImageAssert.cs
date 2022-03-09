using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Unity.ClusterDisplay.Graphics.Tests
{
    /// <summary>
    /// This class provides image comparison utilities.
    /// </summary>
    /// <remarks>
    /// Most of the code is borrowed from UnityEngine.TestTools.Graphics.
    /// We had to modify it due to the unusual way in which we use these tools.
    /// We typically compare on-the-fly generated against each other.
    /// (As opposed to comparing a generated image against one stored as an asset.)
    /// We also test that images are not equal in some instances.
    /// (While the original version only expects images asserted to be equal.)
    /// </remarks>>
    public class ImageAssert
    {
        const int k_BatchSize = 1024;

        struct FailedImageMessage
        {
            public string Directory;
            public string ImageName;
            public Texture2D ExpectedImage;
            public Texture2D ActualImage;
            public Texture2D DiffImage;
        }

        // Just for readability.
        public static void AreEqual(Texture2D expected, Texture2D actual, ImageComparisonSettings settings, string imageSuffix = null)
        {
            CompareImages(expected, actual, settings, true, imageSuffix);
        }

        public static void AreNotEqual(Texture2D expected, Texture2D actual, ImageComparisonSettings settings, string imageSuffix = null)
        {
            CompareImages(expected, actual, settings, false, imageSuffix);
        }

        /// <summary>
        /// Compares two images.
        /// </summary>
        /// <param name="expected">What the image is supposed to look like.</param>
        /// <param name="actual">What the image actually looks like.</param>
        /// <param name="settings">Settings that control how the comparison is performed.</param>
        /// <param name="expectEquality">If true, we expect images to be equal.</param>
        /// <param name="imageSuffix">Optional, suffix appended to the image name</param>
        static void CompareImages(Texture2D expected, Texture2D actual, ImageComparisonSettings settings, bool expectEquality, string imageSuffix = null)
        {
            if (expected == null)
            {
                throw new ArgumentNullException(nameof(expected));
            }

            if (actual == null)
            {
                throw new ArgumentNullException(nameof(actual));
            }

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            // We retain similar paths to the Graphics Tests Framework.
            // Note that we do not care about XR so use "None".
            // We're also not concerned about Windows Store Apps.
            // We directly use the values exposed by UseGraphicsTestCasesAttribute in the GFX Test Framework.
            var failedImageMessage = new FailedImageMessage
            {
                Directory = Path.Combine("Assets/ActualImages", $"{QualitySettings.activeColorSpace}/{Application.platform.ToString()}/{SystemInfo.graphicsDeviceType}/None"),
                ImageName = StripParametricTestCharacters(imageSuffix == null ? TestContext.CurrentContext.Test.Name : $"{TestContext.CurrentContext.Test.Name}_{imageSuffix}")
            };

            try
            {
                Assert.That(expected, Is.Not.Null, "No reference image was provided.");

                Assert.That(actual.width, Is.EqualTo(expected.width),
                    "The expected image had width {0}px, but the actual image had width {1}px.", expected.width,
                    actual.width);
                Assert.That(actual.height, Is.EqualTo(expected.height),
                    "The expected image had height {0}px, but the actual image had height {1}px.", expected.height,
                    actual.height);

                Assert.That(actual.format, Is.EqualTo(expected.format),
                    "The expected image had format {0} but the actual image had format {1}.", expected.format,
                    actual.format);

                using (var expectedPixels = new NativeArray<Color32>(expected.GetPixels32(0), Allocator.TempJob))
                using (var actualPixels = new NativeArray<Color32>(actual.GetPixels32(0), Allocator.TempJob))
                using (var diffPixels = new NativeArray<Color32>(expectedPixels.Length, Allocator.TempJob))
                using (var sumOverThreshold = new NativeArray<float>(Mathf.CeilToInt(expectedPixels.Length / (float)k_BatchSize), Allocator.TempJob))
                {
                    new ComputeDiffJob
                    {
                        expected = expectedPixels,
                        actual = actualPixels,
                        diff = diffPixels,
                        sumOverThreshold = sumOverThreshold,
                        deltaEThreshold = settings.PerPixelCorrectnessThreshold,
                    }.Schedule(expectedPixels.Length, k_BatchSize).Complete();

                    var pixelCount = expected.width * expected.height;
                    var averageDeltaE = sumOverThreshold.Sum() / pixelCount;

                    try
                    {
                        if (expectEquality)
                        {
                            Assert.That(averageDeltaE, Is.LessThanOrEqualTo(settings.AverageCorrectnessThreshold));
                        }
                        else
                        {
                            Assert.That(averageDeltaE, Is.GreaterThan(settings.AverageCorrectnessThreshold));
                        }
                    }
                    catch (AssertionException)
                    {
                        var diffImage = new Texture2D(expected.width, expected.height, TextureFormat.RGB24, false);
                        var diffPixelsArray = new Color32[expected.width * expected.height];
                        diffPixels.CopyTo(diffPixelsArray);
                        diffImage.SetPixels32(diffPixelsArray, 0);
                        diffImage.Apply(false);

                        failedImageMessage.DiffImage = diffImage;
                        failedImageMessage.ExpectedImage = expected;
                        throw;
                    }
                }
            }
            catch (AssertionException)
            {
                failedImageMessage.ActualImage = actual;
#if UNITY_EDITOR
                var actualImageName = $"{failedImageMessage.ImageName}.actual.png";
                var expectedImageName = $"{failedImageMessage.ImageName}.expected.png";
                var diffImageName = $"{failedImageMessage.ImageName}.diff.png";

                GraphicsTestUtil.SaveAsPNG(failedImageMessage.ActualImage, failedImageMessage.Directory, actualImageName);
                GraphicsTestUtil.SaveAsPNG(failedImageMessage.ExpectedImage, failedImageMessage.Directory, expectedImageName);
                GraphicsTestUtil.SaveAsPNG(failedImageMessage.DiffImage, failedImageMessage.Directory, diffImageName);

                GraphicsTestUtil.ReportArtifact(Path.Combine(failedImageMessage.Directory, actualImageName));
                GraphicsTestUtil.ReportArtifact(Path.Combine(failedImageMessage.Directory, expectedImageName));
                GraphicsTestUtil.ReportArtifact(Path.Combine(failedImageMessage.Directory, diffImageName));
#endif
                throw;
            }
        }

        struct ComputeDiffJob : IJobParallelFor
        {
            [ReadOnly]
            public NativeArray<Color32> expected;
            [ReadOnly]
            public NativeArray<Color32> actual;
            public NativeArray<Color32> diff;

            public float deltaEThreshold;

            [NativeDisableParallelForRestriction]
            public NativeArray<float> sumOverThreshold;

            public void Execute(int index)
            {
                var exp = expected[index];
                var act = actual[index];
                var batch = index / k_BatchSize;

                var deltaE = JABDeltaE(RGBtoJAB(exp), RGBtoJAB(act));
                var deltaEOverThreshold = Mathf.Max(0f, deltaE - deltaEThreshold);
                sumOverThreshold[batch] = sumOverThreshold[batch] + deltaEOverThreshold;

                // deltaE is linear, convert it to sRGB for easier debugging
                deltaE = Mathf.LinearToGammaSpace(deltaE);
                var colorResult = new Color(deltaE, deltaE, deltaE, 1f);
                diff[index] = colorResult;
            }
        }

        // Linear RGB to XYZ using D65 ref. white
        static Vector3 RGBtoXYZ(Color color)
        {
            var x = color.r * 0.4124564f + color.g * 0.3575761f + color.b * 0.1804375f;
            var y = color.r * 0.2126729f + color.g * 0.7151522f + color.b * 0.0721750f;
            var z = color.r * 0.0193339f + color.g * 0.1191920f + color.b * 0.9503041f;
            return new Vector3(x * 100f, y * 100f, z * 100f);
        }

        // sRGB to JzAzBz
        // https://www.osapublishing.org/oe/fulltext.cfm?uri=oe-25-13-15131&id=368272
        static Vector3 RGBtoJAB(Color color)
        {
            var xyz = RGBtoXYZ(color.linear);

            const float kB = 1.15f;
            const float kG = 0.66f;
            const float kC1 = 0.8359375f; // 3424 / 2^12
            const float kC2 = 18.8515625f; // 2413 / 2^7
            const float kC3 = 18.6875f; // 2392 / 2^7
            const float kN = 0.15930175781f; // 2610 / 2^14
            const float kP = 134.034375f; // 1.7 * 2523 / 2^5
            const float kD = -0.56f;
            const float kD0 = 1.6295499532821566E-11f;

            var x2 = kB * xyz.x - (kB - 1f) * xyz.z;
            var y2 = kG * xyz.y - (kG - 1f) * xyz.x;

            var l = 0.41478372f * x2 + 0.579999f * y2 + 0.0146480f * xyz.z;
            var m = -0.2015100f * x2 + 1.120649f * y2 + 0.0531008f * xyz.z;
            var s = -0.0166008f * x2 + 0.264800f * y2 + 0.6684799f * xyz.z;
            l = Mathf.Pow(l / 10000f, kN);
            m = Mathf.Pow(m / 10000f, kN);
            s = Mathf.Pow(s / 10000f, kN);

            // Can we switch to unity.mathematics yet?
            var lms = new Vector3(l, m, s);
            var a = new Vector3(kC1, kC1, kC1) + kC2 * lms;
            var b = Vector3.one + kC3 * lms;
            var tmp = new Vector3(a.x / b.x, a.y / b.y, a.z / b.z);

            lms.x = Mathf.Pow(tmp.x, kP);
            lms.y = Mathf.Pow(tmp.y, kP);
            lms.z = Mathf.Pow(tmp.z, kP);

            var jab = new Vector3(
                0.5f * lms.x + 0.5f * lms.y,
                3.524000f * lms.x + -4.066708f * lms.y + 0.542708f * lms.z,
                0.199076f * lms.x + 1.096799f * lms.y + -1.295875f * lms.z
            );

            jab.x = ((1f + kD) * jab.x) / (1f + kD * jab.x) - kD0;

            return jab;
        }

        static float JABDeltaE(Vector3 v1, Vector3 v2)
        {
            var c1 = Mathf.Sqrt(v1.y * v1.y + v1.z * v1.z);
            var c2 = Mathf.Sqrt(v2.y * v2.y + v2.z * v2.z);

            var h1 = Mathf.Atan(v1.z / v1.y);
            var h2 = Mathf.Atan(v2.z / v2.y);

            var deltaH = 2f * Mathf.Sqrt(c1 * c2) * Mathf.Sin((h1 - h2) / 2f);
            var deltaE = Mathf.Sqrt(Mathf.Pow(v1.x - v2.x, 2f) + Mathf.Pow(c1 - c2, 2f) + deltaH * deltaH);
            return deltaE;
        }

        static string StripParametricTestCharacters(string name)
        {
            {
                string illegal = "\"";
                int found = name.IndexOf(illegal);
                while (found >= 0)
                {
                    name = name.Remove(found, 1);
                    found = name.IndexOf(illegal);
                }
            }
            {
                string illegal = ",";
                name = name.Replace(illegal, "-");
            }
            {
                string illegal = "(";
                name = name.Replace(illegal, "_");
            }
            {
                string illegal = ")";
                name = name.Replace(illegal, "_");
            }
            return name;
        }
    }
}
