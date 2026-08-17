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
    /// Surface discovery runs on an immutable caller-owned occupancy summary. The Burst job never
    /// borrows RegionReadView/BrickPool memory, so later Storage mutation or eviction cannot race
    /// it. These tests cover both mipless/mipped Storage sources and the async scheduler boundary.
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
                region.SetBlockOccupancySummary(
                    Region.BrickIndex(b.x, b.y, b.z), occupied: true, fullySolid: true);
            }
            _table.CommitRegion(in region);
            _journal.PublishRegion(regionCoord);
            return region;
        }

        private static NativeArray<byte> RunDiscovery(IRegionReadSource source, int3 regionCoord)
        {
            using var occupied = new NativeArray<ulong>(
                VoxelReadGrid.BlockSummaryWordCount, Allocator.TempJob,
                NativeArrayOptions.UninitializedMemory);
            using var fullySolid = new NativeArray<ulong>(
                VoxelReadGrid.BlockSummaryWordCount, Allocator.TempJob,
                NativeArrayOptions.UninitializedMemory);
            Assert.True(source.TryCopyBlockSummary(regionCoord, occupied, fullySolid, out _));

            var flags = new NativeArray<byte>(BlockCount, Allocator.TempJob,
                                              NativeArrayOptions.UninitializedMemory);
            new SurfaceBrickDiscoveryJob
            {
                OccupiedWords = occupied,
                FullySolidWords = fullySolid,
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

            // Regression: discovery must be schedulable without depending on optional mip
            // containers because the job receives only the copied block summary.
            using NativeArray<byte> flags = RunDiscovery(source, int3.zero);
            Assert.AreEqual(1, flags[FlagIndex(new int3(10, 10, 10))]);
        }

        [Test]
        public void DiscoveryJobSchedulesForRegionWithMipPyramid()
        {
            Region region = _table.LoadRegion(int3.zero);
            int3 solidBlock = new(10, 10, 10);
            region.SetBrick(solidBlock.x, solidBlock.y, solidBlock.z, BrickRef.Uniform(7));
            region.SetBlockOccupancySummary(
                Region.BrickIndex(solidBlock.x, solidBlock.y, solidBlock.z),
                occupied: true, fullySolid: true);
            region.AllocateMips(MipBuilder.MaxLevels, Allocator.Persistent);
            MipBuilder.RebuildRegion(in _pool, ref region);
            _table.CommitRegion(in region);

            var source = new RegionReadSource(in _table, in _pool, _journal);
            Assert.IsTrue(source.TryAcquireRegion(int3.zero, out RegionReadView view));
            Assert.IsTrue(view.HasMips);

            using NativeArray<byte> flags = RunDiscovery(source, int3.zero);
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

            using NativeArray<byte> flags = RunDiscovery(source, int3.zero);
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

            using NativeArray<byte> flags = RunDiscovery(source, int3.zero);
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

            using NativeArray<byte> flags = RunDiscovery(source, int3.zero);
            Assert.AreEqual(1, flags[FlagIndex(new int3(0, 5, 5))]);
            Assert.AreEqual(1, flags[FlagIndex(new int3(Edge - 1, 5, 5))]);
        }

        [Test]
        public void ClipmapMotionReadmitsAlreadyResidentSurfaceIntoFinerLod()
        {
            // Region 5 starts at 256 m. It is initially in the step-4 band but outside step-1.
            // After moving the camera to x=200 m the same unchanged surface is ~57 m away and
            // belongs to step-1. No second journal publication is allowed: clipmap admission must
            // request compact discovery for the newly exposed region itself.
            int3 regionCoord = new(5, 0, 0);
            MakeRegion(regionCoord, new int3(1, 10, 10));
            var source = new RegionReadSource(in _table, in _pool, _journal);

            var cameraObject = new GameObject("ClipmapReadmissionCamera");
            var camera = cameraObject.AddComponent<Camera>();
            var scheduler = new VoxelSurfaceScheduler();
            try
            {
                MaterialPaletteView palette = default;
                SurfaceCatalogueView surfaceCatalogue = default;
                CoatingCatalogueView coatingCatalogue = default;
                camera.transform.position = Vector3.zero;

                bool discoveredByOuterLod = false;
                int frame = 1;
                var initialDiscoveryClock = System.Diagnostics.Stopwatch.StartNew();
                while (!discoveredByOuterLod && initialDiscoveryClock.ElapsedMilliseconds < 2000)
                {
                    scheduler.Prepare(source, in palette, in surfaceCatalogue,
                                      in coatingCatalogue, null, _journal,
                                      camera, 0.1f, frame++);
                    discoveredByOuterLod = scheduler.KnownChunkCountForSourceStep(4) > 0;
                    if (!discoveredByOuterLod) System.Threading.Thread.Sleep(1);
                }

                Assert.True(discoveredByOuterLod,
                    $"Initial discovery never reached the step-4 ring within {initialDiscoveryClock.ElapsedMilliseconds} ms, so the re-admission setup is invalid.");
                Assert.AreEqual(0, scheduler.KnownChunkCountForSourceStep(1),
                    "The target surface must begin outside the fine-ring clipmap.");

                camera.transform.position = new Vector3(200f, 0f, 0f);
                bool admittedToFineLod = false;
                var fineAdmissionClock = System.Diagnostics.Stopwatch.StartNew();
                while (!admittedToFineLod && fineAdmissionClock.ElapsedMilliseconds < 2000)
                {
                    scheduler.Prepare(source, in palette, in surfaceCatalogue,
                                      in coatingCatalogue, null, _journal,
                                      camera, 0.1f, frame++);
                    admittedToFineLod = scheduler.KnownChunkCountForSourceStep(1) > 0;
                    if (!admittedToFineLod) System.Threading.Thread.Sleep(1);
                }

                Assert.True(admittedToFineLod,
                    $"Camera motion entered an already-resident surface region but the fine LOD "
                  + $"never re-ran surface discovery within {fineAdmissionClock.ElapsedMilliseconds} ms. This would create an LOD handoff hole.");
            }
            finally
            {
                scheduler.Dispose();
                Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void RepeatedSurfaceDiscoveryDoesNotReinvalidateKnownChunk()
        {
            using var cache = new CpuTransvoxelChunkCache(sourceStep: 4);
            cache.SetClipmapWindow(int3.zero, radius: 1);

            // Interior block: maps to exactly one chunk and does not exercise halo neighbours.
            int3 brick = new(1, 1, 1);
            Assert.AreEqual(1, cache.DiscoverSurfaceBricks(new[] { brick }),
                "The first immutable summary publication must admit the chunk.");
            Assert.AreEqual(1, cache.KnownCount);
            Assert.AreEqual(0, cache.DirtyCount,
                "Discovery must admit render ownership without creating geometry work.");

            Assert.AreEqual(0, cache.DiscoverSurfaceBricks(new[] { brick }),
                "Later publication slices for the same unchanged region must not create a new "
              + "source generation for an already-known chunk.");
            Assert.AreEqual(1, cache.KnownCount);
            Assert.AreEqual(0, cache.DirtyCount);

            // A real edit invalidates the generation proof but remains cold until visible
            // coverage explicitly requests it. This is the demand-driven T2 contract.
            cache.InvalidateSurfaceBricks(new[] { brick });
            Assert.AreEqual(1, cache.KnownCount);
            Assert.AreEqual(0, cache.DirtyCount);
            Assert.True(cache.RequestHierarchyCoverage(
                int3.zero, SurfaceBuildPriority.VisibleRefinement));
            Assert.AreEqual(1, cache.DirtyCount,
                "Explicit coverage demand must enqueue the invalidated generation.");
        }

        [Test]
        public void SurfaceDiscoveryOutsideClipmapDoesNotCreateDirtyBuildWork()
        {
            using var cache = new CpuTransvoxelChunkCache(sourceStep: 4);
            cache.SetClipmapWindow(int3.zero, radius: 1);

            // A step-4 chunk spans 32 Storage blocks per axis. This block maps to chunk +10,
            // well outside the [-1,+1] clipmap window. Discovery may observe it in the broader
            // resident Storage stream, but rejected render residency must not leak into _dirty.
            cache.InvalidateSurfaceBricks(new[] { new int3(10 * cache.BricksPerAxis, 0, 0) });

            Assert.AreEqual(0, cache.KnownCount,
                "Out-of-window discovery must not acquire render residency.");
            Assert.AreEqual(0, cache.DirtyCount,
                "Out-of-window discovery must not enqueue build work for an unowned chunk.");
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
                bool discovered = false;
                int frame = 1;
                var discoveryClock = System.Diagnostics.Stopwatch.StartNew();
                while (!discovered && discoveryClock.ElapsedMilliseconds < 2000)
                {
                    scheduler.Prepare(source, in palette, in surfaceCatalogue, in coatingCatalogue,
                                      null, _journal, camera, 0.1f, frame++);
                    discovered = scheduler.Metrics.DiscoveredSurfaceBricks > 0;
                    if (!discovered) System.Threading.Thread.Sleep(1);
                }

                Assert.True(discovered,
                    $"Async discovery must publish surface bricks within {discoveryClock.ElapsedMilliseconds} ms "
                  + "without waiting on an unfinished Burst job in Prepare.");
            }
            finally
            {
                scheduler.Dispose();
                Object.DestroyImmediate(cameraObject);
            }
        }
    }
}
