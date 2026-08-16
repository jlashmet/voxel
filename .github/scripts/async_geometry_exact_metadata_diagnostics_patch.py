from pathlib import Path

cache_path = Path('Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/CpuTransvoxelChunkCache.cs')
scheduler_path = Path('Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/VoxelSurfaceScheduler.cs')
lod_path = Path('Assets/Tests/PlayMode/LodRenderingTests.cs')
test_path = Path('Assets/Tests/EditMode/ExactSnapshotMetadataJobTests.cs')
meta_path = Path('Assets/Tests/EditMode/ExactSnapshotMetadataJobTests.cs.meta')

# Cache counters and increments.
s = cache_path.read_text()
old = '''        public ulong CompletedBuildCount { get; private set; }\n        public ulong StaleBuildCount { get; private set; }\n        public ulong UploadedGeometryBytes { get; private set; }\n'''
new = '''        public ulong CompletedBuildCount { get; private set; }\n        public ulong StaleBuildCount { get; private set; }\n        public ulong ExactMetadataScheduleCount { get; private set; }\n        public ulong ExactMetadataCompleteCount { get; private set; }\n        public ulong ExactMetadataRevisionRejectCount { get; private set; }\n        public ulong ExactMetadataPinRejectCount { get; private set; }\n        public ulong UploadedGeometryBytes { get; private set; }\n'''
if old not in s: raise SystemExit('cache counter anchor missing')
s = s.replace(old, new, 1)

old = '''                _exactMetadataJobScheduled = false;\n                if (!PinnedRegionMetadataCurrent())\n                {\n                    ReleasePinnedRegionMetadataImmediate();\n'''
new = '''                _exactMetadataJobScheduled = false;\n                ExactMetadataCompleteCount++;\n                if (!PinnedRegionMetadataCurrent())\n                {\n                    ExactMetadataRevisionRejectCount++;\n                    ReleasePinnedRegionMetadataImmediate();\n'''
if old not in s: raise SystemExit('metadata completion anchor missing')
s = s.replace(old, new, 1)

# Count all four mixed-pin rejection shapes using stable snippets.
repls = [
('''                    if (!source.TryPinWorldBlock(worldBlock, out PinnedVoxelReadBlock pinned))\n                    {\n                        // Metadata said this block was mixed, but the coordinate can no longer\n''',
 '''                    if (!source.TryPinWorldBlock(worldBlock, out PinnedVoxelReadBlock pinned))\n                    {\n                        ExactMetadataPinRejectCount++;\n                        // Metadata said this block was mixed, but the coordinate can no longer\n'''),
('''                    if (pinned.Kind != VoxelReadBlockKind.Mixed || !pinned.HasPinnedPayload\n                        || pinned.MixedOffset != expected.MixedOffset)\n                    {\n                        if (pinned.HasPinnedPayload)\n''',
 '''                    if (pinned.Kind != VoxelReadBlockKind.Mixed || !pinned.HasPinnedPayload\n                        || pinned.MixedOffset != expected.MixedOffset)\n                    {\n                        ExactMetadataPinRejectCount++;\n                        if (pinned.HasPinnedPayload)\n'''),
('''                    else if (_pinnedMixedVoxels.Length != pinned.MixedVoxels.Length\n                             || _pinnedMixedSurfaceSemantics.Length\n                                != pinned.MixedSurfaceSemantics.Length\n                             || _pinnedMixedBoundarySamples.Length\n                                != pinned.MixedBoundarySamples.Length)\n                    {\n                        source.ReleasePinnedWorldBlock(in pinned.Pin);\n''',
 '''                    else if (_pinnedMixedVoxels.Length != pinned.MixedVoxels.Length\n                             || _pinnedMixedSurfaceSemantics.Length\n                                != pinned.MixedSurfaceSemantics.Length\n                             || _pinnedMixedBoundarySamples.Length\n                                != pinned.MixedBoundarySamples.Length)\n                    {\n                        ExactMetadataPinRejectCount++;\n                        source.ReleasePinnedWorldBlock(in pinned.Pin);\n'''),
]
for oldr,newr in repls:
    if oldr not in s: raise SystemExit('pin rejection anchor missing')
    s = s.replace(oldr,newr,1)

old = '''            if (!PinnedRegionMetadataCurrent())\n            {\n                ReleasePinnedRegionMetadataImmediate();\n                _discardBuildAfterPinRelease = true;\n'''
new = '''            if (!PinnedRegionMetadataCurrent())\n            {\n                ExactMetadataRevisionRejectCount++;\n                ReleasePinnedRegionMetadataImmediate();\n                _discardBuildAfterPinRelease = true;\n'''
# At this point only the second check remains without the increment inserted above.
if old not in s: raise SystemExit('second region revision anchor missing')
s = s.replace(old,new,1)

