using System.Collections;
using System.Collections.Generic;
using Unity.ClusterDisplay;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Text))]
public class AnimatorStatusUI : MonoBehaviour
{
    private Text textUI;

    private void Start()
    {
        if (textUI == null)
        {
            textUI = GetComponent<Text>();
            if (textUI == null)
                return;

            textUI.text = $"Animator {(ClusterDisplayState.IsEmitter ? "Enabled" : "Disabled")}";
            textUI.color = ClusterDisplayState.IsEmitter ? Color.green : Color.red;
        }
    }
}
