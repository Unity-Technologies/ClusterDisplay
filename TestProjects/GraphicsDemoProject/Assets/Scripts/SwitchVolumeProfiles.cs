using System;
using Unity.ClusterRendering.Toolkit;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
[RequireComponent(typeof(Volume))]
public class SwitchVolumeProfiles : MonoBehaviour
{
    [SerializeField]
    VolumeProfile[] m_Profiles;

    Volume m_Volume;
    string[] m_CachedEntryNames;
    int m_Selected;

    void OnEnable()
    {
        m_Volume = GetComponent<Volume>();
        m_Selected = m_Profiles == null ? -1 : 0;
        UpdateCache();
        UpdateProfile();
    }

    void OnValidate()
    {
        UpdateCache();
    }

    // introduced keyboard controls to make up for lack of imgui support with cluster display
    void Update()
    {
        var selected = m_Selected;
        if (Input.GetKey(KeyCode.P))
        {
            if (Input.GetKeyDown(KeyCode.RightArrow))
                ++selected;
            else if (Input.GetKeyDown(KeyCode.LeftArrow))
                --selected;
        }

        if (m_Profiles != null && selected != m_Selected)
        {
            m_Selected = (selected + m_Profiles.Length) % m_Profiles.Length;
            UpdateProfile();
        }
    }

    void UpdateCache()
    {
        if (m_Profiles == null)
        {
            m_CachedEntryNames = null;
            m_Selected = -1;
            return;
        }

        m_CachedEntryNames = new string[m_Profiles.Length];
        for (var i = 0; i < m_Profiles.Length; i++)
        {
            m_CachedEntryNames[i] = m_Profiles[i].name;
        }
    }

    void UpdateProfile()
    {
        if (m_Selected != -1)
            m_Volume.profile = m_Profiles[m_Selected];
        else
            m_Volume.profile = null;
    }

    public void DrawGUI()
    {
        if (m_Profiles == null)
            return;

        GUILayout.Label("<b>Post Effects</b>");
        GUILayout.Label("Press <b>[P]</b> then use <b>left/right</b> arrows to swap effects");
        var prevSelected = m_Selected;
        m_Selected = GUILayout.SelectionGrid(prevSelected, m_CachedEntryNames, 2);
        if (m_Selected != prevSelected)
            UpdateProfile();
    }
}
