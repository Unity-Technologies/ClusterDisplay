using NUnit.Framework;
using UnityEngine;

public class Utilities
{
    // TODO: Use Burst!
    // We use this method to verify images are *not* monochromatic so the stochastic process is sufficient
    static public bool IsMonochromeStochastic(Texture2D tex, int samples)
    {
        var pixels = tex.GetPixels();
        // pick a random pixel
        var color = pixels[(int)Random.Range(0, pixels.Length - 1)];
        // see if we get the same color picking pixels randomly
        for (var i = 0; i != samples; ++i)
        {
            var c = pixels[(int)Random.Range(0, pixels.Length - 1)];
            if (c != color)
                return false;
        }
        return true;
    }
    
    // TODO: Use Burst!
    // *Not* monochrome is cheaper to compute than monochrome.
    static public bool IsNotMonochrome(Texture2D tex)
    {
        var pixels = tex.GetPixels();
        // pick a random pixel
        var color = pixels[(int)Random.Range(0, pixels.Length - 1)];
        // see if we get the same color picking pixels randomly
        for (var i = 0; i != pixels.Length; ++i)
        {
            var c = pixels[i];
            if (c != color)
                return true;
        }
        return false;
    }

    static bool IsZero(Color c)
    {
        return c.r < Mathf.Epsilon && c.g < Mathf.Epsilon && c.b < Mathf.Epsilon;
    }

    // TODO: Use Burst!
    static public bool NonZeroPixelsAreEqual(Texture2D a, Texture2D b, float maxAbsoluteErrorPerPixel)
    {
        var pixelsA = a.GetPixels();
        var pixelsB = b.GetPixels();
        Assert.AreEqual(pixelsA.Length, pixelsB.Length);
        var error = 0f;
        for (var i = 0; i != pixelsA.Length; ++i)
        {
            var pa = pixelsA[i];
            if (IsZero(pa))
                continue;
            
            var pb = pixelsB[i];
            if (IsZero(pb))
                continue;

            error += Mathf.Abs(pa.r - pb.r) + Mathf.Abs(pa.g - pb.g) + Mathf.Abs(pa.b - pb.b);
        }
        return error < maxAbsoluteErrorPerPixel * pixelsA.Length;
    }
}

