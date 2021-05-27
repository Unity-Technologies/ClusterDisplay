using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Unity.ClusterDisplay.Graphics
{
    /// <summary>
    /// The purpose of this class is to provide a registry which cameras will register them selves when
    /// OnCameraRender is called. Cameras registered here are automatically accessed by ClusterDisplayRenderer
    /// and this registry manages it's camera context.
    /// </summary>
    public class CameraContextRegistery : SingletonMonoBehaviour<CameraContextRegistery>, ISerializationCallbackReceiver
    {
        #if UNITY_EDITOR
        [CustomEditor(typeof(CameraContextRegistery))]
        private class CameraContextRegistryEditor : Editor
        {
            public override void OnInspectorGUI()
            {
                base.OnInspectorGUI();

                var cameraContextRegistry = target as CameraContextRegistery;
                if (cameraContextRegistry == null)
                    return;

                if (GUILayout.Button("Flush Registry"))
                    cameraContextRegistry.Flush();

                var cameraContextTargets = cameraContextRegistry.cameraContextTargets;
                for (int i = 0; i < cameraContextTargets.Length; i++)
                    EditorGUILayout.LabelField(cameraContextTargets[i].gameObject.name);
            }
        }
        #endif

        private readonly Dictionary<Camera, CameraContextTarget> k_CameraContextTargets = new Dictionary<Camera, CameraContextTarget>();
        [HideInInspector][SerializeField] private CameraContextTarget[] m_SerializedCameraContextTargets;

        [HideInInspector] [SerializeField] private CameraContextTarget m_FocusedCameraContextTarget;
        [HideInInspector] [SerializeField] private CameraContextTarget m_PreviousFocusedCameraContextTarget;

        /// <summary>
        /// The current camera that's rendering.
        /// </summary>
        public CameraContextTarget focusedCameraContextTarget
        {
            get
            {
                if (m_FocusedCameraContextTarget == null)
                {
                    if (k_CameraContextTargets.Count != 0)
                    {
                        var first = k_CameraContextTargets.FirstOrDefault();
                        focusedCameraContextTarget = first.Value;
                    }

                    else
                    {
                        PollCameraTargets();
                        if (k_CameraContextTargets.Count > 0)
                        {
                            var first = k_CameraContextTargets.FirstOrDefault();
                            focusedCameraContextTarget = first.Value;
                        }
                    }
                }

                return m_FocusedCameraContextTarget;
            }

            set
            {
                if (value == m_FocusedCameraContextTarget)
                    return;

                if (value != null)
                    Debug.Log($"Changing camera context to: \"{value.gameObject.name}\".");
                else Debug.Log($"Changing camera context to: \"NULL\".");

                previousFocusedCameraContextTarget = m_FocusedCameraContextTarget;
                m_FocusedCameraContextTarget = value;
            }
        }

        public string m_TargetCameraTag = "MainCamera";

        /// <summary>
        /// Only pay attention to cameras with this tag.
        /// </summary>
        public static string targetCameraTag
        {
            get
            {
                if (!TryGetInstance(out var instance))
                    return null;
                return instance.m_TargetCameraTag;
            }
        }

        public static bool CanChangeContextTo (Camera camera) => camera.cameraType == CameraType.Game && camera.gameObject.tag == targetCameraTag;

        public CameraContextTarget previousFocusedCameraContextTarget
        {
            get => m_PreviousFocusedCameraContextTarget;
            private set => m_PreviousFocusedCameraContextTarget = value;
        }

        private CameraContextTarget[] cameraContextTargets => k_CameraContextTargets.Values.ToArray();

        public bool TryGetCameraContextTarget (Camera camera, out CameraContextTarget cameraContextTarget)
        {
            if (k_CameraContextTargets.TryGetValue(camera, out cameraContextTarget))
                return true;
            cameraContextTarget = Register(camera);
            return cameraContextTarget;
        }

        private void PollCameraTargets ()
        {
            var cameraContextTargets = FindObjectsOfType<CameraContextTarget>();
            if (cameraContextTargets.Length == 0)
                return;

            for (int i = 0; i < cameraContextTargets.Length; i++)
            {
                if (!cameraContextTargets[i].TryGetCamera(out var camera))
                    continue;

                if (k_CameraContextTargets.ContainsKey(camera))
                    continue;

                Register(camera);
            }
        }

        /// <summary>
        /// When the level is loaded, automatically find all CameraContextTargets and register them.
        /// </summary>
        /// <param name="level"></param>
        private void OnLevelWasLoaded(int level) => PollCameraTargets();

        private void OnCameraEnabled(CameraContextTarget cameraContextTarget)
        {
            cameraContextTarget.TargetCamera.enabled = true;
        }

        private void OnCameraDisabled(CameraContextTarget cameraContextTarget)
        {
        }

        public CameraContextTarget Register (Camera camera, bool logError = true)
        {
            if (k_CameraContextTargets.ContainsKey(camera))
            {
                if (logError)
                    Debug.LogError($"Cannot register {nameof(CameraContextTarget)}: \"{camera.gameObject.name}\", it was already registered.");
                return null;
            }

            CameraContextTarget cameraContextTarget = null;
            if ((cameraContextTarget = camera.gameObject.GetComponent<CameraContextTarget>()) == null)
                cameraContextTarget = camera.gameObject.AddComponent<CameraContextTarget>();

            cameraContextTarget.onCameraEnabled += OnCameraEnabled;
            cameraContextTarget.onCameraDisabled += OnCameraDisabled;

            k_CameraContextTargets.Add(camera, cameraContextTarget);
            return cameraContextTarget;
        }

        public void UnRegister (CameraContextTarget cameraContextTarget, bool destroy = false)
        {
            cameraContextTarget.onCameraEnabled -= OnCameraEnabled;
            cameraContextTarget.onCameraDisabled -= OnCameraDisabled;

            if (destroy || !cameraContextTarget.TryGetCamera(out var camera))
            {
                DestroyCameraContextTarget(cameraContextTarget);
                return;
            }

            if (!k_CameraContextTargets.ContainsKey(camera))
            {
                Debug.LogError($"Cannot unregister {nameof(CameraContextTarget)}: \"{cameraContextTarget.gameObject.name}\", it was never registered.");
                return;
            }

            k_CameraContextTargets.Remove(camera);
        }

        public void OnAfterDeserialize()
        {
            if (m_SerializedCameraContextTargets == null)
                return;

            for (int i = 0; i < m_SerializedCameraContextTargets.Length; i++)
            {
                if (m_SerializedCameraContextTargets[i] == null)
                    continue;

                if (!m_SerializedCameraContextTargets[i].cameraReferenceIsValid)
                    continue;

                m_SerializedCameraContextTargets[i].onCameraEnabled += OnCameraEnabled;
                m_SerializedCameraContextTargets[i].onCameraDisabled += OnCameraDisabled;

                k_CameraContextTargets.Add(m_SerializedCameraContextTargets[i].TargetCamera, m_SerializedCameraContextTargets[i]);
            }
        }

        public void OnBeforeSerialize()
        {
            int validCameraContextCount = 0;
            foreach (var cameraContextPair in k_CameraContextTargets)
            {
                if (cameraContextPair.Key == null || cameraContextPair.Value == null)
                    continue;
                validCameraContextCount++;
            }

            if (validCameraContextCount == 0)
            {
                m_SerializedCameraContextTargets = null;
                return;
            }

            m_SerializedCameraContextTargets = new CameraContextTarget[validCameraContextCount];
            int cameraContextIndex = 0;
            foreach (var cameraContextPair in k_CameraContextTargets)
            {
                if (cameraContextPair.Value == null)
                    continue;

                m_SerializedCameraContextTargets[cameraContextIndex++] = cameraContextPair.Value;
            }
        }

        private void DestroyCameraContextTarget (CameraContextTarget cameraContextTarget)
        {
            if (!Application.isPlaying)
                Object.DestroyImmediate(cameraContextTarget);
            else Object.Destroy(cameraContextTarget);
        }

        private void Flush ()
        {
            k_CameraContextTargets.Clear();
            m_SerializedCameraContextTargets = null;

            m_FocusedCameraContextTarget = null;
            m_PreviousFocusedCameraContextTarget = null;
        }
    }
}