old = '''            _exactMetadataJobHandle = new ExactMixedBrickCompactJob\n            {\n                MixedFlags = _exactMixedFlags,\n                MixedIndices = _exactMixedBrickIndices,\n            }.Schedule(dependency);\n            _exactMetadataJobScheduled = true;\n'''
new = '''            _exactMetadataJobHandle = new ExactMixedBrickCompactJob\n            {\n                MixedFlags = _exactMixedFlags,\n                MixedIndices = _exactMixedBrickIndices,\n            }.Schedule(dependency);\n            ExactMetadataScheduleCount++;\n            _exactMetadataJobScheduled = true;\n'''
if old not in s: raise SystemExit('metadata schedule anchor missing')
s = s.replace(old,new,1)
cache_path.write_text(s)

# Aggregate the counters into step-4 metrics.
t = scheduler_path.read_text()
old = '''        public readonly uint Step4BuildPhaseMask;\n        public readonly uint Step4ActiveJobMask;\n        public readonly int RunningGeometryJobs;\n'''
new = '''        public readonly uint Step4BuildPhaseMask;\n        public readonly uint Step4ActiveJobMask;\n        public readonly ulong Step4ExactMetadataScheduled;\n        public readonly ulong Step4ExactMetadataCompleted;\n        public readonly ulong Step4ExactMetadataRevisionRejects;\n        public readonly ulong Step4ExactMetadataPinRejects;\n        public readonly int RunningGeometryJobs;\n'''
if old not in t: raise SystemExit('metrics fields anchor missing')
t = t.replace(old,new,1)

old = '''            Step4ActiveJobMask = isStep4 ? solids.ActiveJobMask : 0u;\n            RunningGeometryJobs = solids.RunningJobCount + water.RunningJobCount;\n'''
new = '''            Step4ActiveJobMask = isStep4 ? solids.ActiveJobMask : 0u;\n            Step4ExactMetadataScheduled = isStep4 ? solids.ExactMetadataScheduleCount : 0UL;\n            Step4ExactMetadataCompleted = isStep4 ? solids.ExactMetadataCompleteCount : 0UL;\n            Step4ExactMetadataRevisionRejects = isStep4 ? solids.ExactMetadataRevisionRejectCount : 0UL;\n            Step4ExactMetadataPinRejects = isStep4 ? solids.ExactMetadataPinRejectCount : 0UL;\n            RunningGeometryJobs = solids.RunningJobCount + water.RunningJobCount;\n'''
if old not in t: raise SystemExit('single metrics assignment anchor missing')
t = t.replace(old,new,1)

old = '''            uint step4BuildPhaseMask = 0, step4ActiveJobMask = 0;\n            long pendingUploadBytes = 0;\n'''
new = '''            uint step4BuildPhaseMask = 0, step4ActiveJobMask = 0;\n            ulong step4MetadataScheduled = 0, step4MetadataCompleted = 0;\n            ulong step4MetadataRevisionRejects = 0, step4MetadataPinRejects = 0;\n            long pendingUploadBytes = 0;\n'''
if old not in t: raise SystemExit('aggregate local anchor missing')
t = t.replace(old,new,1)

old = '''                    if (worker.ActiveBuildPhase >= 0)\n                        step4BuildPhaseMask |= 1u << worker.ActiveBuildPhase;\n                    step4ActiveJobMask |= worker.ActiveJobMask;\n                }\n'''
new = '''                    if (worker.ActiveBuildPhase >= 0)\n                        step4BuildPhaseMask |= 1u << worker.ActiveBuildPhase;\n                    step4ActiveJobMask |= worker.ActiveJobMask;\n                    step4MetadataScheduled += worker.ExactMetadataScheduleCount;\n                    step4MetadataCompleted += worker.ExactMetadataCompleteCount;\n                    step4MetadataRevisionRejects += worker.ExactMetadataRevisionRejectCount;\n                    step4MetadataPinRejects += worker.ExactMetadataPinRejectCount;\n                }\n'''
if old not in t: raise SystemExit('aggregate worker anchor missing')
t = t.replace(old,new,1)

old = '''            Step4BuildPhaseMask = step4BuildPhaseMask;\n            Step4ActiveJobMask = step4ActiveJobMask;\n            RunningGeometryJobs = running + water.RunningJobCount + schedulerRunningJobs;\n'''
new = '''            Step4BuildPhaseMask = step4BuildPhaseMask;\n            Step4ActiveJobMask = step4ActiveJobMask;\n            Step4ExactMetadataScheduled = step4MetadataScheduled;\n            Step4ExactMetadataCompleted = step4MetadataCompleted;\n            Step4ExactMetadataRevisionRejects = step4MetadataRevisionRejects;\n            Step4ExactMetadataPinRejects = step4MetadataPinRejects;\n            RunningGeometryJobs = running + water.RunningJobCount + schedulerRunningJobs;\n'''
if old not in t: raise SystemExit('aggregate assignment anchor missing')
t = t.replace(old,new,1)
scheduler_path.write_text(t)

