using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AdditiveSceneManager : MonoBehaviour
{
    [SerializeField]
    string m_SceneName;

    [SerializeField]
    float m_Delay;

    bool m_Loaded;
    
    void OnEnable()
    {
        m_Loaded = false;
        StartCoroutine(LoadScene());
    }

    void OnDisable()
    {
        StopAllCoroutines();
        if (m_Loaded) 
            SceneManager.UnloadSceneAsync(m_SceneName, UnloadSceneOptions.UnloadAllEmbeddedSceneObjects);
    }

    IEnumerator LoadScene()
    {
        for (;;)
        {
            yield return new WaitForSeconds(m_Delay);

            if (m_Loaded)
            {
                SceneManager.UnloadSceneAsync(m_SceneName, UnloadSceneOptions.UnloadAllEmbeddedSceneObjects);
                m_Loaded = false;
            }
            else
            {
                m_Loaded = true;
                SceneManager.LoadScene(m_SceneName, LoadSceneMode.Additive);
            }
        }
    }
}


