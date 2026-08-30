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
                Assert.That(edgeView.TryReadCell(new int3(0, VoxelReadGrid.BlockEdge, 4), out VoxelCell inside), Is.True);
                Assert.That(inside.BaseMaterialId, Is.EqualTo(MountainMaterial));
                Assert.That(edgeView.TryReadCell(new int3(7, VoxelReadGrid.BlockEdge, 4), out VoxelCell outside), Is.True);
                Assert.That(outside.IsSolid, Is.False,
                    "The far side of a boundary block must remain empty after frustum rasterization.");
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
