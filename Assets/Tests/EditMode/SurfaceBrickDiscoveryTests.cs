using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;
using VoxelEngine.Storage.Api;
using VoxelEngine.Storage.Runtime;
using VoxelEngine.Storage.Runtime.Occupancy;

namespace VoxelEngine.Tests.EditMode
{
    /// <summary>
    /// Surface discovery reads regions from inside a Burst job, which means every container a
    /// <see cref="RegionReadView"/> carries must be constructed even when the underlying storage
    /// is optional. The mip pyramid is optional — nothing in the runtime allocates it — so a
    /// mipless region used to abort the whole render pass at schedule time. These tests pin both
    /// the schedulability contract and the classification the job is actually there to produce.
    /// </summary>
    public sealed class SurfaceBrickDiscoveryTests
    {
        private const int Edge = VoxelReadGrid.BlocksPerRegionEdge;
        private const int BlockCount = Edge * Edge * Edge;

        private RegionTable _table;
        private BrickPool _pool;
        private VoxelChangeJournal _journal;

        [SetUp]
        public void SetUp()
        {
            _table = new RegionTable(4, Allocator.Persistent);
            _pool = new BrickPool(16, Allocator.Persistent);
            _journal = new VoxelChangeJournal();
        }

        [TearDown]
        public void TearDown()
        {
            _pool.Dispose();
            _table.Dispose();
        }

        private static int FlagIndex(int3 block) =>
            block.x + Edge * (block.y + Edge * block.z);

        /// <summary>Commits a region whose listed blocks are uniform solid rock, without mips.</summary>
        private Region MakeRegion(int3 regionCoord, params int3[] solidBlocks)
        {
            Region region = _table.LoadRegion(regionCoord);
            for (int i = 0; i < solidBlocks.Length; i++)
            {
                int3 b = solidBlocks[i];
                region.SetBrick(b.x, b.y, b.z, BrickRef.Uniform(7));
            }
            _table.CommitRegion(in region);
            _journal.PublishRegion(regionCoord);
            return region;
        }

        private static NativeArray<byte> RunDiscovery(in RegionReadView view)
        {
            var flags = new NativeArray<byte>(BlockCount, Allocator.TempJob,
                                              NativeArrayOptions.UninitializedMemory);
            new SurfaceBrickDiscoveryJob
            {
                Region = view,
                IsSurface = flags,
                Edge = Edge,
            }.Schedule(BlockCount, 256).Complete();
            return flags;
        }

        [Test]
        public void DiscoveryJobSchedulesForRegionWithoutMipPyramid()
        {
            MakeRegion(int3.zero, new int3(10, 10, 10));
            var source = new RegionReadSource(in _table, in _pool, _journal);

            Assert.IsTrue(source.TryAcquireRegion(int3.zero, out RegionReadView view));
            Assert.IsFalse(view.HasMips,
                "This test only means anything while the region genuinely lacks a pyramid.");

            // Regression: an unconstructed OccupancyMips container made this throw
            // InvalidOperationException at schedule time and aborted VoxelRenderPass.
            using NativeArray<byte> flags = RunDiscovery(in view);
            Assert.AreEqual(1, flags[FlagIndex(new int3(10, 10, 10))]);
        }

        [Test]
        public void DiscoveryJobSchedulesForRegionWithMipPyramid()
        {
            Region region = _table.LoadRegion(int3.zero);
            region.SetBrick(10, 10, 10, BrickRef.Uniform(7));
            region.AllocateMips(MipBuilder.MaxLevels, Allocator.Persistent);
            MipBuilder.RebuildRegion(in _pool, ref region);
            _table.CommitRegion(in region);

            var source = new RegionReadSource(in _table, in _pool, _journal);
            Assert.IsTrue(source.TryAcquireRegion(int3.zero, out RegionReadView view));
            Assert.IsTrue(view.HasMips);

            using NativeArray<byte> flags = RunDiscovery(in view);
            Assert.AreEqual(1, flags[FlagIndex(new int3(10, 10, 10))]);
        }

        [Test]
        public void MiplessViewReportsNoMipsAndRefusesPyramidSampling()
        {
            MakeRegion(int3.zero, new int3(10, 10, 10));
            var source = new RegionReadSource(in _table, in _pool, _journal);
            Assert.IsTrue(source.TryAcquireRegion(int3.zero, out RegionReadView view));

            Assert.IsFalse(view.HasMips);
            Assert.AreEqual(0, view.MipLevelCount);

            int3 solidVoxel = new int3(10, 10, 10) * VoxelReadGrid.BlockEdge;

            // The placeholder containers exist only to satisfy job safety. Level 1 and above must
            // still report "no data" rather than reading a one-element stand-in array.
            Assert.IsFalse(view.TrySample(solidVoxel, 1, out _, out _));

            // Levels that do not come from the pyramid keep working.
            Assert.IsTrue(view.TrySample(solidVoxel, 0, out bool blockOccupied, out byte blockMaterial));
            Assert.IsTrue(blockOccupied);
            Assert.AreEqual(7, blockMaterial);
            Assert.IsTrue(view.TrySample(solidVoxel, -1, out bool cellOccupied, out byte cellMaterial));
            Assert.IsTrue(cellOccupied);
            Assert.AreEqual(7, cellMaterial);
        }

