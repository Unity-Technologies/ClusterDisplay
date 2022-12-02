using UnityEngine;

namespace Unity.ClusterDisplay
{
    class ClusterDisplaySettings : ScriptableObject
    {
        const string k_AssetName = "ClusterDisplaySettings";

        [SerializeField]
        bool m_EnableOnPlay = true;

        public bool EnableOnPlay
        {
            get => m_EnableOnPlay;
            set => m_EnableOnPlay = value;
        }

        public static ClusterDisplaySettings CurrentSettings
        {
            get
            {
                ClusterDisplaySettings settings = Resources.Load<ClusterDisplaySettings>(k_AssetName);
#if UNITY_EDITOR
                if (settings == null)
                {
                    settings = CreateInstance<ClusterDisplaySettings>();
                    UnityEditor.AssetDatabase.CreateAsset(settings, $"Assets/Resources/{k_AssetName}.asset");
                    UnityEditor.AssetDatabase.SaveAssets();
                }
#endif
                return settings;
            }
        }
    }
}
