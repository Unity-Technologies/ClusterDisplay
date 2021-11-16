using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.ClusterDisplay.Graphics
{
    class SlicedFrustumGizmo
    {
        // indices for tile frustum gizmo
        readonly static int[] k_Indices = new[]
        {
            0, 1, 1, 2, 2, 3, 3, 0, // front
            4, 5, 5, 6, 6, 7, 7, 4, // back
            0, 4, 1, 5, 2, 6, 3, 7, // sides
        };

        public int tileIndex
        {
            set { m_TileIndex = value; }
        }

        public Vector2Int gridSize
        {
            set { m_GridSize = value; }
        }

        public Matrix4x4 viewProjectionInverse
        {
            set { m_ViewProjectionInverse = value; }
        }

        Mesh m_Mesh;
        List<Vector3> m_Vertices = new List<Vector3>();
        List<Vector3> m_Normals = new List<Vector3>();
        List<int> m_Indices = new List<int>();

        int m_TileIndex;
        Vector2Int m_GridSize = Vector2Int.zero;
        Matrix4x4 m_ViewProjectionInverse = Matrix4x4.identity;

        // bookkeeping, avoid recomputing gizmo geometry when neither the camera nor grid-size nor tile-index changed
        int m_CachedTileIndex = 0;
        Vector2Int m_CachedGridSize = Vector2Int.zero;
        Matrix4x4 m_CachedViewProjectionInverse = Matrix4x4.identity;

        public void Draw()
        {
            if (m_GridSize.x < 1 || m_GridSize.y < 1)
                return;

            var camera = Camera.current;
            if (camera == null)
                return;

            // lazy mesh instanciation
            if (m_Mesh == null)
            {
                m_Mesh = new Mesh();
                m_Mesh.hideFlags = HideFlags.HideAndDontSave;
            }

            if (m_CachedGridSize != m_GridSize ||
                m_TileIndex != m_CachedTileIndex ||
                m_CachedViewProjectionInverse != m_ViewProjectionInverse)
            {
                m_Vertices.Clear();
                m_Indices.Clear();

                // visualize sliced frustum
                var numTiles = m_GridSize.x * m_GridSize.y;
                for (var i = 0; i != numTiles; ++i)
                {
                    var rect = Viewport.TileIndexToSubSection(m_GridSize, i);

                    // Convert to clip space.
                    var clipRect = Rect.MinMaxRect(
                        Mathf.Lerp(-1, 1, rect.xMin),
                        Mathf.Lerp(-1, 1, rect.yMin),
                        Mathf.Lerp(-1, 1, rect.xMax),
                        Mathf.Lerp(-1, 1, rect.yMax));

                    AppendTileFrustumGeometry(m_ViewProjectionInverse, clipRect, m_Vertices, m_Indices);
                }

                m_Mesh.Clear();
                m_Mesh.SetVertices(m_Vertices);
                m_Mesh.SetIndices(m_Indices, MeshTopology.Lines, 0);
            }

            // update normals on grid size change only
            if (m_CachedGridSize != m_GridSize)
            {
                m_Normals.Clear();
                for (var i = 0; i != m_Vertices.Count; ++i)
                    m_Normals.Add(Vector3.forward);
                m_Mesh.SetNormals(m_Normals);
            }

            Gizmos.color = Color.cyan;
            Gizmos.DrawMesh(m_Mesh, Vector3.zero);

            m_CachedGridSize = m_GridSize;
            m_CachedTileIndex = m_TileIndex;
            m_CachedViewProjectionInverse = m_ViewProjectionInverse;
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
                indices.Add(k_Indices[i] + baseVertexIndex);
        }
    }
}
