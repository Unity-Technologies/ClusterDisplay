using System.Collections.Generic;

public class Utils
{
    public static IEnumerable<string> VolumeProfileNames
    {
        get
        {
            yield return "Bloom";
            yield return "ChromaticAberration";
            yield return "CustomPostProcess";
            yield return "FilmGrain";
            yield return "LensDistortion";
            yield return "Vignette";
        }
    }

    // Note that FilmGrain is not in this list. Its aspect changes with overscan which is ok.
    // The alternative would be to assume the provided grain texture tiles seamlessly which is not guaranteed.
    public static IEnumerable<string> VolumeProfileOverscanSupportNames
    {
        get
        {
            yield return "Bloom";
            yield return "ChromaticAberration";
            yield return "CustomPostProcess";
            yield return "LensDistortion";
            yield return "Vignette";
        }
    }
}
