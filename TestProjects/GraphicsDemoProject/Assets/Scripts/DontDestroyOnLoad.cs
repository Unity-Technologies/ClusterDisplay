using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DontDestroyOnLoad : MonoBehaviour
{
    private void OnEnable()
    {
        DontDestroyOnLoad(gameObject);
    }
}
