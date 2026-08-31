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
    public sealed class PrimitiveRasteriserWholeBlockFastPathTests
    {
        private const byte SolidMaterial = 7;

        [Test]
        public void BoxCarve_FullyCoveredUniformBlock_UsesOneWholeCellReplacement()
        {
            var table = new RegionTable(1, Allocator.Temp);
            var pool = new BrickPool(2, Allocator.Temp);
            try
            {
                table.LoadRegion(int3.zero);
                var setup = new RegionMutationStore(in table, in pool);
                Assert.That(setup.SetWholeBlock(int3.zero, SolidMaterial, false), Is.True);

                var reads = new RegionReadSource(in table, in pool);
                var mutations = new CountingMutationStore(new RegionMutationStore(in table, in pool));
                Primitive carve = BoxEmitter.Box(
                    int3.zero,
                    new int3(VoxelReadGrid.BlockEdge),
                    VoxelGrid.MaterialEmpty,
                    PrimitiveMode.Carve,
                    0);

                RasterResult result = PrimitiveRasteriser.RasterisePrimitive(
                    in carve,
                    int3.zero,
                    new int3(VoxelReadGrid.BlockEdge),
                    reads,
                    mutations);

                Assert.That(mutations.WholeCellBlockCalls, Is.EqualTo(1));
                Assert.That(mutations.BeginCellBlockCalls, Is.Zero);
                Assert.That(result.VoxelsWritten, Is.EqualTo(VoxelReadGrid.VoxelsPerBlock));
                reads.Refresh(in table, in pool);
                Assert.That(reads.TryAcquireRegionContainingBlock(int3.zero, out RegionReadView view), Is.True);
                Assert.That(view.TryGetWorldBlock(int3.zero, out VoxelReadBlock block), Is.True);
                Assert.That(block.Kind, Is.EqualTo(VoxelReadBlockKind.Empty));
            }
            finally
            {
                table.Dispose();
                pool.Dispose();
            }
        }

        [Test]
        public void FrustumFill_FullyContainedEmptyBlock_UsesOneWholeCellReplacement()
        {
            var table = new RegionTable(1, Allocator.Temp);
            var pool = new BrickPool(2, Allocator.Temp);
            try
            {
                table.LoadRegion(int3.zero);
                var reads = new RegionReadSource(in table, in pool);
                var mutations = new CountingMutationStore(new RegionMutationStore(in table, in pool));
                int halfEdge = VoxelReadGrid.BlockEdge / 2;
                Primitive frustum = CurvedPrimitiveEmitter.Frustum(
                    new int3(halfEdge, 0, halfEdge),
                    VoxelReadGrid.BlockEdge,
                    VoxelReadGrid.BlockEdge,
                    VoxelReadGrid.BlockEdge,
                    1,
                    SolidMaterial,
                    SurfaceStyles.MaterialDefault,
                    PrimitiveMode.Fill,
                    0);

                RasterResult result = PrimitiveRasteriser.RasterisePrimitive(
                    in frustum,
                    int3.zero,
                    new int3(VoxelReadGrid.BlockEdge),
                    reads,
                    mutations);

                Assert.That(mutations.WholeCellBlockCalls, Is.EqualTo(1),
                    "A fully contained canonical-empty block should take the generic frustum whole-block path.");
                Assert.That(mutations.BeginCellBlockCalls, Is.Zero);
                Assert.That(result.VoxelsWritten, Is.EqualTo(VoxelReadGrid.VoxelsPerBlock));
                reads.Refresh(in table, in pool);
                Assert.That(reads.TryAcquireRegionContainingBlock(int3.zero, out RegionReadView view), Is.True);
                Assert.That(view.TryGetWorldBlock(int3.zero, out VoxelReadBlock block), Is.True);
                Assert.That(block.Kind, Is.EqualTo(VoxelReadBlockKind.Uniform));
                Assert.That(block.UniformMaterial, Is.EqualTo(SolidMaterial));
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

            public CountingMutationStore(RegionMutationStore inner) => _inner = inner;

            public int WholeCellBlockCalls { get; private set; }
            public int BeginCellBlockCalls { get; private set; }

            public bool IsRegionResident(int3 regionCoord) => _inner.IsRegionResident(regionCoord);

            public bool SetWholeBlock(int3 worldBlock, byte material, bool markHardSurface) =>
                _inner.SetWholeBlock(worldBlock, material, markHardSurface);

            public bool SetWholeCellBlock(int3 worldBlock, in VoxelCell cell, bool markHardSurface)
            {
                WholeCellBlockCalls++;
                return _inner.SetWholeCellBlock(worldBlock, in cell, markHardSurface);
            }

            public bool TryBeginPartialBlock(
                int3 worldBlock,
                byte targetMaterial,
                bool markHardSurface,
                out VoxelBlockMutation mutation) =>
                _inner.TryBeginPartialBlock(worldBlock, targetMaterial, markHardSurface, out mutation);

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
