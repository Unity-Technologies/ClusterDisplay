using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.ClusterDisplay.Graphics
{
    class ClusterFrustumGizmo
    {
        // indices for tile frustum gizmo
        static readonly int[] k_Indices = new[]
        {
            0, 1, 1, 2, 2, 3, 3, 0, // front
            4, 5, 5, 6, 6, 7, 7, 4, // back
            0, 4, 1, 5, 2, 6, 3, 7 // sides
        };

        Mesh m_FrustumGizmoMesh;
        List<Vector3> m_FrustumGizmoVertices = new List<Vector3>();
        List<Vector3> m_FrustumGizmoNormals = new List<Vector3>();
        List<int> m_FrustumGizmoIndices = new List<int>();

        // bookkeeping, avoid recomputing gizmo geometry when neither the camera nor grid-size nor tile-index changed
        int m_CachedTileIndex = 0;
        Vector2Int m_CachedGridSize = Vector2Int.zero;
        Matrix4x4 m_CachedViewProjectionInverse = Matrix4x4.identity;

        public void Draw(Matrix4x4 viewProjectionInverse, Vector2Int gridSize, int tileIndex)
        {
            if (gridSize.x < 1 || gridSize.y < 1)
            {
                return;
            }

            var camera = Camera.current;
            if (camera == null)
            {
                return;
            }

            // lazy mesh instanciation
            if (m_FrustumGizmoMesh == null)
            {
                m_FrustumGizmoMesh = new Mesh();
                m_FrustumGizmoMesh.hideFlags = HideFlags.HideAndDontSave;
            }

            if (m_CachedGridSize != gridSize ||
                tileIndex != m_CachedTileIndex ||
                m_CachedViewProjectionInverse != viewProjectionInverse)
            {
                m_FrustumGizmoVertices.Clear();
                m_FrustumGizmoIndices.Clear();

                // visualize sliced frustum
                var numTiles = gridSize.x * gridSize.y;
                for (var i = 0; i != numTiles; ++i)
                {
                    var rect = GraphicsUtil.TileIndexToViewportSection(gridSize, i);

                    // Convert to clip space.
                    var clipRect = Rect.MinMaxRect(
                        Mathf.Lerp(-1, 1, rect.xMin),
                        Mathf.Lerp(-1, 1, rect.yMin),
                        Mathf.Lerp(-1, 1, rect.xMax),
                        Mathf.Lerp(-1, 1, rect.yMax));

                    AppendTileFrustumGeometry(viewProjectionInverse, clipRect, m_FrustumGizmoVertices, m_FrustumGizmoIndices);
                }

                m_FrustumGizmoMesh.Clear();
                m_FrustumGizmoMesh.SetVertices(m_FrustumGizmoVertices);
                m_FrustumGizmoMesh.SetIndices(m_FrustumGizmoIndices, MeshTopology.Lines, 0);
            }

            // update normals on grid size change only
            if (m_CachedGridSize != gridSize)
            {
                m_FrustumGizmoNormals.Clear();
                for (var i = 0; i != m_FrustumGizmoVertices.Count; ++i)
                {
                    m_FrustumGizmoNormals.Add(Vector3.forward);
                }

                m_FrustumGizmoMesh.SetNormals(m_FrustumGizmoNormals);
            }

            Gizmos.color = Color.cyan;
            Gizmos.DrawMesh(m_FrustumGizmoMesh, Vector3.zero);

            m_CachedGridSize = gridSize;
            m_CachedTileIndex = tileIndex;
            m_CachedViewProjectionInverse = viewProjectionInverse;
        }

        static void AppendTileFrustumGeometry(Matrix4x4 viewProjectionInverse, Rect viewportSection, List<Vector3> vertices, List<int> indices)
        {
            var baseVertexIndex = vertices.Count;

            // Append vertices
            // - front
            vertices.Add(viewProjectionInverse.MultiplyPoint(new Vector3(viewportSection.xMin, viewportSection.yMin, 0)));
            vertices.Add(viewProjectionInverse.MultiplyPoint(new Vector3(viewportSection.xMax, viewportSection.yMin, 0)));
            vertices.Add(viewProjectionInverse.MultiplyPoint(new Vector3(viewportSection.xMax, viewportSection.yMax, 0)));
            vertices.Add(viewProjectionInverse.MultiplyPoint(new Vector3(viewportSection.xMin, viewportSection.yMax, 0)));

            // - back
            vertices.Add(viewProjectionInverse.MultiplyPoint(new Vector3(viewportSection.xMin, viewportSection.yMin, 1)));
            vertices.Add(viewProjectionInverse.MultiplyPoint(new Vector3(viewportSection.xMax, viewportSection.yMin, 1)));
            vertices.Add(viewProjectionInverse.MultiplyPoint(new Vector3(viewportSection.xMax, viewportSection.yMax, 1)));
            vertices.Add(viewProjectionInverse.MultiplyPoint(new Vector3(viewportSection.xMin, viewportSection.yMax, 1)));

            // Append indices
            for (var i = 0; i != k_Indices.Length; ++i)
            {
                indices.Add(k_Indices[i] + baseVertexIndex);
            }
        }
    }
}
