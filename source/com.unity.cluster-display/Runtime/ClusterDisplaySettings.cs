using UnityEngine;

namespace Unity.ClusterDisplay
{
    class ClusterDisplaySettings : ScriptableObject
    {
        const string k_AssetName = "ClusterDisplaySettings";

        [SerializeField]
        bool m_EnableOnPlay = true;

        [SerializeField]
        ClusterParams m_ClusterParams;

        public bool EnableOnPlay => m_EnableOnPlay;

        public ClusterParams ClusterParams => m_ClusterParams;

        public static ClusterDisplaySettings CurrentSettings
        {
            get
            {
                ClusterDisplaySettings settings = Resources.Load<ClusterDisplaySettings>(k_AssetName);
                if (settings == null)
                {
                    settings = CreateInstance<ClusterDisplaySettings>();
                    settings.m_ClusterParams = ClusterParams.Default;
#if UNITY_EDITOR
                    if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/Resources"))
                    {
                        UnityEditor.AssetDatabase.CreateFolder("Assets", "Resources");
                    }
                    UnityEditor.AssetDatabase.CreateAsset(settings, $"Assets/Resources/{k_AssetName}.asset");
                    UnityEditor.AssetDatabase.SaveAssets();
#endif
                }
                return settings;
            }
        }
    }
}
