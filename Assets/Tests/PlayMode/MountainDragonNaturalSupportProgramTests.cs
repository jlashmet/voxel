using System.Reflection;
using Game.WorldBuilder.Voxel;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Showcase;
using VoxelEngine.Storage.Api;
using VoxelEngine.Storage.Runtime;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;
using VoxelEngine.Structures.Runtime.Emitters;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class MountainDragonNaturalSupportProgramTests
    {
        private const uint Seed = 0x5EED1234;
        private const byte MountainMaterial = 1;
        private const byte PathMaterial = 13;
        private const byte DragonMaterial = 9;
        private const int ProductionMotorWidthVoxels = 6;
        private const int RequiredLateralMarginVoxels = 5;
        private const long MaximumTraversalCarveVoxels = 2_800_000;

        [Test]
        public void MountainPathSupportUsesTaperedMassesWithoutTallRetainingWallBoxes()
        {
            MountainLandmarkSpec spec = ShowcaseMountainDragonLayout.CreateLandmark(Seed);
            FeatureCatalogue catalogue = WorldBuilderMountainLandmarkCatalogue.Build(
                in spec,
                MountainMaterial,
                PathMaterial,
                DragonMaterial,
                Allocator.Temp);

            try
            {
                FeatureDefinition landform = catalogue.Definitions[0];
                int pc = landform.ProgramOffset;
                int end = pc + landform.ProgramLength;
                int frustumCount = 0;
                int tallGroundSupportBoxes = 0;
                int carveBoxCount = 0;
                long carveVoxelVolume = 0;
                int expectedClearanceWidth = System.Math.Min(
                    spec.PathWidth,
                    WorldBuilderMountainLandmarkCatalogue.PathClearanceWidthVoxels);
                int expectedCarveBoxCount = 2;
                for (int level = 0; level < spec.SwitchbackCount; level++)
                    expectedCarveBoxCount += spec.PathTier(level).SegmentCount;

                Assert.That(
                    WorldBuilderMountainLandmarkCatalogue.PathClearanceWidthVoxels,
                    Is.GreaterThanOrEqualTo(
                        ProductionMotorWidthVoxels + RequiredLateralMarginVoxels * 2),
                    "The centered traversal lane must preserve at least 0.5 m lateral clearance "
                    + "on both sides of the 0.6 m production motor.");

                while (pc < end)
                {
                    ShapeOp op = (ShapeOp)catalogue.Program[pc];
                    if (op == ShapeOp.End) break;

                    int instructionLength = ShapeOps.InstructionLength(op);
                    Assert.That(instructionLength, Is.GreaterThan(0),
                        "Mountain landform program contains an unknown shape opcode.");
                    Assert.That(pc + instructionLength, Is.LessThanOrEqualTo(end),
                        "Mountain landform program contains a truncated shape instruction.");

                    if (op == ShapeOp.EmitFrustum)
                        frustumCount++;
                    else if (op == ShapeOp.EmitBox)
                    {
                        int y = catalogue.Program[pc + 3];
                        int sizeX = catalogue.Program[pc + 5];
                        int sizeY = catalogue.Program[pc + 6];
                        int sizeZ = catalogue.Program[pc + 7];
                        int material = catalogue.Program[pc + 8];
                        PrimitiveMode mode = (PrimitiveMode)catalogue.Program[pc + 11];
                        if (y == 0 && sizeY > 1 && material == MountainMaterial)
                            tallGroundSupportBoxes++;

                        if (mode == PrimitiveMode.Carve)
                        {
                            carveBoxCount++;
                            carveVoxelVolume += (long)sizeX * sizeY * sizeZ;
                            int horizontalNarrow = sizeX < sizeZ ? sizeX : sizeZ;
                            Assert.That(horizontalNarrow, Is.EqualTo(expectedClearanceWidth),
                                "Headroom carving must stay on the centered traversal lane instead "
                                + "of rasterizing empty space across the full decorative path width.");
                        }
                    }

                    pc += instructionLength;
                }

                Assert.That(tallGroundSupportBoxes, Is.Zero,
                    "Switchback support must not regress to tall ground-to-path rectangular retaining walls.");
                Assert.That(frustumCount, Is.GreaterThanOrEqualTo(20),
                    "The path and silhouette must be supported by multiple tapered landform masses.");
                Assert.That(carveBoxCount, Is.EqualTo(expectedCarveBoxCount),
                    "Every authored shell-following ramp segment, final ascent, and summit approach "
                    + "must retain explicit headroom carving.");
                Assert.That(carveVoxelVolume, Is.LessThanOrEqualTo(MaximumTraversalCarveVoxels),
                    "Traversal clearance must remain below the measured one-time bake-cost envelope; "
                    + "full-width carving previously rasterized more than five million voxels.");
                Assert.That(landform.MaxPrimitives, Is.LessThanOrEqualTo(80),
                    "Naturalized Mountain Dragon support must stay within the feature's measured primitive envelope.");
                Assert.That(landform.MaxPrimitives, Is.LessThanOrEqualTo(FeatureBudget.MaxPrimitivesPerInstance),
                    "Mountain Dragon realization must remain inside the shared per-instance primitive budget.");
            }
            finally
            {
                catalogue.Dispose();
            }
        }

        [Test]
        public void OfflineBakeFarFieldSuppressionIsScopedAndRestored()
        {
            var store = new FarFieldStructureStore();
            FieldInfo captureEnabled = typeof(FarFieldStructureStore).GetField(
                "_captureEnabled",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(captureEnabled, Is.Not.Null,
                "The far-field store must retain an instance-scoped capture gate for offline baking.");
            Assert.That(captureEnabled.GetValue(store), Is.True,
                "Runtime far-field capture must be enabled by default.");

            using (store.SuppressCapture())
            {
                Assert.That(captureEnabled.GetValue(store), Is.False,
                    "Offline bake scope must suppress non-serialized far-field scans.");

                using (store.SuppressCapture())
                    Assert.That(captureEnabled.GetValue(store), Is.False,
                        "Nested suppression must remain disabled until the outer scope exits.");

                Assert.That(captureEnabled.GetValue(store), Is.False,
                    "Disposing a nested scope must not re-enable an outer suppression scope.");
            }

            Assert.That(captureEnabled.GetValue(store), Is.True,
                "Capture must be restored after offline generation so LoadBake/runtime rebuilding works.");
        }

        [Test]
        public void BoxCarveSkipsEmptyAtomicallyClearsUniformAndPreservesMixedAndPartialAccounting()
        {
            var table = new RegionTable(1, Allocator.Temp);
            var pool = new BrickPool(4, Allocator.Temp);

            try
            {
                table.LoadRegion(int3.zero);
                var reads = new RegionReadSource(in table, in pool);
                var mutations = new RegionMutationStore(in table, in pool);
                Primitive fullCarve = BoxEmitter.Box(
                    int3.zero,
                    new int3(VoxelReadGrid.BlockEdge, VoxelReadGrid.BlockEdge, VoxelReadGrid.BlockEdge),
                    VoxelGrid.MaterialEmpty,
                    PrimitiveMode.Carve,
                    0);

                var emptyMutations = new CountingMutationStore(
                    new RegionMutationStore(in table, in pool));
                RasterResult emptyResult = PrimitiveRasteriser.RasterisePrimitive(
                    in fullCarve,
                    int3.zero,
                    new int3(VoxelReadGrid.BlockEdge, VoxelReadGrid.BlockEdge, VoxelReadGrid.BlockEdge),
                    reads,
                    emptyMutations);

                Assert.That(emptyResult.VoxelsWritten, Is.Zero,
                    "Carving an explicitly Empty storage block must remain an authoritative no-op.");
                Assert.That(emptyMutations.WholeCellBlockCalls, Is.Zero,
                    "Canonical-empty skip must happen before the whole-block mutation path.");
                Assert.That(emptyMutations.BeginCellBlockCalls, Is.Zero,
                    "Canonical-empty skip must not open a partial mutation.");
                Assert.That(pool.AllocatedCount, Is.Zero,
                    "The boxed-carve fast path must not materialize canonical empty storage.");

                Assert.That(mutations.SetWholeBlock(int3.zero, MountainMaterial, false), Is.True,
                    "Uniform-solid setup must replace the canonical empty block.");
                reads.Refresh(in table, in pool);
                Assert.That(reads.TryAcquireRegionContainingBlock(int3.zero, out RegionReadView uniformView), Is.True);
                Assert.That(uniformView.TryGetWorldBlock(int3.zero, out VoxelReadBlock uniformBlock), Is.True);
                Assert.That(uniformBlock.Kind, Is.EqualTo(VoxelReadBlockKind.Uniform));
                Assert.That(uniformBlock.UniformMaterial, Is.EqualTo(MountainMaterial));

                var uniformMutations = new CountingMutationStore(
                    new RegionMutationStore(in table, in pool));
                RasterResult uniformResult = PrimitiveRasteriser.RasterisePrimitive(
                    in fullCarve,
                    int3.zero,
                    new int3(VoxelReadGrid.BlockEdge, VoxelReadGrid.BlockEdge, VoxelReadGrid.BlockEdge),
                    reads,
                    uniformMutations);
                reads.Refresh(in table, in pool);

                Assert.That(uniformMutations.WholeCellBlockCalls, Is.EqualTo(1),
                    "A fully covered Uniform solid box-carve block must use one whole-cell replacement.");
                Assert.That(uniformMutations.BeginCellBlockCalls, Is.Zero,
                    "A fully covered Uniform block must not regress to the 512-cell partial mutation loop.");
                Assert.That(uniformResult.VoxelsWritten, Is.EqualTo(VoxelReadGrid.VoxelsPerBlock),
                    "Atomic Uniform carve must preserve exact 512-cell write accounting.");
                Assert.That(reads.TryAcquireRegionContainingBlock(int3.zero, out RegionReadView uniformClearedView), Is.True);
                Assert.That(uniformClearedView.TryGetWorldBlock(int3.zero, out VoxelReadBlock uniformClearedBlock), Is.True);
                Assert.That(uniformClearedBlock.Kind, Is.EqualTo(VoxelReadBlockKind.Empty));
                Assert.That(pool.AllocatedCount, Is.Zero,
                    "Atomic Uniform clearing must not leave mixed-brick allocation behind.");

                VoxelCell emptyWithBoundary = new VoxelCell
                {
                    Boundary = VoxelBoundarySample.FromSignedQ4(-8)
                };
                mutations.Refresh(in table, in pool);
                Assert.That(mutations.SetWholeCellBlock(int3.zero, in emptyWithBoundary, false), Is.True,
                    "Mixed setup must create authored empty-side boundary state.");
                reads.Refresh(in table, in pool);
                Assert.That(reads.TryAcquireRegionContainingBlock(int3.zero, out RegionReadView mixedView), Is.True);
                Assert.That(mixedView.TryGetWorldBlock(int3.zero, out VoxelReadBlock mixedBlock), Is.True);
                Assert.That(mixedBlock.Kind, Is.EqualTo(VoxelReadBlockKind.Mixed));

                var mixedMutations = new CountingMutationStore(
                    new RegionMutationStore(in table, in pool));
                RasterResult mixedResult = PrimitiveRasteriser.RasterisePrimitive(
                    in fullCarve,
                    int3.zero,
                    new int3(VoxelReadGrid.BlockEdge, VoxelReadGrid.BlockEdge, VoxelReadGrid.BlockEdge),
                    reads,
                    mixedMutations);
                reads.Refresh(in table, in pool);

                Assert.That(mixedMutations.WholeCellBlockCalls, Is.Zero,
                    "Mixed state must stay on the cell path so sparse write accounting remains exact.");
                Assert.That(mixedMutations.BeginCellBlockCalls, Is.EqualTo(1),
                    "Mixed boundary payload must be cleared through the existing partial-cell path.");
                Assert.That(mixedResult.VoxelsWritten, Is.EqualTo(VoxelReadGrid.VoxelsPerBlock),
                    "This all-boundary Mixed fixture changes every cell and must still report 512 writes.");
                Assert.That(reads.TryAcquireRegionContainingBlock(int3.zero, out RegionReadView mixedClearedView), Is.True);
                Assert.That(mixedClearedView.TryGetWorldBlock(int3.zero, out VoxelReadBlock mixedClearedBlock), Is.True);
                Assert.That(mixedClearedBlock.Kind, Is.EqualTo(VoxelReadBlockKind.Empty),
                    "Mixed boundary clearing must collapse back to canonical Empty.");
                Assert.That(pool.AllocatedCount, Is.Zero);

                VoxelCell solidWithBoundary = new VoxelCell
                {
                    BaseMaterialId = MountainMaterial,
                    Boundary = VoxelBoundarySample.FromSignedQ4(12)
                };
                mutations.Refresh(in table, in pool);
                Assert.That(mutations.SetWholeCellBlock(int3.zero, in solidWithBoundary, false), Is.True,
                    "Partial-edge setup must restore a semantic solid block.");
                reads.Refresh(in table, in pool);

                int partialWidth = VoxelReadGrid.BlockEdge / 2;
                Primitive partialCarve = BoxEmitter.Box(
                    int3.zero,
                    new int3(partialWidth, VoxelReadGrid.BlockEdge, VoxelReadGrid.BlockEdge),
                    VoxelGrid.MaterialEmpty,
                    PrimitiveMode.Carve,
                    0);
                var partialMutations = new CountingMutationStore(
                    new RegionMutationStore(in table, in pool));
                RasterResult partialResult = PrimitiveRasteriser.RasterisePrimitive(
                    in partialCarve,
                    int3.zero,
                    new int3(VoxelReadGrid.BlockEdge, VoxelReadGrid.BlockEdge, VoxelReadGrid.BlockEdge),
                    reads,
                    partialMutations);
                reads.Refresh(in table, in pool);

                Assert.That(partialMutations.WholeCellBlockCalls, Is.Zero,
                    "A clipped box must not clear cells outside its authored bounds through whole-block replacement.");
                Assert.That(partialMutations.BeginCellBlockCalls, Is.EqualTo(1),
                    "A clipped box must retain the existing partial-cell mutation path.");
                Assert.That(partialResult.VoxelsWritten,
                    Is.EqualTo(partialWidth * VoxelReadGrid.BlockEdge * VoxelReadGrid.BlockEdge),
                    "Partial carve must report only cells inside its half-block geometry.");
                Assert.That(reads.TryAcquireRegionContainingBlock(int3.zero, out RegionReadView partialView), Is.True);
                Assert.That(partialView.TryReadCell(new int3(0, 0, 0), out VoxelCell carvedCell), Is.True);
                Assert.That(carvedCell.IsSolid, Is.False);
                Assert.That(carvedCell.Boundary.IsAuthored, Is.False,
                    "Cells inside the partial carve must be reset to the exact default cell.");
                Assert.That(partialView.TryReadCell(
                    new int3(VoxelReadGrid.BlockEdge - 1, 0, 0), out VoxelCell preservedCell), Is.True);
                Assert.That(preservedCell.BaseMaterialId, Is.EqualTo(MountainMaterial),
                    "Cells outside the clipped carve must preserve their authored material.");
                Assert.That(preservedCell.Boundary.IsAuthored, Is.True,
                    "Cells outside the clipped carve must preserve authored boundary semantics.");
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
