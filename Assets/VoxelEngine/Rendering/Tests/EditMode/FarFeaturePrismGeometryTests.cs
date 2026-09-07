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
    public sealed class FarFeaturePrismGeometryTests
    {
        [Test]
        public void BoxFacesKeepPlanarNormalsAtEveryCorner()
        {
            var geometry = new FarFeatureGeometry(new[] { new FarFeatureGeometryPrimitive(
                FarFeatureGeometryShape.Box, float3.zero, new float3(1)) });
            var instance = new FarFeatureInstance(1, float3.zero, quaternion.identity,
                new float3(1), new float3(0.5f), new float3(0.5f), "planar-box", "style",
                FarFeatureTier.Mid, FarFeatureVisualFlags.None, geometry);
            var root = new GameObject("far-planar-normal-regression");
            try
            {
                Mesh mesh = root.AddComponent<ProceduralFarFeatureRenderer>().ResolveMesh(instance);
                Vector3[] positions = mesh.vertices;
                Vector3[] normals = mesh.normals;
                int[] triangles = mesh.triangles;
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    Vector3 face = Vector3.Cross(positions[triangles[i + 1]] - positions[triangles[i]],
                        positions[triangles[i + 2]] - positions[triangles[i]]).normalized;
                    for (int corner = 0; corner < 3; corner++)
                        Assert.That(Vector3.Dot(face, normals[triangles[i + corner]]), Is.GreaterThan(0.999f),
                            "Perpendicular walls must not share smoothed corner normals.");
                }
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase(0, 1, 16, 8, PrismProfile.Gable)]
        [TestCase(2, -1, 15, 8, PrismProfile.Gable)]
        [TestCase(0, 1, 4, 48, PrismProfile.Gable)]
        [TestCase(2, 1, 16, 8, PrismProfile.Shed)]
        [TestCase(0, -1, 4, 48, PrismProfile.Shed)]
        [TestCase(2, 1, 16, 8, PrismProfile.Arch)]
        [TestCase(0, -1, 15, 8, PrismProfile.Arch)]
        [TestCase(2, 1, 1, 12, PrismProfile.Arch)]
        public void ProductionFarSurfaceTracksCanonicalPrismOccupancy(
            int axis, int direction, int run, int height, PrismProfile profile)
        {
            int3 size = new(12, height, 12);
            size[axis == 0 ? 2 : 0] = run;
            var primitive = new Primitive { Shape = PrimitiveShape.Prism, Mode = PrimitiveMode.Fill,
                A = new int3(-91, 13, -37), Axis = (byte)axis, Direction = (sbyte)direction, Profile = profile, Material = 1 };
            primitive.B = primitive.A + size - 1;
            var bake = new FeaturePresentationBake(101, 42, default, int3.zero, 0,
                primitive.A - new int3(3, 7, 5), primitive.B + new int3(9, 3, 2), new[] { primitive });
            var policy = new FarFeatureSelectionPolicy(
                new FarFeatureSelectionPolicy.Thresholds(24, 18, 4, 3, 1.5f, 1),
                new FarFeatureSelectionPolicy.DistanceCaps(1000, 1000, 1000), 60, 1080);
            const float voxelSize = 0.1f;
            var adapter = new FarFeaturePresentationAdapter(new SingleSource(bake), policy, voxelSize);
            FarFeatureInstance instance = adapter.Query(float3.zero, 1000)[0];
            var root = new GameObject("canonical-far-prism-regression");
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
                        $"Canonical column ({x},{z}) was replaced with a box or reversed roof profile.");
                }
                float3 interior = float3.zero;
                foreach (float3 vertex in vertices) interior += vertex;
                interior /= vertices.Length;
                Assert.That(mesh.vertexCount, Is.LessThanOrEqualTo(80), "Far roof cost must be independent of voxel dimensions.");
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
