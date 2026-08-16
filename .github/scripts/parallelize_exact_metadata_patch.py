from pathlib import Path

cache = Path('Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/CpuTransvoxelChunkCache.cs')
tests = Path('Assets/Tests/EditMode/GeometryPipelineArchitectureTests.cs')

s = cache.read_text()
old = '''            JobHandle dependency = new ExactBrickMetadataClearJob
            {
                Bricks = _densityBricks,
                MixedFlags = _exactMixedFlags,
            }.Schedule(BrickCacheCount, 256);
'''
new = '''            JobHandle clearHandle = new ExactBrickMetadataClearJob
            {
                Bricks = _densityBricks,
                MixedFlags = _exactMixedFlags,
            }.Schedule(BrickCacheCount, 256);
            // Region intersections are disjoint cache ranges. Schedule every copy behind the
            // shared clear only, then combine their handles once before compaction. Chaining each
            // copy behind the previous region serializes phase-0 snapshot work and can starve
            // coarse LOD workers on Metal even though the copies are independent.
            JobHandle dependency = clearHandle;
'''
if old not in s:
    raise SystemExit('clear/dependency anchor not found')
s = s.replace(old, new, 1)
old2 = '''                dependency = new ExactBrickMetadataRegionJob
                {
                    EncodedBlockRefs = pinned.EncodedBlockRefs,
                    RegionCoord = regionCoord,
                    IntersectionMinWorldBlock = intersectionMin,
                    IntersectionSize = size,
                    CacheOrigin = cacheOrigin,
                    BrickCacheEdge = BrickCacheEdge,
                    Bricks = _densityBricks,
                    MixedFlags = _exactMixedFlags,
                }.Schedule(volume, 128, dependency);
'''
new2 = '''                JobHandle regionHandle = new ExactBrickMetadataRegionJob
                {
                    EncodedBlockRefs = pinned.EncodedBlockRefs,
                    RegionCoord = regionCoord,
                    IntersectionMinWorldBlock = intersectionMin,
                    IntersectionSize = size,
                    CacheOrigin = cacheOrigin,
                    BrickCacheEdge = BrickCacheEdge,
                    Bricks = _densityBricks,
                    MixedFlags = _exactMixedFlags,
                }.Schedule(volume, 128, clearHandle);
                dependency = JobHandle.CombineDependencies(dependency, regionHandle);
'''
if old2 not in s:
    raise SystemExit('region dependency anchor not found')
s = s.replace(old2, new2, 1)
cache.write_text(s)

# Architecture guard: independent region jobs must fan out from clear rather than serialize.
t = tests.read_text()
anchor = '''        [Test]\n        public void GeometryJobsAreFlushedOnceWithoutWaiting()\n'''
test = '''        [Test]\n        public void ExactMetadataRegionCopiesFanOutFromSharedClear()\n        {\n            string cacheSource = ReadRenderingSource(\n                Path.Combine(\"SurfaceExtraction\", \"CpuTransvoxelChunkCache.cs\"));\n            StringAssert.Contains(\"JobHandle clearHandle = new ExactBrickMetadataClearJob\", cacheSource);\n            StringAssert.Contains(\".Schedule(volume, 128, clearHandle);\", cacheSource);\n            StringAssert.Contains(\"JobHandle.CombineDependencies(dependency, regionHandle)\", cacheSource);\n            StringAssert.DoesNotContain(\".Schedule(volume, 128, dependency);\", cacheSource,\n                \"Exact metadata region copies must not form a serial dependency ladder.\");\n        }\n\n'''
if 'ExactMetadataRegionCopiesFanOutFromSharedClear' not in t:
    if anchor not in t:
        raise SystemExit('test insertion anchor not found')
    t = t.replace(anchor, test + anchor, 1)
    tests.write_text(t)
