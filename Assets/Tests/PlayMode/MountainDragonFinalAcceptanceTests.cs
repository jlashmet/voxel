using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Storage.Runtime;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;
using VoxelEngine.Structures.Runtime.Emitters;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// Exact single-filter entry point for the SceneIssue CI transport. Keep the focused structural,
    /// semantic bake, and real-traversal predicates together so one targeted request proves the
    /// naturalized landform contract before the workflow launches the exact built-scene replay.
    /// </summary>
    public sealed class MountainDragonFinalAcceptanceTests
    {
        private const byte MountainMaterial = 1;
        private const byte PathMaterial = 13;

        [Test]
        public void NaturalizedMountainBakeAndEncounterAreReadyForBuiltPlayerReplay()
        {
            var startup = new MountainDragonStartupBakeAcceptanceTests();
            startup.MountainLandformProgramUsesMultipleAsymmetricMasses();

            var ownership = new MountainDragonCastleRegionOwnershipTests();
            ownership.MountainFootprintDoesNotEnterCastleOwnedFeatureSuppressionRegions();

            var support = new MountainDragonNaturalSupportProgramTests();
            support.MountainPathSupportUsesTaperedMassesWithoutTallRetainingWallBoxes();
            support.OfflineBakeFarFieldSuppressionIsScopedAndRestored();
            support.BoxCarveSkipsEmptyAtomicallyClearsUniformAndPreservesMixedAndPartialAccounting();

            FrustumFillAtomicallyAuthorsFullyInteriorEmptyBlockWithoutOverfillingBoundaryBlock();
            FrustumFillIfEmptyAtomicallyAuthorsEmptySkipsUniformAndPreservesMixedAndBoundaryBlocks();

            var headroom = new MountainDragonPathHeadroomBakeTests();
            headroom.PreparedStartupBakeKeepsPlayerClearAirAboveEveryMountainPathTier();

            var traversal = new ShowcaseWaypointTraversalContractTests();
            traversal.AnchoredVerticalBandRejectsFlatOrAirborneFalseArrival();

            startup.PreparedStartupBakeContainsMountainPathAndSupportedDragonAndExportsEvidence();
        }

        [Test]
        public void FrustumFillAtomicallyAuthorsFullyInteriorEmptyBlockWithoutOverfillingBoundaryBlock()
        {
            var table = new RegionTable(1, Allocator.Temp);
            var pool = new BrickPool(8, Allocator.Temp);

            try
            {
                table.LoadRegion(int3.zero);
                var reads = new RegionReadSource(in table, in pool);

                Primitive interiorFrustum = CurvedPrimitiveEmitter.Frustum(
                    new int3(4, 0, 4),
                    32,
                    24,
                    24,
                    1,
                    MountainMaterial,
                    SurfaceStyles.MaterialDefault,
                    PrimitiveMode.Fill,
                    0);
                int3 interiorMin = new(0, VoxelReadGrid.BlockEdge, 0);
                int3 interiorMax = interiorMin + VoxelReadGrid.BlockEdge;
                var interiorMutations = new CountingMutationStore(
                    new RegionMutationStore(in table, in pool));

                RasterResult interiorResult = PrimitiveRasteriser.RasterisePrimitive(
                    in interiorFrustum,
                    interiorMin,
                    interiorMax,
                    reads,
                    interiorMutations);
                reads.Refresh(in table, in pool);

                Assert.That(interiorMutations.WholeCellBlockCalls, Is.EqualTo(1),
                    "A canonical-empty 8^3 block wholly inside a filled frustum must use one "
                    + "authoritative whole-cell replacement instead of 512 cell mutations.");
                Assert.That(interiorMutations.BeginCellBlockCalls, Is.Zero,
                    "A fully interior empty frustum block must not materialize the partial-cell path.");
                Assert.That(interiorResult.VoxelsWritten, Is.EqualTo(VoxelReadGrid.VoxelsPerBlock),
                    "Atomic interior frustum fill must preserve the existing 512 logical writes.");
                Assert.That(reads.TryAcquireRegionContainingBlock(
                    new int3(0, 1, 0), out RegionReadView interiorView), Is.True);
                Assert.That(interiorView.TryGetWorldBlock(
                    new int3(0, 1, 0), out VoxelReadBlock interiorBlock), Is.True);
                Assert.That(interiorBlock.Kind, Is.EqualTo(VoxelReadBlockKind.Uniform));
                Assert.That(interiorBlock.UniformMaterial, Is.EqualTo(MountainMaterial));
                Assert.That(interiorView.TryReadCell(
                    new int3(4, VoxelReadGrid.BlockEdge, 4), out VoxelCell deepInterior), Is.True);
                Assert.That(deepInterior.Boundary.IsAuthored, Is.False,
                    "A frustum cell more than two voxels from every analytic surface must not "
                    + "receive boundary metadata from the halo pass.");

                Primitive edgeFrustum = CurvedPrimitiveEmitter.Frustum(
                    new int3(4, 0, 4),
                    32,
                    16,
                    16,
                    1,
                    MountainMaterial,
                    SurfaceStyles.MaterialDefault,
                    PrimitiveMode.Fill,
                    0);
                int3 edgeMin = new(VoxelReadGrid.BlockEdge * 2, VoxelReadGrid.BlockEdge, 0);
                int3 edgeMax = edgeMin + VoxelReadGrid.BlockEdge;
                var edgeMutations = new CountingMutationStore(
                    new RegionMutationStore(in table, in pool));

                RasterResult edgeResult = PrimitiveRasteriser.RasterisePrimitive(
                    in edgeFrustum,
                    edgeMin,
                    edgeMax,
                    reads,
                    edgeMutations);
                reads.Refresh(in table, in pool);

                Assert.That(edgeMutations.WholeCellBlockCalls, Is.Zero,
                    "A frustum boundary block must remain on the exact per-cell path; corner "
                    + "rejection must prevent whole-block overfill.");
                Assert.That(edgeMutations.BeginCellBlockCalls, Is.GreaterThan(0),
                    "A partially covered frustum block must still author its contained cells.");
                Assert.That(edgeResult.VoxelsWritten, Is.GreaterThan(0));
                Assert.That(edgeResult.VoxelsWritten, Is.LessThan(VoxelReadGrid.VoxelsPerBlock));
                Assert.That(reads.TryAcquireRegionContainingBlock(
                    new int3(2, 1, 0), out RegionReadView edgeView), Is.True);
                Assert.That(edgeView.TryReadCell(
                    new int3(VoxelReadGrid.BlockEdge * 2, VoxelReadGrid.BlockEdge, 4), out VoxelCell inside), Is.True);
                Assert.That(inside.BaseMaterialId, Is.EqualTo(MountainMaterial));
                Assert.That(edgeView.TryReadCell(
                    new int3(20, VoxelReadGrid.BlockEdge, 4), out VoxelCell surfaceInside), Is.True);
                Assert.That(surfaceInside.IsSolid, Is.True);
                Assert.That(surfaceInside.Boundary.IsAuthored, Is.True,
                    "A near-surface filled frustum cell must retain its authored positive boundary sample.");
                Assert.That(surfaceInside.Boundary.SignedQ4, Is.GreaterThan(0));
                Assert.That(edgeView.TryReadCell(
                    new int3(21, VoxelReadGrid.BlockEdge, 4), out VoxelCell surfaceOutside), Is.True);
                Assert.That(surfaceOutside.IsSolid, Is.False);
                Assert.That(surfaceOutside.Boundary.IsAuthored, Is.True,
                    "The adjacent empty-side halo cell must retain its authored negative boundary sample.");
                Assert.That(surfaceOutside.Boundary.SignedQ4, Is.LessThan(0));
                Assert.That(edgeView.TryReadCell(
                    new int3(VoxelReadGrid.BlockEdge * 3 - 1, VoxelReadGrid.BlockEdge, 4), out VoxelCell outside), Is.True);
                Assert.That(outside.IsSolid, Is.False,
                    "The far side of a boundary block must remain empty after frustum rasterization.");
            }
            finally
            {
                table.Dispose();
                pool.Dispose();
            }
        }

        [Test]
        public void FrustumFillIfEmptyAtomicallyAuthorsEmptySkipsUniformAndPreservesMixedAndBoundaryBlocks()
        {
            var table = new RegionTable(1, Allocator.Temp);
            var pool = new BrickPool(8, Allocator.Temp);

            try
            {
                table.LoadRegion(int3.zero);
                var reads = new RegionReadSource(in table, in pool);
                var setup = new RegionMutationStore(in table, in pool);
                int3 interiorBlockCoord = new(0, 1, 0);
                int3 interiorMin = interiorBlockCoord << VoxelReadGrid.BlockEdgeLog2;
                int3 interiorMax = interiorMin + VoxelReadGrid.BlockEdge;
                Primitive supportFrustum = CurvedPrimitiveEmitter.Frustum(
                    new int3(4, 0, 4),
                    32,
                    24,
                    24,
                    1,
                    MountainMaterial,
                    SurfaceStyles.MaterialDefault,
                    PrimitiveMode.FillIfEmpty,
                    0);

                var emptyMutations = new CountingMutationStore(
                    new RegionMutationStore(in table, in pool));
                RasterResult emptyResult = PrimitiveRasteriser.RasterisePrimitive(
                    in supportFrustum,
                    interiorMin,
                    interiorMax,
                    reads,
                    emptyMutations);
                reads.Refresh(in table, in pool);

                Assert.That(emptyMutations.WholeCellBlockCalls, Is.EqualTo(1),
                    "A canonical-empty 8^3 block wholly inside a FillIfEmpty support frustum must "
                    + "use one authoritative whole-cell replacement instead of 512 reads/writes.");
                Assert.That(emptyMutations.BeginCellBlockCalls, Is.Zero);
                Assert.That(emptyResult.VoxelsWritten, Is.EqualTo(VoxelReadGrid.VoxelsPerBlock),
                    "Atomic FillIfEmpty on canonical Empty must retain exact 512 logical writes.");
                Assert.That(reads.TryAcquireRegionContainingBlock(interiorBlockCoord, out RegionReadView emptyView), Is.True);
                Assert.That(emptyView.TryGetWorldBlock(interiorBlockCoord, out VoxelReadBlock filledBlock), Is.True);
                Assert.That(filledBlock.Kind, Is.EqualTo(VoxelReadBlockKind.Uniform));
                Assert.That(filledBlock.UniformMaterial, Is.EqualTo(MountainMaterial));

                setup.Refresh(in table, in pool);
                Assert.That(setup.SetWholeBlock(interiorBlockCoord, PathMaterial, false), Is.True,
                    "Uniform-solid fixture must replace the prior mountain block.");
                reads.Refresh(in table, in pool);
                var uniformMutations = new CountingMutationStore(
                    new RegionMutationStore(in table, in pool));
                RasterResult uniformResult = PrimitiveRasteriser.RasterisePrimitive(
                    in supportFrustum,
                    interiorMin,
                    interiorMax,
                    reads,
                    uniformMutations);
                reads.Refresh(in table, in pool);

                Assert.That(uniformResult.VoxelsWritten, Is.Zero,
                    "FillIfEmpty must remain an exact no-op for a fully solid Uniform block.");
                Assert.That(uniformMutations.WholeCellBlockCalls, Is.Zero,
                    "Uniform-solid FillIfEmpty must not overwrite existing authored material.");
                Assert.That(uniformMutations.BeginCellBlockCalls, Is.Zero,
                    "Uniform-solid FillIfEmpty must not materialize a partial mutation.");
                Assert.That(reads.TryAcquireRegionContainingBlock(interiorBlockCoord, out RegionReadView uniformView), Is.True);
                Assert.That(uniformView.TryGetWorldBlock(interiorBlockCoord, out VoxelReadBlock uniformBlock), Is.True);
                Assert.That(uniformBlock.Kind, Is.EqualTo(VoxelReadBlockKind.Uniform));
                Assert.That(uniformBlock.UniformMaterial, Is.EqualTo(PathMaterial));

                setup.Refresh(in table, in pool);
                VoxelCell empty = default;
                Assert.That(setup.SetWholeCellBlock(interiorBlockCoord, in empty, false), Is.True);
                Assert.That(setup.TryBeginCellBlock(interiorBlockCoord, false, out VoxelBlockMutation mixedSetup), Is.True);
                VoxelCell preserved = new VoxelCell
                {
                    BaseMaterialId = PathMaterial,
                    Boundary = VoxelBoundarySample.FromSignedQ4(8)
                };
                Assert.That(mixedSetup.SetCell(0, in preserved), Is.True);
                Assert.That(setup.CompletePartialBlock(ref mixedSetup, true), Is.True);
                reads.Refresh(in table, in pool);
                Assert.That(reads.TryAcquireRegionContainingBlock(interiorBlockCoord, out RegionReadView mixedBeforeView), Is.True);
                Assert.That(mixedBeforeView.TryGetWorldBlock(interiorBlockCoord, out VoxelReadBlock mixedBefore), Is.True);
                Assert.That(mixedBefore.Kind, Is.EqualTo(VoxelReadBlockKind.Mixed));

                var mixedMutations = new CountingMutationStore(
                    new RegionMutationStore(in table, in pool));
                RasterResult mixedResult = PrimitiveRasteriser.RasterisePrimitive(
                    in supportFrustum,
                    interiorMin,
                    interiorMax,
                    reads,
                    mixedMutations);
                reads.Refresh(in table, in pool);

                Assert.That(mixedMutations.WholeCellBlockCalls, Is.Zero,
                    "Mixed FillIfEmpty state must remain on the per-cell path.");
                Assert.That(mixedMutations.BeginCellBlockCalls, Is.EqualTo(1));
                Assert.That(mixedResult.VoxelsWritten, Is.EqualTo(VoxelReadGrid.VoxelsPerBlock - 1),
                    "Only the 511 empty cells in the Mixed fixture may be filled.");
                Assert.That(reads.TryAcquireRegionContainingBlock(interiorBlockCoord, out RegionReadView mixedAfterView), Is.True);
                Assert.That(mixedAfterView.TryReadCell(interiorMin, out VoxelCell preservedAfter), Is.True);
                Assert.That(preservedAfter.BaseMaterialId, Is.EqualTo(PathMaterial));
                Assert.That(preservedAfter.Boundary.IsAuthored, Is.True,
                    "FillIfEmpty must preserve pre-existing authored solid-side boundary semantics.");
                Assert.That(mixedAfterView.TryReadCell(interiorMin + new int3(1, 0, 0), out VoxelCell filledAfter), Is.True);
                Assert.That(filledAfter.BaseMaterialId, Is.EqualTo(MountainMaterial));

                Primitive edgeFrustum = CurvedPrimitiveEmitter.Frustum(
                    new int3(4, 0, 4),
                    32,
                    16,
                    16,
                    1,
                    MountainMaterial,
                    SurfaceStyles.MaterialDefault,
                    PrimitiveMode.FillIfEmpty,
                    0);
                int3 edgeMin = new(VoxelReadGrid.BlockEdge * 2, VoxelReadGrid.BlockEdge, 0);
                int3 edgeMax = edgeMin + VoxelReadGrid.BlockEdge;
                var edgeMutations = new CountingMutationStore(
                    new RegionMutationStore(in table, in pool));
                RasterResult edgeResult = PrimitiveRasteriser.RasterisePrimitive(
                    in edgeFrustum,
                    edgeMin,
                    edgeMax,
                    reads,
                    edgeMutations);
                reads.Refresh(in table, in pool);

                Assert.That(edgeMutations.WholeCellBlockCalls, Is.Zero,
                    "A FillIfEmpty frustum boundary block must not use whole-block replacement.");
                Assert.That(edgeMutations.BeginCellBlockCalls, Is.GreaterThan(0));
                Assert.That(edgeResult.VoxelsWritten, Is.GreaterThan(0));
                Assert.That(edgeResult.VoxelsWritten, Is.LessThan(VoxelReadGrid.VoxelsPerBlock));
                Assert.That(reads.TryAcquireRegionContainingBlock(new int3(2, 1, 0), out RegionReadView edgeView), Is.True);
                Assert.That(edgeView.TryReadCell(new int3(20, VoxelReadGrid.BlockEdge, 4), out VoxelCell surfaceInside), Is.True);
                Assert.That(surfaceInside.IsSolid, Is.True);
                Assert.That(surfaceInside.Boundary.IsAuthored, Is.True,
                    "FillIfEmpty must preserve the positive solid-side analytic boundary sample.");
                Assert.That(surfaceInside.Boundary.SignedQ4, Is.GreaterThan(0));
                Assert.That(edgeView.TryReadCell(new int3(21, VoxelReadGrid.BlockEdge, 4), out VoxelCell surfaceOutside), Is.True);
                Assert.That(surfaceOutside.IsSolid, Is.False);
                Assert.That(surfaceOutside.Boundary.IsAuthored, Is.True,
                    "FillIfEmpty must preserve the adjacent negative empty-side halo sample.");
                Assert.That(surfaceOutside.Boundary.SignedQ4, Is.LessThan(0));
            }
            finally
            {
                table.Dispose();
                pool.Dispose();
            }
        }

        private sealed class CountingMutationStore : IRegionMutationStore
        {
            private readonly RegionMutationStore _inner;

            public CountingMutationStore(RegionMutationStore inner)
            {
                _inner = inner;
            }

            public int WholeCellBlockCalls { get; private set; }
            public int BeginCellBlockCalls { get; private set; }

            public bool IsRegionResident(int3 regionCoord) => _inner.IsRegionResident(regionCoord);

            public bool SetWholeBlock(int3 worldBlock, byte material, bool markHardSurface) =>
                _inner.SetWholeBlock(worldBlock, material, markHardSurface);

            public bool SetWholeCellBlock(
                int3 worldBlock,
                in VoxelCell cell,
                bool markHardSurface)
            {
                WholeCellBlockCalls++;
                return _inner.SetWholeCellBlock(worldBlock, in cell, markHardSurface);
            }

            public bool TryBeginPartialBlock(
                int3 worldBlock,
                byte targetMaterial,
                bool markHardSurface,
                out VoxelBlockMutation mutation) =>
                _inner.TryBeginPartialBlock(
                    worldBlock, targetMaterial, markHardSurface, out mutation);

            public bool TryBeginCellBlock(
                int3 worldBlock,
                bool markHardSurface,
                out VoxelBlockMutation mutation)
            {
                BeginCellBlockCalls++;
                return _inner.TryBeginCellBlock(worldBlock, markHardSurface, out mutation);
            }

            public bool CompletePartialBlock(ref VoxelBlockMutation mutation, bool payloadChanged) =>
                _inner.CompletePartialBlock(ref mutation, payloadChanged);
        }
    }
}
