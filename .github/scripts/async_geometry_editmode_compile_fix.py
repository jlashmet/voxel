from pathlib import Path

# Geometry architecture tests use Application.dataPath in several source guards.
p = Path('Assets/Tests/EditMode/GeometryPipelineArchitectureTests.cs')
s = p.read_text()
if 'using UnityEngine;\n' not in s:
    s = s.replace('using NUnit.Framework;\n', 'using NUnit.Framework;\nusing UnityEngine;\n', 1)
p.write_text(s)

# Surface discovery no longer borrows RegionReadView inside Burst. Tests must exercise the
# immutable occupancy-summary snapshot boundary that the production scheduler uses.
p = Path('Assets/Tests/EditMode/SurfaceBrickDiscoveryTests.cs')
s = p.read_text()
s = s.replace(
'''    /// <summary>
    /// Surface discovery reads regions from inside a Burst job, which means every container a
    /// <see cref="RegionReadView"/> carries must be constructed even when the underlying storage
    /// is optional. The mip pyramid is optional — nothing in the runtime allocates it — so a
    /// mipless region used to abort the whole render pass at schedule time. These tests pin both
    /// the schedulability contract and the classification the job is actually there to produce.
    /// </summary>''',
'''    /// <summary>
    /// Surface discovery runs on an immutable caller-owned occupancy summary. The Burst job never
    /// borrows RegionReadView/BrickPool memory, so later Storage mutation or eviction cannot race
    /// it. These tests cover both mipless/mipped Storage sources and the async scheduler boundary.
    /// </summary>''', 1)
old = '''        private static NativeArray<byte> RunDiscovery(in RegionReadView view)
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
        }'''
new = '''        private static NativeArray<byte> RunDiscovery(IRegionReadSource source, int3 regionCoord)
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
        }'''
if s.count(old) != 1:
    raise SystemExit(f'RunDiscovery legacy helper expected once, found {s.count(old)}')
s = s.replace(old, new, 1)
s = s.replace('RunDiscovery(in view)', 'RunDiscovery(source, int3.zero)')
s = s.replace(
'''            // Regression: an unconstructed OccupancyMips container made this throw
            // InvalidOperationException at schedule time and aborted VoxelRenderPass.
            using NativeArray<byte> flags = RunDiscovery(source, int3.zero);''',
'''            // Regression: discovery must be schedulable without depending on optional mip
            // containers because the job receives only the copied block summary.
            using NativeArray<byte> flags = RunDiscovery(source, int3.zero);''', 1)

old_scheduler = '''                scheduler.Prepare(source, in palette, in surfaceCatalogue, in coatingCatalogue,
                                  null, _journal, camera, 0.1f, 1);

                Assert.Greater(scheduler.Metrics.DiscoveredSurfaceBricks, 0,
                               "A published region with solid content must yield surface bricks.");'''
new_scheduler = '''                bool discovered = false;
                for (int frame = 1; frame <= 256 && !discovered; frame++)
                {
                    scheduler.Prepare(source, in palette, in surfaceCatalogue, in coatingCatalogue,
                                      null, _journal, camera, 0.1f, frame);
                    discovered = scheduler.Metrics.DiscoveredSurfaceBricks > 0;
                    if (!discovered) System.Threading.Thread.Yield();
                }

                Assert.True(discovered,
                    "Async discovery must eventually publish surface bricks without waiting "
                  + "on an unfinished Burst job in Prepare.");'''
if s.count(old_scheduler) != 1:
    raise SystemExit(f'scheduler one-frame assertion expected once, found {s.count(old_scheduler)}')
s = s.replace(old_scheduler, new_scheduler, 1)
p.write_text(s)

# Static compile-boundary guards.
assert 'using UnityEngine;' in Path('Assets/Tests/EditMode/GeometryPipelineArchitectureTests.cs').read_text()
surface = p.read_text()
assert 'Region = view' not in surface
assert 'OccupiedWords = occupied' in surface
assert 'FullySolidWords = fullySolid' in surface
assert 'RunDiscovery(in view)' not in surface
assert 'for (int frame = 1; frame <= 256' in surface
