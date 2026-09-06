using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Composition;
using VoxelEngine.Rendering.Api;
using VoxelEngine.Rendering.Runtime.FarWorld;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class FarFeatureRampGeometryTests
    {
        [TestCase(0, 1, 64, 16)]
        [TestCase(0, -1, 64, 16)]
        [TestCase(2, 1, 64, 16)]
        [TestCase(2, -1, 64, 16)]
        [TestCase(0, 1, 4, 48)]
        [TestCase(2, -1, 4, 48)]
        [TestCase(1, 1, 16, 15)]
        [TestCase(1, -1, 16, 15)]
        [TestCase(1, -1, 16, 16)]
        [TestCase(0, -1, 1, 16)]
        public void ProductionFarSurfaceTracksCanonicalRampOccupancy(
            int axis, int direction, int run, int height)
        {
            int3 size = new(12, height, 12);
            if (axis != 1) size[axis] = run;
            var primitive = new Primitive { Shape = PrimitiveShape.Ramp, Mode = PrimitiveMode.Fill,
                A = new int3(-91, 13, -37), Axis = (byte)axis, Direction = (sbyte)direction, Material = 1 };
            primitive.B = primitive.A + size - 1;
            var bake = new FeaturePresentationBake(101, 42, default, int3.zero, 0,
                primitive.A - new int3(3, 7, 5), primitive.B + new int3(9, 3, 2), new[] { primitive });
            var policy = new FarFeatureSelectionPolicy(
                new FarFeatureSelectionPolicy.Thresholds(24, 18, 4, 3, 1.5f, 1),
                new FarFeatureSelectionPolicy.DistanceCaps(1000, 1000, 1000), 60, 1080);
            const float voxelSize = 0.1f;
            var adapter = new FarFeaturePresentationAdapter(new SingleSource(bake), policy, voxelSize);
            FarFeatureInstance instance = adapter.Query(float3.zero, 1000)[0];
            var root = new GameObject("canonical-far-ramp-regression");
            try
            {
                Mesh mesh = root.AddComponent<ProceduralFarFeatureRenderer>().ResolveMesh(instance);
                var vertices = new float3[mesh.vertexCount];
                Vector3[] local = mesh.vertices;
                for (int i = 0; i < vertices.Length; i++)
                    vertices[i] = (instance.Position + instance.Scale * (float3)local[i]) / voxelSize;
                int[] indices = mesh.triangles;
                for (int x = 0; x < size.x; x++)
                for (int z = 0; z < size.z; z++)
                {
                    int3 sample = primitive.A + new int3(x, 0, z);
                    int occupied = 0;
                    for (int y = 0; y < height; y++)
                        if (PrimitiveRasteriser.Contains(primitive, sample + new int3(0, y, 0))) occupied++;
                    float3 origin = (float3)sample + new float3(0.5f, height + 2, 0.5f);
                    float distance = NearestIntersection(vertices, indices, origin, new float3(0, -1, 0));
                    Assert.That(math.isfinite(distance), Is.True);
                    float top = origin.y - distance - primitive.A.y;
                    Assert.That(top, Is.EqualTo(occupied).Within(1.001f),
                        $"Canonical column ({x},{z}) was replaced with a wall or reversed slope.");
                }
                float3 interior = float3.zero;
                foreach (float3 vertex in vertices) interior += vertex;
                interior /= vertices.Length;
                AssertClosedOutwardMesh(vertices, indices, interior);
            }
            finally { Object.DestroyImmediate(root); }
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