        [Test]
        public void MiplessViewStillReportsHardSurfaceSemantics()
        {
            // A mipless view fills its pyramid fields by aliasing containers it already holds.
            // Those containers keep their own meaning, so the reads that own them must be intact.
            int3 block = new(10, 10, 10);
            Region region = _table.LoadRegion(int3.zero);
            region.SetBrick(block.x, block.y, block.z, BrickRef.Uniform(7));
            Assert.IsTrue(region.MarkHardSurfaceBrick(Region.BrickIndex(block.x, block.y, block.z)));
            _table.CommitRegion(in region);

            var source = new RegionReadSource(in _table, in _pool, _journal);
            Assert.IsTrue(source.TryAcquireRegion(int3.zero, out RegionReadView view));

            Assert.IsFalse(view.HasMips);
            Assert.IsTrue(view.IsHardSurfaceBlock(block));
            Assert.IsFalse(view.IsHardSurfaceBlock(new int3(11, 10, 10)));
        }

        [Test]
        public void IsolatedSolidBlockIsSurfaceAndEmptyNeighboursAreNot()
        {
            MakeRegion(int3.zero, new int3(10, 10, 10));
            var source = new RegionReadSource(in _table, in _pool, _journal);
            Assert.IsTrue(source.TryAcquireRegion(int3.zero, out RegionReadView view));

            using NativeArray<byte> flags = RunDiscovery(in view);
            Assert.AreEqual(1, flags[FlagIndex(new int3(10, 10, 10))],
                            "A solid block with empty neighbours is a surface block.");
            Assert.AreEqual(0, flags[FlagIndex(new int3(11, 10, 10))],
                            "An empty block is never a surface block.");
            Assert.AreEqual(0, flags[FlagIndex(new int3(30, 30, 30))]);
        }

        [Test]
        public void FullyEnclosedSolidBlockIsNotSurface()
        {
            int3 centre = new(20, 20, 20);
            var solid = new System.Collections.Generic.List<int3>();
            for (int z = -1; z <= 1; z++)
            for (int y = -1; y <= 1; y++)
            for (int x = -1; x <= 1; x++)
                solid.Add(centre + new int3(x, y, z));

            MakeRegion(int3.zero, solid.ToArray());
            var source = new RegionReadSource(in _table, in _pool, _journal);
            Assert.IsTrue(source.TryAcquireRegion(int3.zero, out RegionReadView view));

            using NativeArray<byte> flags = RunDiscovery(in view);
            Assert.AreEqual(0, flags[FlagIndex(centre)],
                            "A block whose six neighbours are fully solid is interior.");
            Assert.AreEqual(1, flags[FlagIndex(centre + new int3(1, 0, 0))],
                            "The shell of the cluster is still exposed.");
        }

        [Test]
        public void BlocksOnTheRegionBoundaryAreAlwaysSurface()
        {
            // Neighbour data lives in another region the job cannot see, so an edge block is
            // conservatively a surface block rather than a silently missing one.
            MakeRegion(int3.zero, new int3(0, 5, 5), new int3(Edge - 1, 5, 5));
            var source = new RegionReadSource(in _table, in _pool, _journal);
            Assert.IsTrue(source.TryAcquireRegion(int3.zero, out RegionReadView view));

            using NativeArray<byte> flags = RunDiscovery(in view);
            Assert.AreEqual(1, flags[FlagIndex(new int3(0, 5, 5))]);
            Assert.AreEqual(1, flags[FlagIndex(new int3(Edge - 1, 5, 5))]);
        }

        [Test]
        public void SchedulerPrepareDiscoversSurfaceBricksWithoutMips()
        {
            // The reported failure was raised from VoxelSurfaceScheduler.Prepare inside render
            // graph recording, so exercise that entry point and not just the job.
            int3 regionCoord = new(40, 0, 40);   // ~2 km out: past every ring, so no chunk builds.
            MakeRegion(regionCoord, new int3(10, 10, 10));
            var source = new RegionReadSource(in _table, in _pool, _journal);

            var cameraObject = new GameObject("SurfaceDiscoveryTestCamera");
            var camera = cameraObject.AddComponent<Camera>();
            var scheduler = new VoxelSurfaceScheduler();
            try
            {
                MaterialPaletteView palette = default;
                SurfaceCatalogueView surfaceCatalogue = default;
                CoatingCatalogueView coatingCatalogue = default;
                scheduler.Prepare(source, in palette, in surfaceCatalogue, in coatingCatalogue,
                                  null, _journal, camera, 0.1f, 1);

                Assert.Greater(scheduler.Metrics.DiscoveredSurfaceBricks, 0,
                               "A published region with solid content must yield surface bricks.");
            }
            finally
            {
                scheduler.Dispose();
                Object.DestroyImmediate(cameraObject);
            }
        }
    }
}
