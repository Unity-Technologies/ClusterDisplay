﻿using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.ClusterDisplay.Graphics
{
    /// <summary>
    /// The purpose of this class is to provide a registry which cameras will register them selves when
    /// OnCameraRender is called. Cameras registered here are automatically accessed by ClusterDisplayRenderer
    /// and this registry manages it's camera context.
    /// </summary>
    public class CameraContextRegistry : SingletonMonoBehaviour<CameraContextRegistry>, ISerializationCallbackReceiver
    {
        private readonly Dictionary<Camera, CameraContextTarget> k_CameraContextTargets = new Dictionary<Camera, CameraContextTarget>();
        [HideInInspector][SerializeField] private CameraContextTarget[] m_SerializedCameraContextTargets;

        [HideInInspector] [SerializeField] private CameraContextTarget m_FocusedCameraContextTarget;
        [HideInInspector] [SerializeField] private CameraContextTarget m_PreviousFocusedCameraContextTarget;

        /// <summary>
        /// The current camera that's rendering.
        /// </summary>
        public bool TryGetFocusedCameraContextTarget (out CameraContextTarget cameraContextTarget)
        {
            if (m_FocusedCameraContextTarget == null)
            {
                if (k_CameraContextTargets.Count != 0)
                {
                    var first = k_CameraContextTargets.FirstOrDefault();
                    SetFocusedCameraContextTarget(first.Value);
                }

                else
                {
                    PollCameraTargets();
                    if (k_CameraContextTargets.Count > 0)
                    {
                        var first = k_CameraContextTargets.FirstOrDefault();
                        SetFocusedCameraContextTarget(first.Value);
                    }
                }
            }

            return (cameraContextTarget = m_FocusedCameraContextTarget) != null;
        }

        public void SetFocusedCameraContextTarget (CameraContextTarget cameraContextTarget)
        {
            if (cameraContextTarget == m_FocusedCameraContextTarget)
                return;

            if (cameraContextTarget != null)
                Debug.Log($"Changing camera context to: \"{cameraContextTarget.gameObject.name}\".");
            else
                Debug.Log($"Changing camera context to: \"NULL\".");

            SetPreviousFocusedCameraContextTarget(m_FocusedCameraContextTarget);
            m_FocusedCameraContextTarget = cameraContextTarget;
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

        public bool TryGetPreviousFocusedCameraContextTarget (out CameraContextTarget previousCameraContextTarget) => (previousCameraContextTarget = m_PreviousFocusedCameraContextTarget) != null;
        public void SetPreviousFocusedCameraContextTarget (CameraContextTarget previousCameraContextTarget) => m_PreviousFocusedCameraContextTarget = previousCameraContextTarget;

        public CameraContextTarget[] cameraContextTargets => k_CameraContextTargets.Values.ToArray();

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

            cameraContextTarget = camera.gameObject.GetComponent<CameraContextTarget>();
            if (cameraContextTarget == null)
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

        public void Flush ()
        {
            k_CameraContextTargets.Clear();
            m_SerializedCameraContextTargets = null;

            m_FocusedCameraContextTarget = null;
            m_PreviousFocusedCameraContextTarget = null;
        }
    }
}