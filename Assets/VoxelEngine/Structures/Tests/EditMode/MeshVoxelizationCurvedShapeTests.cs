using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime.MeshImport;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class MeshVoxelizationCurvedShapeTests
    {
        [Test]
        public void ClosedCurvedMesh_PreservesExtremaAndFilledCenter()
        {
            MeshVoxelizationSource sphere = BuildUvSphere(
                radius: 2.4f,
                latitudeSegments: 18,
                longitudeSegments: 32,
                material: 6,
                transform: float4x4.identity);
            var settings = Settings(voxelSize: 0.4f, fillInterior: true);

            BakedVoxelStructure bake = MeshVoxelizer.Voxelize(in sphere, in settings);

            Assert.That(bake.Cells.Length, Is.GreaterThan(400));
            Assert.That(bake.Size.x, Is.GreaterThan(8));
            Assert.That(bake.Size.y, Is.GreaterThan(8));
            Assert.That(bake.Size.z, Is.GreaterThan(8));
            Assert.That(ContainsLocal(bake, bake.Size / 2), Is.True,
                "A closed curved source must fill its interior rather than collapsing to a shell.");
            Assert.That(HasBoundaryCell(bake, 0, false), Is.True);
            Assert.That(HasBoundaryCell(bake, 0, true), Is.True);
            Assert.That(HasBoundaryCell(bake, 1, false), Is.True);
            Assert.That(HasBoundaryCell(bake, 1, true), Is.True);
            Assert.That(HasBoundaryCell(bake, 2, false), Is.True);
            Assert.That(HasBoundaryCell(bake, 2, true), Is.True);
        }

        [Test]
        public void SameCurvedSourceAndSettings_ProduceStableOrderedOutput()
        {
            MeshVoxelizationSource source = BuildUvSphere(
                radius: 1.75f,
                latitudeSegments: 12,
                longitudeSegments: 24,
                material: 7,
                transform: float4x4.TRS(
                    new float3(2.125f, -1.75f, 5.5f),
                    quaternion.EulerXYZ(0.31f, -0.57f, 0.19f),
                    new float3(1.2f, 0.8f, 1.1f)));
            var settings = Settings(voxelSize: 0.35f, fillInterior: true);

            BakedVoxelStructure first = MeshVoxelizer.Voxelize(in source, in settings);
            BakedVoxelStructure second = MeshVoxelizer.Voxelize(in source, in settings);

            Assert.That(second.GridOrigin, Is.EqualTo(first.GridOrigin));
            Assert.That(second.Size, Is.EqualTo(first.Size));
            Assert.That(second.Cells.Length, Is.EqualTo(first.Cells.Length));
            for (int i = 0; i < first.Cells.Length; i++)
            {
                Assert.That(second.Cells[i].Position, Is.EqualTo(first.Cells[i].Position));
                Assert.That(second.Cells[i].Material, Is.EqualTo(first.Cells[i].Material));
                if (i > 0)
                    Assert.That(Compare(first.Cells[i - 1].Position, first.Cells[i].Position), Is.LessThan(0),
                        "Serialized cells must remain in stable lexicographic order.");
            }
        }

        private static MeshVoxelizationSettings Settings(float voxelSize, bool fillInterior)
        {
            return new MeshVoxelizationSettings(
                voxelSize,
                fillInterior,
                fallbackMaterial: 2,
                maxDimensions: new int3(127, 511, 127),
                maxDenseCells: 2_000_000,
                thinFeaturePaddingVoxels: 0);
        }

        private static MeshVoxelizationSource BuildUvSphere(
            float radius,
            int latitudeSegments,
            int longitudeSegments,
            byte material,
            float4x4 transform)
        {
            var vertices = new List<float3>();
            var triangles = new List<MeshVoxelTriangle>();
            for (int lat = 0; lat <= latitudeSegments; lat++)
            {
                float theta = math.PI * lat / latitudeSegments;
                float y = math.cos(theta) * radius;
                float ring = math.sin(theta) * radius;
                for (int lon = 0; lon <= longitudeSegments; lon++)
                {
                    float phi = 2f * math.PI * lon / longitudeSegments;
                    vertices.Add(new float3(math.cos(phi) * ring, y, math.sin(phi) * ring));
                }
            }

            int stride = longitudeSegments + 1;
            for (int lat = 0; lat < latitudeSegments; lat++)
            for (int lon = 0; lon < longitudeSegments; lon++)
            {
                int a = lat * stride + lon;
                int b = a + 1;
                int c = a + stride;
                int d = c + 1;
                if (lat != 0)
                    triangles.Add(new MeshVoxelTriangle(a, c, b, material));
                if (lat != latitudeSegments - 1)
                    triangles.Add(new MeshVoxelTriangle(b, c, d, material));
            }

            return new MeshVoxelizationSource(vertices.ToArray(), triangles.ToArray(), transform);
        }

        private static bool ContainsLocal(BakedVoxelStructure bake, int3 local)
        {
            for (int i = 0; i < bake.Cells.Length; i++)
                if (math.all(bake.Cells[i].Position == local))
                    return true;
            return false;
        }

        private static bool HasBoundaryCell(BakedVoxelStructure bake, int axis, bool upper)
        {
            int expected = upper ? bake.Size[axis] - 1 : 0;
            for (int i = 0; i < bake.Cells.Length; i++)
                if (bake.Cells[i].Position[axis] == expected)
                    return true;
            return false;
        }

        private static int Compare(int3 a, int3 b)
        {
            if (a.x != b.x) return a.x.CompareTo(b.x);
            if (a.y != b.y) return a.y.CompareTo(b.y);
            return a.z.CompareTo(b.z);
        }
    }
}
