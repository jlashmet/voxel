using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class FarTerrainTopologyReuseTests
    {
        [Test]
        public void RebuildAfterCameraSnap_ReusesExistingIndexTopology()
        {
            var go = new GameObject("FarTerrainTopologyReuseTests");
            try
            {
                var far = go.AddComponent<VoxelFarTerrain>();
                SetField(far, "m_InnerRadiusMetres", 12.8f);
                SetField(far, "m_OuterRadiusMetres", 64f);
                SetField(far, "m_Resolution", 8);
                far.HoleRadiusMetres = 4f;

                Invoke(far, "EnsureRings");

                var heights = GetField<List<NativeArray<int>>>(far, "_ringHeights");
                var meshes = GetField<List<Mesh>>(far, "_ringMeshes");
                Assert.GreaterOrEqual(heights.Count, 2);
                Assert.GreaterOrEqual(meshes.Count, 2);

                const int ring = 1;
                NativeArray<int> ringHeights = heights[ring];
                for (int i = 0; i < ringHeights.Length; i++)
                    ringHeights[i] = ShowcaseWorld.BaseHeightVoxels;

                int spacing = far.SpacingForRing(ring);
                InvokeRebuild(far, ring, new int2(0, 0), spacing);

                Mesh mesh = meshes[ring];
                int[] firstTriangles = mesh.triangles;
                Vector3 firstVertex = mesh.vertices[0];
                ulong firstTopologyBuilds = far.TopologyRebuildCount;

                Assert.Greater(firstTriangles.Length, 0);
                Assert.AreEqual(1ul, firstTopologyBuilds,
                    "The first publication of the isolated ring should establish its topology once.");

                // Moving a clipmap ring changes every vertex position, but not which lattice
                // vertices form its annulus. Re-publishing after a snapped camera move must keep
                // the existing index buffer rather than rebuilding and uploading ~6 indices per
                // quad again on the player frame.
                InvokeRebuild(far, ring, new int2(spacing, 0), spacing);

                Assert.AreEqual(firstTopologyBuilds, far.TopologyRebuildCount,
                    "A camera-origin change rebuilt invariant far-terrain topology.");
                CollectionAssert.AreEqual(firstTriangles, mesh.triangles,
                    "A camera-origin change altered the ring's invariant index topology.");
                Assert.AreEqual(firstVertex.x + spacing * 0.1f, mesh.vertices[0].x, 0.0001f,
                    "The vertex buffer did not move with the snapped clipmap origin.");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        private static void InvokeRebuild(VoxelFarTerrain far, int ring, int2 origin, int spacing)
        {
            MethodInfo method = typeof(VoxelFarTerrain).GetMethod(
                "RebuildRingFromCachedHeights", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            method.Invoke(far, new object[] { ring, origin, spacing });
        }

        private static void Invoke(VoxelFarTerrain far, string methodName)
        {
            MethodInfo method = typeof(VoxelFarTerrain).GetMethod(
                methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method, methodName);
            method.Invoke(far, null);
        }

        private static T GetField<T>(VoxelFarTerrain far, string fieldName)
        {
            FieldInfo field = typeof(VoxelFarTerrain).GetField(
                fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field, fieldName);
            return (T)field.GetValue(far);
        }

        private static void SetField<T>(VoxelFarTerrain far, string fieldName, T value)
        {
            FieldInfo field = typeof(VoxelFarTerrain).GetField(
                fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field, fieldName);
            field.SetValue(far, value);
        }
    }
}
