using System;
using UnityEngine;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using Random = UnityEngine.Random;

namespace Unity.ClusterDisplay.Graphics.Tests
{
	[TestFixture]
	class GraphicsUtilTest
	{
		const int k_PlanesPerFrustum = 6;
		static readonly Matrix4x4 k_Projection = Matrix4x4.Perspective(60, 1, 1, 300);
		static readonly Rect k_NanRect = new Rect(Single.NaN, Single.NaN, Single.NaN, Single.NaN);
		
		// Frustum Planes Ordering: [0] = Left, [1] = Right, [2] = Down, [3] = Up, [4] = Near, [5] = Far.
		const int k_FrustumLeft = 0;
		const int k_FrustumRight = 1;
		const int k_FrustumDown = 2;
		const int k_FrustumUp = 3;
		const int k_FrustumNear = 4;
		const int k_FrustumFar = 5;
		
		static Vector4 RectToVector4(Rect r)
		{
			return new Vector4(r.x, r.y, r.width, r.height);
		}

		public class TestCaseDataSource
		{
			public static IEnumerable TileIndexToViewportSection
			{
				get
				{
					yield return new TestCaseData(new Vector2Int(0, 0), 0)
						.Returns(Rect.zero);
					yield return new TestCaseData(new Vector2Int(1, 1), 0)
						.Returns(new Rect(0, 0, 1, 1));
					yield return new TestCaseData(new Vector2Int(2, 2), 2)
						.Returns(new Rect(0, 0, 0.5f, 0.5f));
					yield return new TestCaseData(new Vector2Int(2, 2), 0)
						.Returns(new Rect(0, 0.5f, 0.5f, 0.5f));
					yield return new TestCaseData(new Vector2Int(3, 3), 6)
						.Returns(new Rect(0, 0, 1/3f, 1/3f));
					yield return new TestCaseData(new Vector2Int(3, 3), 2)
						.Returns(new Rect(2/3f, 2/3f, 1/3f, 1/3f));
				}
			}

			public static IEnumerable ApplyOverscan
			{
				get
				{
					yield return new TestCaseData(new Rect(0, 0, 1, 1), 0, 0, 0).Returns(k_NanRect);
					yield return new TestCaseData(new Rect(0, 0, 1, 1), 128, 512, 512)
						.Returns(new Rect(-0.25f, -0.25f, 1.5f, 1.5f));
					yield return new TestCaseData(new Rect(0, 0, 1, 1), 128, 512, 256)
						.Returns(new Rect(-0.25f, -0.5f, 1.5f, 2f));
				}
			}
		}

		[TestCaseSource(typeof(TestCaseDataSource), "TileIndexToViewportSection")]
		public Rect TileIndexToViewportSection(Vector2Int gridSize, int tileIndex)
		{
			return GraphicsUtil.TileIndexToViewportSection(gridSize, tileIndex);
		}
		
		[TestCaseSource(typeof(TestCaseDataSource), "ApplyOverscan")]
		public Rect ApplyOverscan(Rect normalizedViewportSubsection, int overscanInPixels, int viewportWidth, int viewportHeight)
		{
			return GraphicsUtil.ApplyOverscan(normalizedViewportSubsection, overscanInPixels, viewportWidth, viewportHeight);
		}
		
		[TestCase(0)]
		[TestCase(3)]
		[TestCase(13)]
		[TestCase(29)]
		[TestCase(67)]
		public void HdrpClusterDisplayParams(int seed)
		{
			Random.InitState(seed);
			var overscannedViewportSubsection = new Rect(
				Random.value + Mathf.Epsilon, 
				Random.value + Mathf.Epsilon, 
				Random.value + Mathf.Epsilon, 
				Random.value + Mathf.Epsilon);
			var globalScreenSize = new Vector2(Random.Range(128, 2048), Random.Range(128, 2048));
			var gridSize = new Vector2Int(Random.Range(1, 12), Random.Range(1, 12));
			var parms = GraphicsUtil.GetHdrpClusterDisplayParams(overscannedViewportSubsection, globalScreenSize, gridSize);
			
			Assert.IsTrue(parms.GetRow(0) == RectToVector4(overscannedViewportSubsection));
			var row1 = parms.GetRow(1);
			Assert.AreEqual(row1.x, globalScreenSize.x);
			Assert.AreEqual(row1.y, globalScreenSize.y);
			Assert.AreEqual(row1.z, 1f / globalScreenSize.x);
			Assert.AreEqual(row1.w, 1f / globalScreenSize.y);
			var row2 = parms.GetRow(2);
			Assert.AreEqual(row2.x, gridSize.x);
			Assert.AreEqual(row2.y, gridSize.y);
		}

		static bool PlanesAreEqual(Plane a, Plane b)
		{
			return a.normal == b.normal && Mathf.Approximately(a.distance, b.distance);
		}
		
