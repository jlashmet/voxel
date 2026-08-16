from pathlib import Path

p = Path('Assets/Tests/PlayMode/AsyncGeometryStressTests.cs')
text = p.read_text()

if 'GeometryArenaPressureKeepsPublishedLeaseUntilReplacementConverges' in text:
    raise SystemExit('arena pressure gate already present')

old = 'using NUnit.Framework;\nusing Unity.Mathematics;\n'
new = 'using NUnit.Framework;\nusing Unity.Collections;\nusing Unity.Mathematics;\n'
if text.count(old) != 1:
    raise SystemExit(f'using anchor expected once, found {text.count(old)}')
text = text.replace(old, new, 1)

anchor = '''\n\n        [UnityTest, Timeout(900000)]\n        public IEnumerator WarmRepeatedClipmapTraversalAllocatesNoManagedGeometryMemory()\n'''
if text.count(anchor) != 1:
    raise SystemExit(f'test insertion anchor expected once, found {text.count(anchor)}')

test = r'''

        [UnityTest, Timeout(900000)]
        public IEnumerator GeometryArenaPressureKeepsPublishedLeaseUntilReplacementConverges()
        {
            // Two aligned leases exactly fill this arena. A replacement for A therefore cannot
            // stage until another published lease retires; pressure must become backlog, not a
            // fallback buffer allocation or a visible hole.
            yield return null;

            var arena = new SurfaceGeometryArena(
                vertexCapacity: 512,
                indexCapacity: 1024,
                argsRecordCapacity: 2);
            var entryA = new CpuTransvoxelChunkCache.Entry(
                int3.zero, CpuTransvoxelChunkCache.BaseVoxelsPerAxis,
                CpuTransvoxelChunkCache.BaseSourceStep, arena);
            var entryB = new CpuTransvoxelChunkCache.Entry(
                new int3(1, 0, 0), CpuTransvoxelChunkCache.BaseVoxelsPerAxis,
                CpuTransvoxelChunkCache.BaseSourceStep, arena);
            var vertices = new NativeList<SmoothSurfaceVertex>(4, Allocator.Persistent);
            var sixIndices = new NativeList<uint>(6, Allocator.Persistent);
            var threeIndices = new NativeList<uint>(3, Allocator.Persistent);
            try
            {
                vertices.Add(new SmoothSurfaceVertex { Position = new Vector3(0f, 0f, 0f) });
                vertices.Add(new SmoothSurfaceVertex { Position = new Vector3(1f, 0f, 0f) });
                vertices.Add(new SmoothSurfaceVertex { Position = new Vector3(1f, 1f, 0f) });
                vertices.Add(new SmoothSurfaceVertex { Position = new Vector3(0f, 1f, 0f) });
                sixIndices.Add(0); sixIndices.Add(1); sixIndices.Add(2);
                sixIndices.Add(0); sixIndices.Add(2); sixIndices.Add(3);
                threeIndices.Add(0); threeIndices.Add(1); threeIndices.Add(2);

                Assert.True(entryA.AdvanceUpload(vertices, sixIndices, int.MaxValue, out _));
                Assert.True(entryB.AdvanceUpload(vertices, sixIndices, int.MaxValue, out _));
                Assert.AreEqual(2, arena.UsedArgsRecords,
                    "Fixture did not fill both arena draw slots.");

                long liveBytesBeforePressure = entryA.GpuBytes;
                Assert.False(entryA.AdvanceUpload(
                    vertices, threeIndices, int.MaxValue, out int blockedUploadBytes),
                    "Replacement unexpectedly staged despite a completely full arena.");
                Assert.AreEqual(0, blockedUploadBytes,
                    "Arena pressure copied geometry before a staging lease existed.");
                Assert.Greater(arena.AllocationFailureCount, 0UL,
                    "Pressure path did not report bounded arena backpressure.");
                Assert.True(entryA.WaitingForArena,
                    "Blocked replacement was not left queued for later convergence.");
                Assert.True(entryA.Ready,
                    "Arena pressure removed A's previously published geometry.");
                Assert.AreEqual(6, entryA.IndexCount,
                    "Arena pressure mutated A's live draw record before replacement publication.");
                Assert.AreEqual(liveBytesBeforePressure, entryA.GpuBytes,
                    "Arena pressure changed A's live lease before replacement publication.");

                // Reclaiming one unrelated/off-screen lease is the scheduler's pressure response.
                // The next attempt must acquire that fixed arena range, publish atomically, and
                // release A's old range without creating any extra GPU buffer.
                entryB.Dispose();
                Assert.AreEqual(1, arena.UsedArgsRecords);
                Assert.True(entryA.AdvanceUpload(
                    vertices, threeIndices, int.MaxValue, out int convergedUploadBytes),
                    "Queued replacement did not converge after fixed-arena space was reclaimed.");
                Assert.Greater(convergedUploadBytes, 0);
                Assert.True(entryA.Ready);
                Assert.False(entryA.WaitingForArena);
                Assert.AreEqual(3, entryA.IndexCount,
                    "Replacement did not atomically become the new live draw record.");
                Assert.AreEqual(1, arena.UsedArgsRecords,
                    "Atomic swap should leave exactly one live arena lease after B is retired.");
            }
            finally
            {
                entryA.Dispose();
                entryB.Dispose();
                if (vertices.IsCreated) vertices.Dispose();
                if (sixIndices.IsCreated) sixIndices.Dispose();
                if (threeIndices.IsCreated) threeIndices.Dispose();
                arena.Dispose();
            }
        }
'''

p.write_text(text.replace(anchor, test + anchor, 1))

# Keep the checklist truthful: coverage exists after this patch, but the runtime gate is not
# checked until the Metal workflow actually passes.
doc = Path('docs/ASYNC_GEOMETRY_PIPELINE.md')
d = doc.read_text()
needle = '- [ ] Verify arena pressure causes backlog/convergence delay rather than frame spikes or visible holes.\n'
replacement = (needle + '  - Coverage: `AsyncGeometryStressTests.GeometryArenaPressureKeepsPublishedLeaseUntilReplacementConverges` fills a tiny fixed arena, proves the old lease stays live while replacement staging is blocked, then proves convergence after one unrelated lease is reclaimed.\n')
if d.count(needle) != 1:
    raise SystemExit(f'doc arena gate anchor expected once, found {d.count(needle)}')
doc.write_text(d.replace(needle, replacement, 1))

print('arena pressure gate applied')
