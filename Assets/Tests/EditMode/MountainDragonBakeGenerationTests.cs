using System.IO;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Showcase;
using VoxelEngine.Showcase.Editor;
using VoxelEngine.Structures.Runtime.MeshImport;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class MountainDragonBakeGenerationTests
    {
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
                "Artifacts/SingleTest/mountain-dragon-bake-metrics.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(metricsPath));
            long denseCells = (long)bake.Size.x * bake.Size.y * bake.Size.z;
            string metrics =
                $"sourceTriangles={bake.SourceTriangleCount}\n" +
                $"voxelSize={bake.VoxelSize:R}\n" +
                $"gridOrigin={bake.GridOrigin.x},{bake.GridOrigin.y},{bake.GridOrigin.z}\n" +
                $"size={bake.Size.x},{bake.Size.y},{bake.Size.z}\n" +
                $"denseEnvelopeCells={denseCells}\n" +
                $"authoredVoxelCount={bake.Cells.Length}\n" +
                $"boundaryEdges={bake.BoundaryEdgeCount}\n" +
                $"nonManifoldEdges={bake.NonManifoldEdgeCount}\n" +
                $"interiorFilled={bake.InteriorFilled}\n" +
                $"voxelizationMilliseconds={bake.VoxelizationMilliseconds:F3}\n" +
                $"serializedBytes={result.SerializedByteCount}\n";
            File.WriteAllText(metricsPath, metrics);
            TestContext.Out.WriteLine(metrics);
        }
    }
}
