using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class RandomWrapper
{
    public static ulong callCount;
    private static bool printLog = true;

    private static Random.State ? localState;
    private static Random.State sharedState;

    public static void BeginTempState (int seed = 100)
    {
        sharedState = Random.state;
        if (!localState.HasValue)
            Random.InitState(seed);
        else Random.state = localState.Value;
    }

    public static void EndTempState ()
    {
        localState = Random.state;
        Random.state = sharedState;
    }

    public static float Range (float from, float to)
    {
        float v = Random.Range(from, to);
        if (printLog)
            Debug.Log($"Called Random.Range to receive value: {v}, this is the {++callCount}nth call to Random.x");
        return v;
    }

    public static float UntrackedRange (float from, float to)
    {
        float v = Random.Range(from, to);
        return v;
    }

    public static float value
    {
        get
        {
            float v = Random.value;
            if (printLog)
                Debug.Log($"Called Random.Range to receive value: {v}, this is the {++callCount}nth call to Random.x");
            return v;
        }
    }

    public static float untrackedValue
    {
        get
        {
            float v = Random.value;
            return v;
        }
    }
}