# Add counters to the failure message.
l = lod_path.read_text()
old = '''                      + $"jobs:{metrics.Step4RunningJobs}/phaseMask:0x{metrics.Step4BuildPhaseMask:X}/"\n                      + $"jobMask:0x{metrics.Step4ActiveJobMask:X}.\");\n'''
new = '''                      + $"jobs:{metrics.Step4RunningJobs}/phaseMask:0x{metrics.Step4BuildPhaseMask:X}/"\n                      + $"jobMask:0x{metrics.Step4ActiveJobMask:X}/"\n                      + $"meta:{metrics.Step4ExactMetadataScheduled}/{metrics.Step4ExactMetadataCompleted}/"\n                      + $"revReject:{metrics.Step4ExactMetadataRevisionRejects}/"\n                      + $"pinReject:{metrics.Step4ExactMetadataPinRejects} "\n                      + $"stale:{metrics.RejectedStaleSolidBuilds}.\");\n'''
if old not in l: raise SystemExit('LOD diagnostic anchor missing')
l = l.replace(old,new,1)
lod_path.write_text(l)

# Focused raw step-4-scale dependency-chain fixture.
test_path.write_text(r'''using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction.Transvoxel;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class ExactSnapshotMetadataJobTests
    {
        [Test]
        public void StepFourSizedEightRegionMetadataChainCompletes()
        {
            const int cacheEdge = 34;
            const int cacheCount = cacheEdge * cacheEdge * cacheEdge;
            int3 cacheOrigin = new(48, 48, 48);
            var bricks = new NativeArray<TransvoxelDensityBrick>(
                cacheCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var flags = new NativeArray<byte>(
                cacheCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var mixed = new NativeList<int>(cacheCount, Allocator.TempJob);
            var encodedRegion = new NativeArray<int>(
                VoxelReadGrid.BlocksPerRegion, Allocator.TempJob,
                NativeArrayOptions.UninitializedMemory);
            try
            {
                // -2 is uniform material 1 in Storage's stable API encoding.
                for (int i = 0; i < encodedRegion.Length; i++) encodedRegion[i] = -2;

                JobHandle dependency = new ExactBrickMetadataClearJob
                {
                    Bricks = bricks,
                    MixedFlags = flags,
                }.Schedule(cacheCount, 256);

                int edge = VoxelReadGrid.BlocksPerRegionEdge;
                int3 cacheMaxExclusive = cacheOrigin + cacheEdge;
                int3 minRegion = cacheOrigin >> VoxelReadGrid.BlocksPerRegionEdgeLog2;
                int3 maxRegion = (cacheMaxExclusive - 1)
                               >> VoxelReadGrid.BlocksPerRegionEdgeLog2;
                int scheduledRegions = 0;
                for (int rz = minRegion.z; rz <= maxRegion.z; rz++)
                for (int ry = minRegion.y; ry <= maxRegion.y; ry++)
                for (int rx = minRegion.x; rx <= maxRegion.x; rx++)
                {
                    int3 regionCoord = new(rx, ry, rz);
                    int3 regionMin = regionCoord * edge;
                    int3 intersectionMin = math.max(cacheOrigin, regionMin);
                    int3 intersectionMax = math.min(cacheMaxExclusive, regionMin + edge);
                    int3 size = intersectionMax - intersectionMin;
                    int volume = size.x * size.y * size.z;
                    if (volume <= 0) continue;
                    scheduledRegions++;
                    dependency = new ExactBrickMetadataRegionJob
                    {
                        EncodedBlockRefs = encodedRegion,
                        RegionCoord = regionCoord,
                        IntersectionMinWorldBlock = intersectionMin,
                        IntersectionSize = size,
                        CacheOrigin = cacheOrigin,
                        BrickCacheEdge = cacheEdge,
                        Bricks = bricks,
                        MixedFlags = flags,
                    }.Schedule(volume, 128, dependency);
                }

                JobHandle final = new ExactMixedBrickCompactJob
                {
                    MixedFlags = flags,
                    MixedIndices = mixed,
                }.Schedule(dependency);
                JobHandle.ScheduleBatchedJobs();
                final.Complete();

                Assert.AreEqual(8, scheduledRegions,
                    "Fixture must reproduce the 2x2x2 region overlap of a boundary-crossing step-4 cache.");
                Assert.AreEqual(0, mixed.Length);
                Assert.AreEqual(1, bricks[0].Kind);
                Assert.AreEqual(1, bricks[cacheCount - 1].Kind);
                Assert.AreEqual(1, bricks[0].UniformMaterial);
                Assert.AreEqual(1, bricks[cacheCount - 1].UniformMaterial);
            }
            finally
            {
                if (encodedRegion.IsCreated) encodedRegion.Dispose();
                if (mixed.IsCreated) mixed.Dispose();
                if (flags.IsCreated) flags.Dispose();
                if (bricks.IsCreated) bricks.Dispose();
            }
        }
    }
}
''')
meta_path.write_text('fileFormatVersion: 2\nguid: eac4789708b54f73a4084250555dc6e7\n')
