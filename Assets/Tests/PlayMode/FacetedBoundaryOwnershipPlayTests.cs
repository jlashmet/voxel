using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction.Transvoxel;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class FacetedBoundaryOwnershipPlayTests
    {
        [Test]
        public void DensityPresentationHaloCannotEraseAuthoritativePlanarCap()
        {
            const int gridSize = 3;
            const int brickVoxelCount = 8 * 8 * 8;
            var bricks = new NativeArray<TransvoxelDensityBrick>(1, Allocator.Temp);
            var voxels = new NativeArray<byte>(brickVoxelCount, Allocator.Temp);
            var storedSurfaces = new NativeArray<ushort>(brickVoxelCount, Allocator.Temp);
            var storedBoundaries = new NativeArray<byte>(brickVoxelCount, Allocator.Temp);
            var density = new NativeArray<float>(gridSize * gridSize * gridSize, Allocator.Temp);
            var materials = new NativeArray<byte>(density.Length, Allocator.Temp);
            var latticeSurfaces = new NativeArray<uint>(density.Length, Allocator.Temp);
            var latticeBoundaries = new NativeArray<byte>(density.Length, Allocator.Temp);
            var masks = new NativeArray<uint>(6, Allocator.Temp);
            try
            {
                bricks[0] = new TransvoxelDensityBrick { Kind = 2, MixedOffset = 0 };

                int backing = BrickIndex(1, 1, 1);
                voxels[backing] = 1;
                storedSurfaces[backing] = new VoxelSurfaceSemantics
                {
                    StyleId = SurfaceStyles.MasonryJoint,
                }.PackedStorage;

                int roundedBesideAir = BrickIndex(2, 2, 1);
                voxels[roundedBesideAir] = 2;
                storedSurfaces[roundedBesideAir] = new VoxelSurfaceSemantics
                {
                    StyleId = SurfaceStyles.Rounded,
                }.PackedStorage;

                var densityJob = new TransvoxelDensityJob
                {
                    Bricks = bricks,
                    MixedVoxels = voxels,
                    MixedSurfaceSemantics = storedSurfaces,
                    MixedBoundarySamples = storedBoundaries,
                    Palette = default,
                    Catalogue = SurfaceCatalogueView.CreateBuiltIns(),
                    Coatings = CoatingCatalogueView.CreateBuiltIns(),
                    Density = density,
                    Materials = materials,
                    SurfaceSemantics = latticeSurfaces,
                    BoundarySamples = latticeBoundaries,
                    ChunkOriginVoxel = new int3(1, 1, 1),
                    BrickCacheOrigin = int3.zero,
                    BrickCacheEdge = 1,
                    GridSize = gridSize,
                    Padding = 1,
                    SourceStep = 1,
                };
                for (int i = 0; i < density.Length; i++) densityJob.Execute(i);

                int backingSample = Index(1, 1, 1, gridSize);
                int airAboveSample = Index(1, 2, 1, gridSize);
                Assert.That(materials[airAboveSample], Is.EqualTo(2),
                    "Fixture must reproduce the solid presentation material on authoritative air.");
                Assert.That(density[airAboveSample], Is.LessThan(0f));
                Assert.That(TransvoxelDensityJob.IsAuthoritativelySolid(latticeSurfaces[backingSample]),
                    Is.True);
                Assert.That(TransvoxelDensityJob.IsAuthoritativelySolid(latticeSurfaces[airAboveSample]),
                    Is.False);

                var maskJob = new FacetedMaskJob
                {
                    Materials = materials,
                    SurfaceSemantics = latticeSurfaces,
                    BoundarySamples = latticeBoundaries,
                    Catalogue = SurfaceCatalogueView.CreateBuiltIns(),
                    Coatings = CoatingCatalogueView.CreateBuiltIns(),
                    CellsPerAxis = 1,
                    GridSize = gridSize,
                    Padding = 1,
                    FaceMasks = masks,
                };
                maskJob.Execute(0);

                Assert.That(masks[3], Is.Not.Zero,
                    "The production density -> faceted-mask path must preserve the Planar cap.");
            }
            finally
            {
                masks.Dispose();
                latticeBoundaries.Dispose();
                latticeSurfaces.Dispose();
                materials.Dispose();
                density.Dispose();
                storedBoundaries.Dispose();
                storedSurfaces.Dispose();
                voxels.Dispose();
                bricks.Dispose();
            }
        }

        private static int Index(int x, int y, int z, int size) => x + size * (y + size * z);
        private static int BrickIndex(int x, int y, int z) => x | (y << 3) | (z << 6);
    }
}
