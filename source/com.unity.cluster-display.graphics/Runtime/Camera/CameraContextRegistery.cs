using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

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

            var cameraContextTargets = cameraContextRegistry.CameraContextTargets;
            for (int i = 0; i < cameraContextTargets.Length; i++)
                EditorGUILayout.LabelField(cameraContextTargets[i].gameObject.name);
        }
    }
    #endif

    private readonly Dictionary<Camera, CameraContextTarget> m_CameraContextTargets = new Dictionary<Camera, CameraContextTarget>();
    [HideInInspector][SerializeField] private CameraContextTarget[] m_SerializedCameraContextTargets;

    [HideInInspector] [SerializeField] private CameraContextTarget m_FocusedCameraContextTarget;
    [HideInInspector] [SerializeField] private CameraContextTarget m_PreviousFocusedCameraContextTarget;
    public CameraContextTarget FocusedCameraContextTarget
    {
        get
        {
            if (m_FocusedCameraContextTarget == null)
            {
                if (m_CameraContextTargets.Count != 0)
                {
                    var first = m_CameraContextTargets.FirstOrDefault();
                    FocusedCameraContextTarget = first.Value;
                }

                else
                {
                    PollCameraTargets();
                    if (m_CameraContextTargets.Count > 0)
                    {
                        var first = m_CameraContextTargets.FirstOrDefault();
                        FocusedCameraContextTarget = first.Value;
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

            PreviousFocusedCameraContextTarget = m_FocusedCameraContextTarget;
            m_FocusedCameraContextTarget = value;
        }
    }

    public string m_TargetCameraTag = "MainCamera";
    public static string TargetCameraTag
    {
        get
        {
            if (!TryGetInstance(out var instance))
                return null;
            return instance.m_TargetCameraTag;
        }
    }

    public static bool CanChangeContextTo (Camera camera) => camera.cameraType == CameraType.Game && camera.gameObject.tag == TargetCameraTag;

    public CameraContextTarget PreviousFocusedCameraContextTarget
    {
        get => m_PreviousFocusedCameraContextTarget;
        private set => m_PreviousFocusedCameraContextTarget = value;
    }

    private CameraContextTarget[] CameraContextTargets => m_CameraContextTargets.Values.ToArray();

    public bool TryGetCameraContextTarget (Camera camera, out CameraContextTarget cameraContextTarget)
    {
        if (m_CameraContextTargets.TryGetValue(camera, out cameraContextTarget))
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

            if (m_CameraContextTargets.ContainsKey(camera))
                continue;

            Register(camera);
        }
    }

    private void OnLevelWasLoaded(int level) => PollCameraTargets();

    public CameraContextTarget Register (Camera camera)
    {
        if (m_CameraContextTargets.ContainsKey(camera))
        {
            Debug.LogError($"Cannot register {nameof(CameraContextTarget)}: \"{camera.gameObject.name}\", it was already registered.");
            return null;
        }

        CameraContextTarget cameraContextTarget = null;
        if ((cameraContextTarget = camera.gameObject.GetComponent<CameraContextTarget>()) == null)
            cameraContextTarget = camera.gameObject.AddComponent<CameraContextTarget>();
        m_CameraContextTargets.Add(camera, cameraContextTarget);
        return cameraContextTarget;
    }

    public void UnRegister (CameraContextTarget cameraContextTarget, bool destroy = false)
    {
        if (destroy || !cameraContextTarget.TryGetCamera(out var camera))
        {
            DestroyCameraContextTarget(cameraContextTarget);
            return;
        }

        if (!m_CameraContextTargets.ContainsKey(camera))
        {
            Debug.LogError($"Cannot unregister {nameof(CameraContextTarget)}: \"{cameraContextTarget.gameObject.name}\", it was never registered.");
            return;
        }

        m_CameraContextTargets.Remove(camera);
    }

    public void OnAfterDeserialize()
    {
        if (m_SerializedCameraContextTargets == null)
            return;

        for (int i = 0; i < m_SerializedCameraContextTargets.Length; i++)
        {
            if (m_SerializedCameraContextTargets[i] == null)
                continue;

            if (!m_SerializedCameraContextTargets[i].CameraReferenceIsValid)
                continue;

            m_CameraContextTargets.Add(m_SerializedCameraContextTargets[i].TargetCamera, m_SerializedCameraContextTargets[i]);
        }
    }

    public void OnBeforeSerialize()
    {
        int validCameraContextCount = 0;
        foreach (var cameraContextPair in m_CameraContextTargets)
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
        foreach (var cameraContextPair in m_CameraContextTargets)
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
        m_CameraContextTargets.Clear();
        m_SerializedCameraContextTargets = null;

        m_FocusedCameraContextTarget = null;
        m_PreviousFocusedCameraContextTarget = null;
    }
}
