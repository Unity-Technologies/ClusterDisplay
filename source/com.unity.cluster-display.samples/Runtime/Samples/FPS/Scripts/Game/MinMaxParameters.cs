using UnityEngine;

namespace Unity.FPS.Game
{
    [System.Serializable]
    public struct MinMaxFloat
    {
        public float Min;
        public float Max;

        public float GetValueFromRatio(float ratio)
        {
            return Mathf.Lerp(Min, Max, ratio);
        }
    }

    [System.Serializable]
    public struct MinMaxColor
    {
        [ColorUsage(true, true)] public Color Min;
        [ColorUsage(true, true)] public Color Max;

        public Color GetValueFromRatio(float ratio)
        {
            return Color.Lerp(Min, Max, ratio);
        }
    }

    [System.Serializable]
    public struct MinMaxVector3
    {
        public Vector3 Min;
        public Vector3 Max;

        public Vector3 GetValueFromRatio(float ratio)
        {
            return Vector3.Lerp(Min, Max, ratio);
        }
    }
}