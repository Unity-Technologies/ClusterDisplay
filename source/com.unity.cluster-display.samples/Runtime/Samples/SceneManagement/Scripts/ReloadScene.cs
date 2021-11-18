using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ReloadScene : MonoBehaviour
{
    private IEnumerator Reload()
    {
        var wait = new WaitForSeconds(4);
        while (true)
        {
            yield return wait;
            SceneManager.LoadScene(0, LoadSceneMode.Single);
        }
    }
}
