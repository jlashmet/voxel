using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction.Transvoxel;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class FacetedBoundaryOwnershipTests
    {
        [Test]
        public void PlanarBackingFaceSurvivesUnrelatedRoundedVeneerHalo()
        {
            const int gridSize = 3;
            var materials = new NativeArray<byte>(gridSize * gridSize * gridSize, Allocator.Temp);
            var surfaces = new NativeArray<uint>(materials.Length, Allocator.Temp);
            var boundaries = new NativeArray<byte>(materials.Length, Allocator.Temp);
            var masks = new NativeArray<uint>(6, Allocator.Temp);
            try
            {
                int backing = Index(1, 1, 1, gridSize);
                int emptyAbove = Index(1, 2, 1, gridSize);
                materials[backing] = 1;
                surfaces[backing] = TransvoxelDensityJob.WithAuthoritativeOccupancy(
                    SurfaceStyles.MasonryJoint, solid: true);

                // A rounded veneer beside the backing can leave its in-plane halo on this empty
                // cell. That halo describes the veneer, not the backing's exact planar top face.
                boundaries[emptyAbove] =
                    VoxelBoundarySample.FromSignedQ4(-8, extrusionAxis: 2).Packed;

                var job = new FacetedMaskJob
                {
                    Materials = materials,
                    SurfaceSemantics = surfaces,
                    BoundarySamples = boundaries,
                    Catalogue = SurfaceCatalogueView.CreateBuiltIns(),
                    Coatings = default,
                    CellsPerAxis = 1,
                    GridSize = gridSize,
                    Padding = 1,
                    FaceMasks = masks,
                };
                job.Execute(0);

                const int positiveYFace = 3;
                Assert.That(masks[positiveYFace], Is.Not.Zero,
                    "An empty neighbour's unrelated curved halo must not steal ownership of "
                  + "the occupied planar cell's exact exposed face.");
            }
            finally
            {
                masks.Dispose();
                boundaries.Dispose();
                surfaces.Dispose();
                materials.Dispose();
            }
        }

        [Test]
        public void SnapshotMaskPreservesPlanarCapWhenAuthoritativeNeighbourIsAir()
        {
            const int brickVoxelCount = 8 * 8 * 8;
            var bricks = new NativeArray<TransvoxelDensityBrick>(1, Allocator.Temp);
            var voxels = new NativeArray<byte>(brickVoxelCount, Allocator.Temp);
            var surfaces = new NativeArray<ushort>(brickVoxelCount, Allocator.Temp);
            var boundaries = new NativeArray<byte>(brickVoxelCount, Allocator.Temp);
            var masks = new NativeArray<uint>(6, Allocator.Temp);
            try
            {
                bricks[0] = new TransvoxelDensityBrick { Kind = 2, MixedOffset = 0 };

                int backing = BrickIndex(1, 1, 1);
                int authoritativeAirAbove = BrickIndex(1, 2, 1);
                voxels[backing] = 1;
                surfaces[backing] = new VoxelSurfaceSemantics
                {
                    StyleId = SurfaceStyles.MasonryJoint,
                }.PackedStorage;

                Assert.That(voxels[authoritativeAirAbove], Is.Zero,
                    "The regression fixture must keep the neighbour authoritative-air even though "
                  + "continuous density sampling may carry a nearby solid presentation material there.");

                var job = new SnapshotFacetedMaskJob
                {
                    Bricks = bricks,
                    MixedVoxels = voxels,
                    MixedSurfaceSemantics = surfaces,
                    MixedBoundarySamples = boundaries,
                    Catalogue = SurfaceCatalogueView.CreateBuiltIns(),
                    Coatings = CoatingCatalogueView.CreateBuiltIns(),
                    ChunkOriginVoxel = new int3(1, 1, 1),
                    BrickCacheOrigin = int3.zero,
                    BrickCacheEdge = 1,
                    CellsPerAxis = 1,
                    SourceStep = 1,
                    FaceMasks = masks,
                };
                job.Execute(0);

                const int positiveYFace = 3;
                Assert.That(masks[positiveYFace], Is.Not.Zero,
                    "Exact faceted exposure must follow authoritative snapshot occupancy so a "
                  + "presentation-only solid sample cannot erase a Planar cap.");
            }
            finally
            {
                masks.Dispose();
                boundaries.Dispose();
                surfaces.Dispose();
                voxels.Dispose();
                bricks.Dispose();
            }
        }

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

                // Planar backing with authoritative air above it. A rounded solid beside that air
                // sample contributes presentation identity to the smooth density field.
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
                    "The fixture must reproduce the production presentation halo: an air-centred "
                  + "sample carries the rounded neighbour's solid material identity.");
                Assert.That(density[airAboveSample], Is.LessThan(0f),
                    "The halo sample must remain on the outside of the continuous scalar field.");
                Assert.That(TransvoxelDensityJob.IsAuthoritativelySolid(latticeSurfaces[backingSample]),
                    Is.True, "The occupied Planar centre must retain authoritative occupancy.");
                Assert.That(TransvoxelDensityJob.IsAuthoritativelySolid(latticeSurfaces[airAboveSample]),
                    Is.False, "Presentation material must not turn authoritative air into occupancy.");

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

                const int positiveYFace = 3;
                Assert.That(masks[positiveYFace], Is.Not.Zero,
                    "The mixed continuous density -> faceted-mask path must preserve the exact "
                  + "Planar cap when its authoritative neighbour is air.");
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
