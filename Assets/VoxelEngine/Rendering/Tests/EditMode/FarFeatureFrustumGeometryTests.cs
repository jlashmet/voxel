using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Composition;
using VoxelEngine.Rendering.Api;
using VoxelEngine.Rendering.Runtime.FarWorld;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;
using VoxelEngine.Structures.Runtime.Emitters;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class FarFeatureFrustumGeometryTests
    {
        [TestCase(0, 1, 24, 6, 1f)]
        [TestCase(0, -1, 24, 6, 0.1f)]
        [TestCase(1, 1, 24, 6, 0.1f)]
        [TestCase(1, -1, 24, 6, 1f)]
        [TestCase(2, 1, 24, 6, 1f)]
        [TestCase(2, -1, 24, 6, 0.1f)]
        [TestCase(1, 1, 6, 24, 1f)]
        [TestCase(2, -1, 24, 0, 0.1f)]
        public void FrustumSilhouetteMatchesCanonicalTaper(
            int axis, int direction, int baseRadius, int endRadius, float voxelSize)
        {
            Primitive primitive = CurvedPrimitiveEmitter.Frustum(
                new int3(-73, 11, -29), 49, baseRadius, endRadius,
                (byte)axis, 1, 0, PrimitiveMode.Fill, 0);
            primitive.Direction = (sbyte)direction;
            if (direction < 0) primitive.C[axis] = primitive.B[axis];
            primitive.Bounds(out int3 min, out int3 max);

            // Unequal normalization dimensions ensure a radial circle is not interpreted as a
            // circle in normalized space. Negative coordinates exercise the real bake transform.
            var bake = new FeaturePresentationBake(
                101UL, 42UL, default, int3.zero, 0,
                min - new int3(7, 3, 13), max + new int3(2, 9, 5), new[] { primitive });
            var policy = new FarFeatureSelectionPolicy(
                new FarFeatureSelectionPolicy.Thresholds(24f, 18f, 4f, 3f, 1.5f, 1f),
                new FarFeatureSelectionPolicy.DistanceCaps(5000f, 5000f, 5000f), 60f, 1080);
            var adapter = new FarFeaturePresentationAdapter(new SingleSource(bake), policy, voxelSize);
            IReadOnlyList<FarFeatureInstance> instances = adapter.Query(
                (float3)primitive.C * voxelSize - new float3(0f, 0f, 60f), 1000f);
            Assert.That(instances.Count, Is.EqualTo(1));
            FarFeatureInstance instance = instances[0];
            var root = new GameObject("far-frustum-geometry-regression");
            try
            {
                var renderer = root.AddComponent<ProceduralFarFeatureRenderer>();
                Mesh mesh = renderer.ResolveMesh(instance);
                Vector3[] localVertices = mesh.vertices;
                int[] indices = mesh.triangles;
                var vertices = new float3[localVertices.Length];
                for (int i = 0; i < vertices.Length; i++)
                    vertices[i] = (instance.Position + instance.Scale * (float3)localVertices[i]) / voxelSize;

                int radialAxis = (axis + 1) % 3;
                int3 sample = primitive.C;
                sample[axis] += direction * 36;
                int outerRadius = math.max(baseRadius, endRadius);
                int includedRadius = -1;
                for (int r = outerRadius + 2; r >= 0; r--)
                {
                    int3 voxel = sample;
                    voxel[radialAxis] += r;
                    if (!PrimitiveRasteriser.Contains(in primitive, voxel)) continue;
                    includedRadius = r;
                    break;
                }
                Assert.That(includedRadius, Is.GreaterThanOrEqualTo(0), "Canonical fixture must contain a cross section.");
                float3 origin = (float3)sample + new float3(0.5f);
                origin[radialAxis] += outerRadius + 4f;
                float3 rayDirection = float3.zero;
                rayDirection[radialAxis] = -1f;
                float distance = NearestIntersection(vertices, indices, origin, rayDirection);
                Assert.That(math.isfinite(distance), Is.True, "The production frustum must have a closed visible side.");
                float actualRadius = outerRadius + 4f - distance;
                // The far mesh is a continuous presentation of integer occupancy. A one-cell
                // envelope tolerance permits integer division/centre-to-face rounding, not an AABB.
                Assert.That(actualRadius, Is.EqualTo(includedRadius + 0.5f).Within(1.25f),
                    "Canonical taper was replaced with its bounding box at the far presentation boundary.");

                AssertClosedOutwardMesh(vertices, indices, ((float3)primitive.A + primitive.B + new float3(1f)) * 0.5f);
                Assert.That(mesh.vertexCount, Is.LessThanOrEqualTo(100), "Far frustum geometry must remain bounded.");
                Assert.That(renderer.ResolveMesh(instance), Is.SameAs(mesh), "An unchanged bake must reuse its mesh.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static float NearestIntersection(float3[] vertices, int[] indices, float3 origin, float3 direction)
        {
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < indices.Length; i += 3)
            {
                float3 a = vertices[indices[i]];
                float3 e1 = vertices[indices[i + 1]] - a;
                float3 e2 = vertices[indices[i + 2]] - a;
                float3 h = math.cross(direction, e2);
                float determinant = math.dot(e1, h);
                if (math.abs(determinant) < 1e-6f) continue;
                float inverse = 1f / determinant;
                float3 s = origin - a;
                float u = inverse * math.dot(s, h);
                if (u < -1e-5f || u > 1f + 1e-5f) continue;
                float3 q = math.cross(s, e1);
                float v = inverse * math.dot(direction, q);
                if (v < -1e-5f || u + v > 1f + 1e-5f) continue;
                float distance = inverse * math.dot(e2, q);
                if (distance >= 0f) nearest = math.min(nearest, distance);
            }
            return nearest;
        }

        private static void AssertClosedOutwardMesh(float3[] vertices, int[] indices, float3 interior)
        {
            var edges = new Dictionary<ulong, int>();
            for (int i = 0; i < indices.Length; i += 3)
            {
                int ia = indices[i], ib = indices[i + 1], ic = indices[i + 2];
                float3 a = vertices[ia], b = vertices[ib], c = vertices[ic];
                float3 normal = math.cross(b - a, c - a);
                Assert.That(math.lengthsq(normal), Is.GreaterThan(1e-8f), "Degenerate far triangle.");
                Assert.That(math.dot(normal, (a + b + c) / 3f - interior), Is.GreaterThan(0f),
                    "Far frustum triangle winding must face outward on every axis.");
                AddEdge(edges, ia, ib); AddEdge(edges, ib, ic); AddEdge(edges, ic, ia);
            }
            foreach (int count in edges.Values) Assert.That(count, Is.EqualTo(2), "Far frustum must be closed.");
        }

        private static void AddEdge(Dictionary<ulong, int> edges, int a, int b)
        {
            ulong key = ((ulong)(uint)math.min(a, b) << 32) | (uint)math.max(a, b);
            edges.TryGetValue(key, out int count);
            edges[key] = count + 1;
        }

        private sealed class SingleSource : IFeaturePresentationSource
        {
            private readonly FeaturePresentationBake[] _bakes;
            public SingleSource(FeaturePresentationBake bake) => _bakes = new[] { bake };
            public bool TryGet(ulong sourceId, out FeaturePresentationBake bake)
            {
                bake = sourceId == _bakes[0].SourceId ? _bakes[0] : null;
                return bake != null;
            }
            public IReadOnlyList<FeaturePresentationBake> Query(FeaturePresentationBounds bounds) => _bakes;
        }
    }
}
