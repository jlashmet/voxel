using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Runtime.MeshImport;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class MeshVoxelizationMetricsTests
    {
        [Test]
        public void Analyze_FilledCubeReportsSurfaceConnectivityMaterialsAndSparseBricks()
        {
            var cells = new List<BakedVoxelCell>();
            for (int x = 0; x < 3; x++)
            for (int y = 0; y < 3; y++)
            for (int z = 0; z < 3; z++)
            {
                byte material = x == 1 && y == 1 && z == 1 ? (byte)9 : (byte)4;
                cells.Add(new BakedVoxelCell(new int3(x, y, z), material));
            }
            var bake = new BakedVoxelStructure(
                0.1f, new int3(-4, 7, 11), new int3(3), cells.ToArray(), 12, 2.5d,
                interiorFilled: true);

            BakedVoxelStructureStats stats = MeshVoxelizationMetrics.Analyze(bake, brickEdgeVoxels: 2);
            BakedVoxelCell[] surface = MeshVoxelizationMetrics.ExtractSurfaceCells(bake);

            Assert.That(stats.CellCount, Is.EqualTo(27));
            Assert.That(stats.SurfaceCellCount, Is.EqualTo(26));
            Assert.That(surface.Length, Is.EqualTo(26));
            Assert.That(stats.ConnectedComponentCount, Is.EqualTo(1));
            Assert.That(stats.MaterialCount, Is.EqualTo(2));
            Assert.That(stats.SparseBrickCount, Is.EqualTo(8));
        }

        [Test]
        public void MeasurePointClouds_IdenticalSamplesAreZeroDistanceAndPerfectSilhouette()
        {
            float3[] samples =
            {
                new float3(0f, 0f, 0f),
                new float3(2f, 0f, 0f),
                new float3(0f, 3f, 0f),
                new float3(2f, 3f, 1f),
                new float3(1f, 1f, 2f),
            };

            MeshVoxelFidelityReport report =
                MeshVoxelizationMetrics.MeasurePointClouds(samples, (float3[])samples.Clone(), 64);

            Assert.That(report.SourceSampleCount, Is.EqualTo(samples.Length));
            Assert.That(report.VoxelSampleCount, Is.EqualTo(samples.Length));
            Assert.That(report.SymmetricP95Voxels, Is.EqualTo(0f).Within(1e-6f));
            Assert.That(report.FrontSilhouetteIoU, Is.EqualTo(1f).Within(1e-6f));
            Assert.That(report.SideSilhouetteIoU, Is.EqualTo(1f).Within(1e-6f));
            Assert.That(report.TopSilhouetteIoU, Is.EqualTo(1f).Within(1e-6f));
            Assert.That(report.MinPrimarySilhouetteIoU, Is.EqualTo(1f).Within(1e-6f));
        }

        [Test]
        public void MeasurePointClouds_SeparatedSamplesReportDistanceAndViewDependentSilhouette()
        {
            float3[] source = { new float3(0f, 0f, 0f) };
            float3[] voxel = { new float3(2f, 0f, 0f) };

            MeshVoxelFidelityReport report = MeshVoxelizationMetrics.MeasurePointClouds(source, voxel, 64);

            Assert.That(report.SymmetricP95Voxels, Is.EqualTo(2f).Within(1e-6f));
            Assert.That(report.FrontSilhouetteIoU, Is.EqualTo(0f));
            Assert.That(report.TopSilhouetteIoU, Is.EqualTo(0f));
            Assert.That(report.SideSilhouetteIoU, Is.EqualTo(1f),
                "Dropping X in the side projection should hide an X-only displacement.");
        }

        [Test]
        public void Measure_VoxelizedTransformedClosedMeshProducesBoundedDeterministicEvidence()
        {
            MeshVoxelizationSource source = BuildBox(
                new float3(-1f, -0.75f, -0.5f),
                new float3(1f, 0.75f, 0.5f),
                material: 6,
                float4x4.TRS(
                    new float3(3.25f, -1.5f, 5.75f),
                    quaternion.EulerXYZ(0.15f, -0.35f, 0.22f),
                    new float3(1.4f, 0.9f, 1.2f)));
            var settings = new MeshVoxelizationSettings(
                voxelSize: 0.35f,
                fillInterior: true,
                fallbackMaterial: 6,
                maxDimensions: new int3(64),
                maxDenseCells: 64 * 64 * 64,
                thinFeaturePaddingVoxels: 0,
                openSurfacePolicy: MeshVoxelOpenSurfacePolicy.Reject);

            BakedVoxelStructure bake = MeshVoxelizer.Voxelize(in source, in settings);
            MeshVoxelFidelityReport first = MeshVoxelizationMetrics.Measure(in source, bake, 256, 64);
            MeshVoxelFidelityReport second = MeshVoxelizationMetrics.Measure(in source, bake, 256, 64);

            Assert.That(first.SourceSampleCount, Is.GreaterThan(0));
            Assert.That(first.VoxelSampleCount, Is.GreaterThan(0));
            Assert.That(first.SymmetricP95Voxels, Is.LessThan(2.0f));
            Assert.That(first.FrontSilhouetteIoU, Is.InRange(0f, 1f));
            Assert.That(first.SideSilhouetteIoU, Is.InRange(0f, 1f));
            Assert.That(first.TopSilhouetteIoU, Is.InRange(0f, 1f));
            Assert.That(second.SymmetricP95Voxels, Is.EqualTo(first.SymmetricP95Voxels).Within(1e-6f));
            Assert.That(second.FrontSilhouetteIoU, Is.EqualTo(first.FrontSilhouetteIoU).Within(1e-6f));
            Assert.That(second.SideSilhouetteIoU, Is.EqualTo(first.SideSilhouetteIoU).Within(1e-6f));
            Assert.That(second.TopSilhouetteIoU, Is.EqualTo(first.TopSilhouetteIoU).Within(1e-6f));
        }

        [Test]
        public void Measure_LowQueryCapDoesNotReportSparseSampleSpacingAsSurfaceError()
        {
            MeshVoxelizationSource source = BuildBox(
                new float3(-1f),
                new float3(1f),
                material: 8,
                float4x4.identity);
            var settings = new MeshVoxelizationSettings(
                voxelSize: 0.1f,
                fillInterior: true,
                fallbackMaterial: 8,
                maxDimensions: new int3(64),
                maxDenseCells: 64 * 64 * 64,
                thinFeaturePaddingVoxels: 0,
                openSurfacePolicy: MeshVoxelOpenSurfacePolicy.Reject);

            BakedVoxelStructure bake = MeshVoxelizer.Voxelize(in source, in settings);
            MeshVoxelFidelityReport report = MeshVoxelizationMetrics.Measure(
                in source, bake, maxSamplesPerSurface: 32, silhouetteResolution: 64);

            Assert.That(report.SourceSampleCount, Is.LessThanOrEqualTo(32));
            Assert.That(report.VoxelSampleCount, Is.LessThanOrEqualTo(32));
            Assert.That(report.SymmetricP95Voxels, Is.LessThanOrEqualTo(1.5f),
                "Distance queries must compare against the full opposite surface, not another sparse query sample.");
        }

        private static MeshVoxelizationSource BuildBox(
            float3 min,
            float3 max,
            byte material,
            float4x4 transform)
        {
            float3[] vertices =
            {
                new float3(min.x, min.y, min.z),
                new float3(max.x, min.y, min.z),
                new float3(max.x, max.y, min.z),
                new float3(min.x, max.y, min.z),
                new float3(min.x, min.y, max.z),
                new float3(max.x, min.y, max.z),
                new float3(max.x, max.y, max.z),
                new float3(min.x, max.y, max.z),
            };
            MeshVoxelTriangle[] triangles =
            {
                new MeshVoxelTriangle(0, 2, 1, material), new MeshVoxelTriangle(0, 3, 2, material),
                new MeshVoxelTriangle(4, 5, 6, material), new MeshVoxelTriangle(4, 6, 7, material),
                new MeshVoxelTriangle(0, 1, 5, material), new MeshVoxelTriangle(0, 5, 4, material),
                new MeshVoxelTriangle(3, 7, 6, material), new MeshVoxelTriangle(3, 6, 2, material),
                new MeshVoxelTriangle(0, 4, 7, material), new MeshVoxelTriangle(0, 7, 3, material),
                new MeshVoxelTriangle(1, 2, 6, material), new MeshVoxelTriangle(1, 6, 5, material),
            };
            return new MeshVoxelizationSource(vertices, triangles, transform);
        }
    }
}
