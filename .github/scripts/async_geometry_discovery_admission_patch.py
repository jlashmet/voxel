from pathlib import Path

cache_path = Path('Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/CpuTransvoxelChunkCache.cs')
scheduler_path = Path('Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/VoxelSurfaceScheduler.cs')
test_path = Path('Assets/Tests/EditMode/SurfaceBrickDiscoveryTests.cs')
docs_path = Path('docs/ASYNC_GEOMETRY_PIPELINE.md')

s = cache_path.read_text()
anchor = '''        /// <summary>\n        /// Discovers/invalidates chunks from the scheduler's authoritative surface-brick stream.\n        /// The one-sample Transvoxel padding can consume a neighbouring chunk's edge,\n        /// so face/edge/corner neighbours are dirtied only when the brick lies on a chunk border.\n        /// </summary>\n        public void InvalidateSurfaceBricks(IReadOnlyList<int3> worldBricks)\n'''
insert = '''        /// <summary>\n        /// Admits chunks discovered from immutable Storage surface summaries. Discovery is not a\n        /// mutation signal: once a chunk is known, its build snapshots the entire authoritative\n        /// chunk, so later 512-brick publication slices from the same unchanged region must not\n        /// advance its source generation and kill in-flight geometry. Real voxel edits continue\n        /// through <see cref="InvalidateSurfaceBricks"/> and region invalidation below.\n        /// Returns the number of newly admitted chunks.\n        /// </summary>\n        internal int DiscoverSurfaceBricks(IReadOnlyList<int3> worldBricks)\n        {\n            if (worldBricks == null) return 0;\n            int admitted = 0;\n\n            for (int i = 0; i < worldBricks.Count; i++)\n            {\n                int3 brick = worldBricks[i];\n                int3 baseChunk = new(FloorDiv(brick.x, BricksPerAxis),\n                                     FloorDiv(brick.y, BricksPerAxis),\n                                     FloorDiv(brick.z, BricksPerAxis));\n                int rx = FloorMod(brick.x, BricksPerAxis);\n                int ry = FloorMod(brick.y, BricksPerAxis);\n                int rz = FloorMod(brick.z, BricksPerAxis);\n\n                int minX = rx == 0 ? -1 : 0;\n                int maxX = rx == BricksPerAxis - 1 ? 1 : 0;\n                int minY = ry == 0 ? -1 : 0;\n                int maxY = ry == BricksPerAxis - 1 ? 1 : 0;\n                int minZ = rz == 0 ? -1 : 0;\n                int maxZ = rz == BricksPerAxis - 1 ? 1 : 0;\n\n                for (int z = minZ; z <= maxZ; z++)\n                for (int y = minY; y <= maxY; y++)\n                for (int x = minX; x <= maxX; x++)\n                {\n                    int3 chunk = baseChunk + new int3(x, y, z);\n                    if (!OwnsShard(chunk) || _known.Contains(chunk)) continue;\n                    if (!TrackKnown(chunk)) continue;\n                    Invalidate(chunk);\n                    admitted++;\n                }\n            }\n            return admitted;\n        }\n\n        /// <summary>\n        /// Invalidates chunks touched by an authoritative voxel change. Unlike surface discovery,\n        /// this path intentionally advances already-known chunk generations so active/ready\n        /// geometry cannot publish stale voxel content.\n        /// The one-sample Transvoxel padding can consume a neighbouring chunk's edge,\n        /// so face/edge/corner neighbours are dirtied only when the brick lies on a chunk border.\n        /// </summary>\n        public void InvalidateSurfaceBricks(IReadOnlyList<int3> worldBricks)\n'''
if anchor not in s: raise SystemExit('cache discovery anchor missing')
s = s.replace(anchor, insert, 1)
cache_path.write_text(s)

q = scheduler_path.read_text()
old = '''            for (int i = 0; i < _allWorkers.Length; i++)\n                _allWorkers[i].InvalidateSurfaceBricks(_discoveredSurfaceBricks);\n'''
new = '''            for (int i = 0; i < _allWorkers.Length; i++)\n                _allWorkers[i].DiscoverSurfaceBricks(_discoveredSurfaceBricks);\n'''
if old not in q: raise SystemExit('scheduler discovery consumer anchor missing')
q = q.replace(old, new, 1)
scheduler_path.write_text(q)

t = test_path.read_text()
anchor = '''        [Test]\n        public void SurfaceDiscoveryOutsideClipmapDoesNotCreateDirtyBuildWork()\n'''
test = '''        [Test]\n        public void RepeatedSurfaceDiscoveryDoesNotReinvalidateKnownChunk()\n        {\n            using var cache = new CpuTransvoxelChunkCache(sourceStep: 4);\n            cache.SetClipmapWindow(int3.zero, radius: 1);\n\n            // Interior block: maps to exactly one chunk and does not exercise halo neighbours.\n            int3 brick = new(1, 1, 1);\n            Assert.AreEqual(1, cache.DiscoverSurfaceBricks(new[] { brick }),\n                "The first immutable summary publication must admit the chunk.");\n            Assert.AreEqual(1, cache.KnownCount);\n            Assert.AreEqual(1, cache.DirtyCount);\n\n            Assert.AreEqual(0, cache.DiscoverSurfaceBricks(new[] { brick }),\n                "Later publication slices for the same unchanged region must not create a new "\n              + "source generation for an already-known chunk.");\n            Assert.AreEqual(1, cache.KnownCount);\n            Assert.AreEqual(1, cache.DirtyCount);\n\n            // Real edits keep the old semantics: known chunks are explicitly invalidated. The\n            // dirty set coalesces membership, but the call is still routed through the mutation\n            // path rather than discovery admission.\n            cache.InvalidateSurfaceBricks(new[] { brick });\n            Assert.AreEqual(1, cache.KnownCount);\n            Assert.AreEqual(1, cache.DirtyCount);\n        }\n\n'''
if anchor not in t: raise SystemExit('discovery test anchor missing')
if 'RepeatedSurfaceDiscoveryDoesNotReinvalidateKnownChunk' in t: raise SystemExit('test exists')
t = t.replace(anchor, test + anchor, 1)
test_path.write_text(t)

d = docs_path.read_text()
needle = '- [x] Make surface-brick discovery asynchronous and gate `Complete()` behind `IsCompleted`.\n'
replacement = needle + '- [x] Treat surface discovery as admission-only after first sighting; repeated 512-brick publication slices never advance an already-known chunk generation.\n'
if needle not in d: raise SystemExit('docs anchor missing')
d = d.replace(needle, replacement, 1)
docs_path.write_text(d)
