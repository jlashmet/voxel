using Game.Materials.Api;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Rendering.Runtime.GpuVoxel;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction.Transvoxel;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class TransvoxelChunkBoundaryOwnershipTests
    {
        [Test]
        public void SceneIssue20260823013834177BoundaryCellBelongsToInsideDensitySide()
        {
            const int cellsPerAxis = 64;
            int3 negativeChunkCell = new(63, 20, 20);
            int3 positiveChunkShellCell = new(-1, 20, 20);

            Assert.False(TransvoxelTopologyJob.OwnsSelectedInsideSample(
                negativeChunkCell, selectedInsideCorner: 1, cellsPerAxis));
            Assert.True(TransvoxelTopologyJob.OwnsSelectedInsideSample(
                positiveChunkShellCell, selectedInsideCorner: 1, cellsPerAxis));
            Assert.True(TransvoxelTopologyJob.OwnsSelectedInsideSample(
                negativeChunkCell, selectedInsideCorner: 0, cellsPerAxis));
            Assert.False(TransvoxelTopologyJob.OwnsSelectedInsideSample(
                positiveChunkShellCell, selectedInsideCorner: 0, cellsPerAxis));
        }

        [Test]
        public void SceneIssueOwnershipIgnoresDominantMaterialOnOutsideCorner()
        {
            float[] density = { -0.40f, 0.30f };
            byte[] dominantMaterial = { 1, 1 };
            int selectedByMaterial = -1;
            int selectedByDensity = -1;
            for (int i = 0; i < 2; i++)
            {
                if (selectedByMaterial < 0 && dominantMaterial[i] != 0) selectedByMaterial = i;
                if (selectedByDensity < 0 && density[i] >= 0f) selectedByDensity = i;
            }

            Assert.AreEqual(0, selectedByMaterial);
            Assert.AreEqual(1, selectedByDensity);
            Assert.True(TransvoxelTopologyJob.OwnsSelectedInsideSample(
                new int3(-1, 20, 20), selectedByDensity, 64));
        }

        [Test]
        public void SceneIssue20260823014011920CoarseTerrainUsesExposedCapMaterial()
        {
            SurfaceCatalogueView surfaces = SurfaceCatalogueView.CreateBuiltIns();
            CoatingCatalogueView coatings = default;
            MaterialPaletteView palette = default;

            // Source steps 2 and 4 use the exact CPU density path. Worst alignment places the
            // lattice point as far below a one-voxel turf cap as possible while the next coarse
            // endpoint is already air.
            foreach (int sourceStep in new[] { 2, 4 })
            {
                CpuDensitySample buriedDirt = CpuDensityOracle.SampleLayeredColumnAtOrigin(
                    sourceStep,
                    topSolidY: sourceStep - 1,
                    surfaceMaterial: GameMaterialIds.Grass,
                    subsurfaceMaterial: GameMaterialIds.Dirt,
                    surfaces, coatings, palette);

                Assert.That(buriedDirt.Density, Is.GreaterThan(0f),
                    $"Step {sourceStep}: the lattice point should remain on the solid side of the reconstructed surface.");
                Assert.AreEqual(GameMaterialIds.Grass, buriedDirt.Material,
                    $"Step {sourceStep}: a coarse sample below turf must render the exposed grass cap, not buried dirt; "
                  + "otherwise coarse rings turn subsurface depth into topographic colour bands.");

                CpuDensitySample exposedDirt = CpuDensityOracle.SampleLayeredColumnAtOrigin(
                    sourceStep,
                    topSolidY: 0,
                    surfaceMaterial: GameMaterialIds.Dirt,
                    subsurfaceMaterial: GameMaterialIds.Dirt,
                    surfaces, coatings, palette);

                Assert.That(exposedDirt.Density, Is.GreaterThan(0f),
                    $"Step {sourceStep}: exposed dirt should remain on the solid side.");
                Assert.AreEqual(GameMaterialIds.Dirt, exposedDirt.Material,
                    $"Step {sourceStep}: authored dirt that is genuinely exposed at the surface must remain dirt.");
            }

            // Source step 8 deliberately switches to the feature-preserving block HLOD backend.
            // Guard that backend separately rather than accidentally testing the exact-density job
            // at a stride production never sends through it.
            var hlodVoxels = new NativeArray<byte>(
                SurfaceBlockHlodSummaryBuilder.VoxelsPerBlock, Allocator.TempJob);
            try
            {
                for (int z = 0; z < SurfaceBlockHlodSummaryBuilder.BlockEdge; z++)
                for (int y = 0; y < SurfaceBlockHlodSummaryBuilder.BlockEdge; y++)
                for (int x = 0; x < SurfaceBlockHlodSummaryBuilder.BlockEdge; x++)
                {
                    int index = x | (y << 3) | (z << 6);
                    hlodVoxels[index] = y == 7
                        ? GameMaterialIds.Grass
                        : GameMaterialIds.Dirt;
                }

                SurfaceBlockHlodSummary grassCap = SurfaceBlockHlodSummaryBuilder.Mixed(hlodVoxels, 0);
                for (int z = 0; z < SurfaceBlockHlodSummaryBuilder.SubcellsPerAxis; z++)
                for (int x = 0; x < SurfaceBlockHlodSummaryBuilder.SubcellsPerAxis; x++)
                {
                    int topSubcell = x
                                   + 3 * SurfaceBlockHlodSummaryBuilder.SubcellsPerAxis
                                   + z * SurfaceBlockHlodSummaryBuilder.SubcellsPerAxis
                                       * SurfaceBlockHlodSummaryBuilder.SubcellsPerAxis;
                    Assert.True(grassCap.IsOccupied(topSubcell));
                    Assert.AreEqual(GameMaterialIds.Grass, grassCap.MaterialAt(topSubcell),
                        "Step 8 HLOD must vote from the exposed turf cap rather than buried dirt.");
                }

                // Remove the turf cap while leaving dirt at y=6. The same top HLOD subcells remain
                // occupied, but their genuinely exposed material must now be Dirt.
                for (int z = 0; z < SurfaceBlockHlodSummaryBuilder.BlockEdge; z++)
                for (int x = 0; x < SurfaceBlockHlodSummaryBuilder.BlockEdge; x++)
                    hlodVoxels[x | (7 << 3) | (z << 6)] = 0;

                SurfaceBlockHlodSummary exposedHlodDirt = SurfaceBlockHlodSummaryBuilder.Mixed(hlodVoxels, 0);
                for (int z = 0; z < SurfaceBlockHlodSummaryBuilder.SubcellsPerAxis; z++)
                for (int x = 0; x < SurfaceBlockHlodSummaryBuilder.SubcellsPerAxis; x++)
                {
                    int topSubcell = x
                                   + 3 * SurfaceBlockHlodSummaryBuilder.SubcellsPerAxis
                                   + z * SurfaceBlockHlodSummaryBuilder.SubcellsPerAxis
                                       * SurfaceBlockHlodSummaryBuilder.SubcellsPerAxis;
                    Assert.True(exposedHlodDirt.IsOccupied(topSubcell));
                    Assert.AreEqual(GameMaterialIds.Dirt, exposedHlodDirt.MaterialAt(topSubcell),
                        "Step 8 HLOD must preserve dirt when dirt is actually the exposed surface.");
                }
            }
            finally
            {
                hlodVoxels.Dispose();
            }
        }

        [Test]
        public void BoundaryLaneCompactsMultipleCellRecordsWithoutLosingIndices()
        {
            using var stream = new NativeStream(1, Allocator.TempJob);
            NativeStream.Writer writer = stream.AsWriter();
            writer.BeginForEachIndex(0);

            var first = new SmoothSurfaceVertex { Material = 101u };
            writer.Write((byte)0);
            writer.Write((byte)1);
            writer.Write((byte)1);
            writer.Write(first);
            writer.Write((byte)0);

            var second = new SmoothSurfaceVertex { Material = 202u };
            writer.Write((byte)0);
            writer.Write((byte)1);
            writer.Write((byte)1);
            writer.Write(second);
            writer.Write((byte)0);
            writer.EndForEachIndex();

            using var vertices = new NativeList<SmoothSurfaceVertex>(4, Allocator.TempJob);
            using var indices = new NativeList<uint>(4, Allocator.TempJob);
            using var overflow = new NativeArray<int>(1, Allocator.TempJob);
            new TransvoxelCompactJob
            {
                Input = stream.AsReader(),
                Vertices = vertices,
                Indices = indices,
                OverflowCell = overflow,
            }.Execute();

            Assert.AreEqual(-1, overflow[0]);
            Assert.AreEqual(2, vertices.Length);
            Assert.AreEqual(101u, vertices[0].Material);
            Assert.AreEqual(202u, vertices[1].Material);
            Assert.AreEqual(2, indices.Length);
            Assert.AreEqual(0u, indices[0]);
            Assert.AreEqual(1u, indices[1]);
        }

        [Test]
        public void MinimumFaceWithNoSolidHaloFallsBackFromGpu()
        {
            const int edge = 5;
            const int padding = 1;
            const int coreEdge = 3;
            var bricks = new NativeArray<TransvoxelDensityBrick>(edge * edge * edge, Allocator.TempJob);
            var mixed = new NativeArray<byte>(1, Allocator.TempJob);
            try
            {
                int y = 2;
                int z = 2;
                int core = 1 + edge * (y + edge * z);
                int halo = 0 + edge * (y + edge * z);
                bricks[core] = new TransvoxelDensityBrick
                {
                    Kind = 1,
                    UniformMaterial = 1,
                    MixedOffset = 0,
                };

                Assert.True(ExactSnapshotClassificationJob.LowBoundaryTouchesNoSolidHalo(
                    bricks, mixed, 1, y, z, edge, padding, coreEdge),
                    "A solid minimum-face brick with an all-air predecessor needs the CPU negative shell.");

                bricks[halo] = new TransvoxelDensityBrick
                {
                    Kind = 1,
                    UniformMaterial = 1,
                    MixedOffset = 0,
                };
                Assert.False(ExactSnapshotClassificationJob.LowBoundaryTouchesNoSolidHalo(
                    bricks, mixed, 1, y, z, edge, padding, coreEdge),
                    "A solid predecessor can publish the shared crossing, so GPU extraction remains eligible.");

                Assert.False(ExactSnapshotClassificationJob.LowBoundaryTouchesNoSolidHalo(
                    bricks, mixed, 2, y, z, edge, padding, coreEdge),
                    "Interior bricks must never trigger the boundary fallback.");
            }
            finally
            {
                mixed.Dispose();
                bricks.Dispose();
            }
        }
    }
}
