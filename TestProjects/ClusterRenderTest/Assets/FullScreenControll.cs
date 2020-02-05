using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FullScreenControll : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        //Screen.SetResolution(1920,1080,FullScreenMode.FullScreenWindow);
        QualitySettings.vSyncCount = 1;
        QualitySettings.maxQueuedFrames = 1;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
