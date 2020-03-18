using System.Collections;
using NUnit.Framework;
using UnityEngine;
using Unity.ClusterDisplay.Graphics;

public class BaseCustomTest : MonoBehaviour
{
    protected ClusterDisplayGraphicsTestSettings m_Settings;
    protected ClusterRenderer m_Renderer;
    protected Camera m_Camera;
    
    void OnEnable()
    {
        m_Camera = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Camera>();
        Assert.IsNotNull(m_Camera, "Invalid test scene, couldn't find MainCamera");
        m_Settings = Object.FindObjectOfType<ClusterDisplayGraphicsTestSettings>();
        Assert.IsNotNull(m_Settings, "Invalid test scene, couldn't find ClusterDisplayGraphicsTestSettings");
        m_Renderer = Object.FindObjectOfType<ClusterRenderer>();
        Assert.IsNotNull(m_Renderer, "Invalid test scene, couldn't find ClusterRenderer");
    }
    
    virtual public IEnumerator Execute()
    {
        yield break;
    }
}
