using System.IO;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using VoxelEngine.MeshVoxelization.Editor;
using VoxelEngine.Showcase;
using VoxelEngine.Showcase.Editor;
using VoxelEngine.Structures.Runtime.MeshImport;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class MountainDragonBakeGenerationTests
    {
        [Test]
        public void FormatMetrics_EmitsStableStructuralFactsWithoutPinnedSource()
        {
            var bake = new BakedVoxelStructure(
                0.3f,
                new int3(-1, 2, 3),
                new int3(4, 5, 6),
                new[]
                {
                    new BakedVoxelCell(new int3(0, 0, 0), 7),
                    new BakedVoxelCell(new int3(1, 0, 0), 7),
                },
                sourceTriangleCount: 29_734,
                voxelizationMilliseconds: 12.5,
                boundaryEdgeCount: 4,
                nonManifoldEdgeCount: 2,
                interiorFilled: true);
            BakedVoxelStructureStats stats = MeshVoxelizationMetrics.Analyze(bake);
            string encoded = BakedVoxelStructureCodec.Encode(bake);
            var result = new MountainDragonBakeGenerator.Result(bake, stats, encoded);

            string metrics = MountainDragonBakeGenerator.FormatMetrics(in result);

            Assert.That(metrics, Does.Contain("sourceTriangles=29734\n"));
            Assert.That(metrics, Does.Contain("voxelSize=0.3\n"));
            Assert.That(metrics, Does.Contain("denseEnvelopeCells=120\n"));
            Assert.That(metrics, Does.Contain("authoredVoxelCount=2\n"));
            Assert.That(metrics, Does.Contain("surfaceVoxelCount=2\n"));
            Assert.That(metrics, Does.Contain("connectedComponents=1\n"));
            Assert.That(metrics, Does.Contain("materialCount=1\n"));
            Assert.That(metrics, Does.Contain("sparseBrickCount=1\n"));
            Assert.That(metrics, Does.Contain("boundaryEdges=4\n"));
            Assert.That(metrics, Does.Contain("nonManifoldEdges=2\n"));
            Assert.That(metrics, Does.Contain("interiorFilled=True\n"));
            Assert.That(metrics, Does.Contain("voxelizationMilliseconds=12.500\n"));
            Assert.That(metrics, Does.Contain($"serializedBytes={result.SerializedByteCount}\n"));
        }

        [Test]
        public void CheckedInBake_MeetsPinnedSourceFidelityTargets()
        {
            MountainDragonSourceArchive.ReconstructImportedAsset();
            GameObject sourceRoot = AssetDatabase.LoadAssetAtPath<GameObject>(
                MountainDragonSourceArchive.GeneratedAssetPath);
            Assert.That(sourceRoot, Is.Not.Null,
                "The exact reconstructed support-free OBJ must import before fidelity measurement.");

            MeshVoxelizationSource source = UnityMeshVoxelizationAdapter.BuildSource(
                sourceRoot,
                MountainDragonPalettePolicy.DragonMaterial);
            Assert.That(source.Triangles.Length,
                Is.EqualTo(MountainDragonVoxelBakePolicy.ExpectedSourceTriangleCount));

            BakedVoxelStructure bake = MountainDragonBakedArtifact.Load();
            MeshVoxelFidelityReport fidelity = MeshVoxelizationMetrics.Measure(
                in source,
                bake,
                maxSamplesPerSurface: 2048,
                silhouetteResolution: 192);

            TestContext.Out.WriteLine(
                $"sourceSamples={fidelity.SourceSampleCount}\n" +
                $"voxelSamples={fidelity.VoxelSampleCount}\n" +
                $"symmetricP95Voxels={fidelity.SymmetricP95Voxels:F4}\n" +
                $"frontSilhouetteIoU={fidelity.FrontSilhouetteIoU:F4}\n" +
                $"sideSilhouetteIoU={fidelity.SideSilhouetteIoU:F4}\n" +
                $"topSilhouetteIoU={fidelity.TopSilhouetteIoU:F4}\n");

            Assert.That(fidelity.SymmetricP95Voxels, Is.LessThanOrEqualTo(1.5f),
                "The checked-in Dragon bake must remain within the ticket's sampled symmetric surface-error target.");
            Assert.That(fidelity.MinPrimarySilhouetteIoU, Is.GreaterThanOrEqualTo(0.90f),
                "Front/side/top Dragon silhouettes must each satisfy the ticket's primary-view overlap target.");
        }

        [Test]
        public void GeneratePinnedBake_ProducesValidatedSparseArtifact()
        {
            MountainDragonBakeGenerator.Result result =
                MountainDragonBakeGenerator.GeneratePinnedBakeAndWriteArtifact();
            BakedVoxelStructure bake = result.Bake;

            Assert.That(bake.SourceTriangleCount,
                Is.EqualTo(MountainDragonVoxelBakePolicy.ExpectedSourceTriangleCount));
            Assert.That(bake.Cells, Is.Not.Empty);
            Assert.That(bake.InteriorFilled, Is.True,
                "The pinned dragon must produce a volumetric voxel shell fill.");
            Assert.That(math.all(bake.Size <= MountainDragonVoxelBakePolicy.MaximumStructureSize), Is.True);
            Assert.That(bake.VoxelSize, Is.EqualTo(MountainDragonVoxelBakePolicy.SourceVoxelSize));
            Assert.That(result.SerializedByteCount, Is.GreaterThan(0));
            Assert.That(result.Stats.CellCount, Is.EqualTo(bake.Cells.Length));
            Assert.That(result.Stats.SparseBrickCount, Is.GreaterThan(0));

            for (int i = 0; i < bake.Cells.Length; i++)
                Assert.That(bake.Cells[i].Material,
                    Is.EqualTo(MountainDragonPalettePolicy.DragonMaterial),
                    $"Unexpected material at sparse cell {i}.");

            BakedVoxelStructure decoded = BakedVoxelStructureCodec.Decode(result.Encoded);
            Assert.That(decoded.SourceTriangleCount, Is.EqualTo(bake.SourceTriangleCount));
            Assert.That(decoded.Size, Is.EqualTo(bake.Size));
            Assert.That(decoded.GridOrigin, Is.EqualTo(bake.GridOrigin));
            Assert.That(decoded.Cells.Length, Is.EqualTo(bake.Cells.Length));

            string metricsPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                MountainDragonBakeGenerator.DefaultMetricsPath);
            Assert.That(File.Exists(metricsPath), Is.True);
            string metrics = File.ReadAllText(metricsPath);
            Assert.That(metrics, Is.EqualTo(MountainDragonBakeGenerator.FormatMetrics(in result)));
            TestContext.Out.WriteLine(metrics);
        }
    }
}
