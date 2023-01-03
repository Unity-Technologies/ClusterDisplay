using UnityEngine;

namespace Unity.ClusterDisplay.Utils
{
    abstract class ProjectSettings<T> : ScriptableObject where T : ProjectSettings<T>
    {
        const string k_AssetName = nameof(T);

        protected abstract void InitializeInstance();

        public static T Current
        {
            get
            {
                var settings = Resources.Load<T>(k_AssetName);
                if (settings == null)
                {
                    settings = CreateInstance<T>();
                    settings.InitializeInstance();
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
