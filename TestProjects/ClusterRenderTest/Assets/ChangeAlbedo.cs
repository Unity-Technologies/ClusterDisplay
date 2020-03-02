using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ChangeAlbedo : MonoBehaviour
{
    private Material material;

    private static readonly int ColoProp = Shader.PropertyToID("_Color");

    // Start is called before the first frame update
    void Start()
    {
        var meshes = SceneManager.GetActiveScene().GetRootGameObjects()
            .SelectMany(x => x.GetComponentsInChildren<MeshRenderer>());
        material = meshes.First().material;
#if UNITY_EDITOR
        material = new Material(material);        
#endif
        foreach (var meshRenderer in meshes)
        {
            meshRenderer.material = material;
        }
    }

    // Update is called once per frame
    void Update()
    {
        material.SetColor(ColoProp, GetColor());
    }

    Color GetColor()
    {
        const int n = 5;
        switch ((Time.frameCount/100) % n)
        {
            case 0:
                var r = Time.deltaTime % 1;
                var g = Time.fixedDeltaTime % 1;
                var b = Time.smoothDeltaTime % 1;
                return new Color(r,g,b);
            case 1:
                 r = Time.time % 1;
                 g = Time.fixedTime % 1;
                 b = Time.timeSinceLevelLoad % 1;
                return new Color(r,g,b);
            case 2:
                r = Time.unscaledTime % 1;
                g = Time.fixedUnscaledTime % 1;
                b = Time.timeScale % 1;
                return new Color(r,g,b);
            case 3:
                r = Time.unscaledDeltaTime % 1;
                g = Time.fixedUnscaledDeltaTime % 1;
                b = Time.maximumDeltaTime % 1;
                return new Color(r,g,b);
            case 4:
                r = Time.captureDeltaTime % 1;
                g = Time.inFixedTimeStep ? 1 : 0;
                b = 1;
                return new Color(r,g,b);
        }
        
        return Color.black;
    }
}
