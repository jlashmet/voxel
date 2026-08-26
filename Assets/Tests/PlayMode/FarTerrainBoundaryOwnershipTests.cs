using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class FarTerrainBoundaryOwnershipTests
    {
        private const float VoxelSize = 0.1f;

        [Test]
        public void RingZeroHoleDoesNotSubmitBoundaryCellsIntoNearOwnedFootprint()
        {
            var go = new GameObject("FarTerrainBoundaryOwnershipTests.RingZeroExclusiveBoundary");
            try
            {
                var far = go.AddComponent<VoxelFarTerrain>();
                const int resolution = 8;
                const float holeMetres = 4f;
                SetField(far, "m_InnerRadiusMetres", 12.8f);
                SetField(far, "m_OuterRadiusMetres", 64f);
                SetField(far, "m_Resolution", resolution);
                far.HoleRadiusMetres = holeMetres;

                Invoke(far, "EnsureRings");

                var heights = GetField<List<NativeArray<int>>>(far, "_ringHeights");
                var meshes = GetField<List<Mesh>>(far, "_ringMeshes");
                int spacing = far.SpacingForRing(0);
                int2 origin = new(
                    -(resolution / 2) * spacing,
                    -(resolution / 2) * spacing);

                NativeArray<int> ringHeights = heights[0];
                for (int i = 0; i < ringHeights.Length; i++)
                    ringHeights[i] = ShowcaseWorld.BaseHeightVoxels;

                InvokeRebuild(far, 0, origin, spacing);

                Mesh ringZero = meshes[0];
                Assert.That(ringZero, Is.Not.Null);
                int[] triangles = ringZero.triangles;
                Vector3[] vertices = ringZero.vertices;
                Assert.That(triangles.Length, Is.GreaterThan(0));

                float holeSq = holeMetres * holeMetres;
                for (int index = 0; index < triangles.Length; index++)
                {
                    Vector3 vertex = vertices[triangles[index]];
                    float radiusSq = vertex.x * vertex.x + vertex.z * vertex.z;
                    Assert.That(radiusSq, Is.GreaterThanOrEqualTo(holeSq - 0.001f),
                        $"Ring 0 submitted coarse boundary vertex {vertex} inside the "
                      + $"{holeMetres:F1}m near-owned footprint.");
                }

                float cellMetres = spacing * VoxelSize;
                Assert.That(ringZero.bounds.extents.x, Is.GreaterThan(holeMetres + cellMetres),
                    "Exclusive boundary ownership must not remove the far ring itself.");
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
            Assert.That(method, Is.Not.Null);
            method.Invoke(far, new object[] { ring, origin, spacing });
        }

        private static void Invoke(VoxelFarTerrain far, string methodName)
        {
            MethodInfo method = typeof(VoxelFarTerrain).GetMethod(
                methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null);
            method.Invoke(far, null);
        }

        private static T GetField<T>(VoxelFarTerrain far, string fieldName)
        {
            FieldInfo field = typeof(VoxelFarTerrain).GetField(
                fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null);
            return (T)field.GetValue(far);
        }

        private static void SetField<T>(VoxelFarTerrain far, string fieldName, T value)
        {
            FieldInfo field = typeof(VoxelFarTerrain).GetField(
                fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null);
            field.SetValue(far, value);
        }
    }
}