		static bool PlanesAreFlipped(Plane a, Plane b)
		{
			return a.normal == b.normal * -1 && Mathf.Approximately(a.distance, b.distance);
		}

		static void SliceProjection(Matrix4x4 projection, Vector2Int gridSize, List<Plane> output)
		{
			var numTiles = gridSize.x * gridSize.y;
			for (var i = 0; i != numTiles; ++i)
			{
				var tile = GraphicsUtil.TileIndexToViewportSection(gridSize, i);
				var asymmetricProjection = GraphicsUtil.GetFrustumSlicingAsymmetricProjection(projection, tile);
				foreach (var plane in GeometryUtility.CalculateFrustumPlanes(asymmetricProjection))
					output.Add(plane);
			}
		}

		static IEnumerable<int> GetLeftTileIndices(Vector2Int gridSize)
		{
			for (var i = 0; i != gridSize.y; ++i)
			{
				var tileIndex = i * gridSize.x;
				yield return tileIndex * k_PlanesPerFrustum + k_FrustumLeft;
			}
		}
		
		static IEnumerable<int> GetRightTileIndices(Vector2Int gridSize)
		{
			for (var i = 0; i != gridSize.y; ++i)
			{
				var tileIndex = i * gridSize.x + gridSize.x - 1;
				yield return tileIndex * k_PlanesPerFrustum + k_FrustumRight;
			}
		}

		static IEnumerable<int> GetUpTileIndices(Vector2Int gridSize)
		{
			for (var i = 0; i != gridSize.x; ++i)
			{
				yield return i * k_PlanesPerFrustum + k_FrustumUp;
			}
		}
		
		static IEnumerable<int> GetDownTileIndices(Vector2Int gridSize)
		{
			for (var i = 0; i != gridSize.x; ++i)
			{
				yield return (gridSize.x * (gridSize.y - 1) + i) * k_PlanesPerFrustum + k_FrustumDown;
			}
		}

		static void UnionOfTilesIsOriginalFrustum(Vector2Int gridSize, Plane[] originalPlanes, List<Plane> planes)
		{
			// Make sure the union of sliced frustum corresponds to the original frustum.
			foreach (var index in GetLeftTileIndices(gridSize))
				Assert.IsTrue(PlanesAreEqual(originalPlanes[k_FrustumLeft], planes[index]));
				
			foreach (var index in GetRightTileIndices(gridSize))
				Assert.IsTrue(PlanesAreEqual(originalPlanes[k_FrustumRight], planes[index]));
			
			foreach (var index in GetUpTileIndices(gridSize))
				Assert.IsTrue(PlanesAreEqual(originalPlanes[k_FrustumUp], planes[index]));
			
			foreach (var index in GetDownTileIndices(gridSize))
				Assert.IsTrue(PlanesAreEqual(originalPlanes[k_FrustumDown], planes[index]));
			
			// Near and far should be the same across all tiles.
			var numTiles = gridSize.x * gridSize.y;
			for (var i = 0; i != numTiles; ++i)
			{
				Assert.IsTrue(PlanesAreEqual(originalPlanes[k_FrustumNear], planes[i * k_PlanesPerFrustum + k_FrustumNear]));
				Assert.IsTrue(PlanesAreEqual(originalPlanes[k_FrustumFar], planes[i * k_PlanesPerFrustum + k_FrustumFar]));
			}
		}

		static void TileFrustumsStitchProperly(Vector2Int gridSize, List<Plane> planes)
		{
			for (var y = 0; y != gridSize.y - 1; ++y)
			{
				for (var x = 0; x != gridSize.x - 1; ++x)
				{
					var i = gridSize.x * y + x;
					
					Assert.IsTrue(PlanesAreFlipped(
						planes[i * k_PlanesPerFrustum + k_FrustumRight],
						planes[(i + 1) * k_PlanesPerFrustum + k_FrustumLeft]));
					
					Assert.IsTrue(PlanesAreFlipped(
						planes[i * k_PlanesPerFrustum + k_FrustumDown],
						planes[(i + gridSize.x) * k_PlanesPerFrustum + k_FrustumUp]));
				}
			}
		}

		[TestCase(2, 2)]
		[TestCase(13, 6)]
		public void FrustumSlicing(int cols, int rows)
		{
			var gridSize = new Vector2Int(cols, rows);
			var originalPlanes = GeometryUtility.CalculateFrustumPlanes(k_Projection);
			var planes = new List<Plane>(); // flat collection of all tiles frustum planes
			SliceProjection(k_Projection, gridSize, planes);

			UnionOfTilesIsOriginalFrustum(gridSize, originalPlanes, planes);
			TileFrustumsStitchProperly(gridSize, planes);
		}
	}
}


