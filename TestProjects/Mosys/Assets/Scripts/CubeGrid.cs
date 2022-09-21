using UnityEngine;

/// <summary>
/// A simple utility generating a grid of cubes,
/// used to populate lens distortion test scenes.
/// </summary>
public class CubeGrid : MonoBehaviour
{
    [SerializeField]
    float m_Space;

    [SerializeField]
    Vector2Int m_Resolution;

    [ContextMenu("Generate")]
    void Generate()
    {
        var offset = new Vector2((m_Resolution.x - 1) * m_Space, (m_Resolution.y - 1) * m_Space) * 0.5f;
        
        for (var y = 0; y != m_Resolution.y; ++y)
        {
            for (var x = 0; x != m_Resolution.x; ++x)
            {
                var position = new Vector2(x * m_Space, y * m_Space) - offset;
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.transform.SetParent(transform);
                cube.transform.localPosition = position;
            }
        }
    }
}
