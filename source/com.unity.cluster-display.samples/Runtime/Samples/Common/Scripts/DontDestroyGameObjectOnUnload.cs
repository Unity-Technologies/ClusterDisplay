using UnityEngine;

public class DontDestroyGameObjectOnUnload : MonoBehaviour
{
    private void Awake() =>
        DontDestroyOnLoad(gameObject);
}
