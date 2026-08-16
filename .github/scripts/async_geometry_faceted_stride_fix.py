from pathlib import Path


def replace_once(path: str, old: str, new: str) -> None:
    p = Path(path)
    text = p.read_text()
    count = text.count(old)
    if count != 1:
        raise SystemExit(f'{path}: expected one match, found {count}\n--- needle ---\n{old}')
    p.write_text(text.replace(old, new, 1))

mask = 'Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/Transvoxel/SnapshotFacetedMaskJob.cs'
replace_once(mask,
'''        public int BrickCacheEdge;\n        public int CellsPerAxis;\n''',
'''        public int BrickCacheEdge;\n        public int CellsPerAxis;\n        public int SourceStep;\n''')
replace_once(mask,
'''            int3 voxel = ChunkOriginVoxel + local;\n''',
'''            int3 voxel = ChunkOriginVoxel + local * SourceStep;\n''')
replace_once(mask,
'''                    int3 neighbour = voxel;\n                    neighbour[axis] += side == 0 ? -1 : 1;\n''',
'''                    int3 neighbour = voxel;\n                    neighbour[axis] += side == 0 ? -SourceStep : SourceStep;\n''')

merge = 'Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/Transvoxel/FacetedMergeJob.cs'
replace_once(merge,
'''        public int CellsPerAxis;\n        public float VoxelSize;\n''',
'''        public int CellsPerAxis;\n        public int SourceStep;\n        public float VoxelSize;\n''')
replace_once(merge,
'''            float3 p0 = ChunkOrigin;\n            p0[axis] += layer + (sign > 0 ? 1 : 0);\n            p0[axisA] += a;\n            p0[axisB] += b;\n            float3 p1 = p0, p2 = p0, p3 = p0;\n            p1[axisA] += width; p2[axisA] += width;\n            p2[axisB] += height; p3[axisB] += height;\n''',
'''            float3 p0 = ChunkOrigin;\n            p0[axis] += (layer + (sign > 0 ? 1 : 0)) * SourceStep;\n            p0[axisA] += a * SourceStep;\n            p0[axisB] += b * SourceStep;\n            float3 p1 = p0, p2 = p0, p3 = p0;\n            p1[axisA] += width * SourceStep; p2[axisA] += width * SourceStep;\n            p2[axisB] += height * SourceStep; p3[axisB] += height * SourceStep;\n''')

cache = 'Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/CpuTransvoxelChunkCache.cs'
replace_once(cache,
'''                BrickCacheEdge = BrickCacheEdge,\n                CellsPerAxis = CellsPerAxis,\n                FaceMasks = _facetedMasks,\n''',
'''                BrickCacheEdge = BrickCacheEdge,\n                CellsPerAxis = CellsPerAxis,\n                SourceStep = SourceStep,\n                FaceMasks = _facetedMasks,\n''')
replace_once(cache,
'''                ChunkOrigin = _build.Coordinate * VoxelsPerAxis,\n                CellsPerAxis = CellsPerAxis,\n                VoxelSize = voxelSize,\n''',
'''                ChunkOrigin = _build.Coordinate * VoxelsPerAxis,\n                CellsPerAxis = CellsPerAxis,\n                SourceStep = SourceStep,\n                VoxelSize = voxelSize,\n''')

# Focused functional test: one faceted step-8 cell must occupy an 8-voxel world extent, not 1.
test = Path('Assets/Tests/EditMode/FacetedLodStrideTests.cs')
if test.exists():
    raise SystemExit(f'{test}: already exists')
test.write_text(r'''using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction.Transvoxel;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class FacetedLodStrideTests
    {
        [Test]
        public void StepEightFacetedMergeSpansEightVoxelsPerCell()
        {
            const int cells = 2;
            const int sourceStep = 8;
            const float voxelSize = 0.1f;
            int cellsPerPlane = cells * cells;
            var masks = new NativeArray<uint>(6 * cells * cellsPerPlane,
                                              Allocator.TempJob);
            var vertices = new NativeList<SmoothSurfaceVertex>(8, Allocator.TempJob);
            var indices = new NativeList<uint>(12, Allocator.TempJob);
            try
            {
                // +X face, layer 0, a=0, b=0. Encoded value one decodes to attributes zero.
                int face = 1;
                int layer = 0;
                int offset = (face * cells + layer) * cellsPerPlane;
                masks[offset] = 1u;

                var job = new FacetedMergeJob
                {
                    FaceMasks = masks,
                    Vertices = vertices,
                    Indices = indices,
                    ChunkOrigin = new int3(100, 200, 300),
                    CellsPerAxis = cells,
                    SourceStep = sourceStep,
                    VoxelSize = voxelSize,
                };
                job.Run();

                Assert.AreEqual(4, vertices.Length);
                Assert.AreEqual(6, indices.Length);
                float expectedMetres = sourceStep * voxelSize;
                float3 p0 = vertices[0].Position;
                float3 p1 = vertices[1].Position;
                float3 p3 = vertices[3].Position;
                Assert.AreEqual(expectedMetres, math.distance(p0, p1), 1e-5f,
                    "Faceted width collapsed to one voxel instead of one LOD cell.");
                Assert.AreEqual(expectedMetres, math.distance(p0, p3), 1e-5f,
                    "Faceted height collapsed to one voxel instead of one LOD cell.");
                Assert.AreEqual((100 + sourceStep) * voxelSize, p0.x, 1e-5f,
                    "+X face plane ignored SourceStep when locating the coarse cell boundary.");
            }
            finally
            {
                if (indices.IsCreated) indices.Dispose();
                if (vertices.IsCreated) vertices.Dispose();
                if (masks.IsCreated) masks.Dispose();
            }
        }

        [Test]
        public void ExactFacetedSnapshotReadsAtTheRingStride()
        {
            string root = UnityEngine.Application.dataPath;
            string path = System.IO.Path.Combine(root,
                "VoxelEngine/Rendering/Runtime/SurfaceExtraction/Transvoxel/SnapshotFacetedMaskJob.cs");
            string source = System.IO.File.ReadAllText(path);
            StringAssert.Contains("public int SourceStep;", source);
            StringAssert.Contains("ChunkOriginVoxel + local * SourceStep", source);
            StringAssert.Contains("side == 0 ? -SourceStep : SourceStep", source);
        }
    }
}
''')
Path(str(test) + '.meta').write_text('fileFormatVersion: 2\nguid: 49067858073148ec9bb2c67ff408349c\n')

# Add source-level architecture coverage to the existing contract file too, so the scoped suite
# catches missing scheduler wiring even if this focused functional fixture is filtered separately.
arch = Path('Assets/Tests/EditMode/GeometryPipelineArchitectureTests.cs')
a = arch.read_text()
anchor = '\n        [Test]\n        public void FramePathJobCompletionIsNonBlockingAndObservable()'
if a.count(anchor) != 1:
    raise SystemExit(f'architecture insertion anchor expected once, found {a.count(anchor)}')
block = r'''
        [Test]
        public void FacetedLodPathCarriesSourceStepThroughSnapshotAndMerge()
        {
            string cache = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "CpuTransvoxelChunkCache.cs"));
            string snapshotMask = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "Transvoxel", "SnapshotFacetedMaskJob.cs"));
            string merge = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "Transvoxel", "FacetedMergeJob.cs"));

            StringAssert.Contains("ChunkOriginVoxel + local * SourceStep", snapshotMask);
            StringAssert.Contains("side == 0 ? -SourceStep : SourceStep", snapshotMask);
            StringAssert.Contains("width * SourceStep", merge);
            StringAssert.Contains("height * SourceStep", merge);

            int snapshotSchedule = cache.IndexOf("private void ScheduleSnapshotFacetedMaskJob()",
                                                 StringComparison.Ordinal);
            int mergeSchedule = cache.IndexOf("private void ScheduleFacetedMergeJob(float voxelSize)",
                                              StringComparison.Ordinal);
            Assert.GreaterOrEqual(snapshotSchedule, 0);
            Assert.Greater(mergeSchedule, snapshotSchedule);
            StringAssert.Contains("SourceStep = SourceStep",
                cache.Substring(snapshotSchedule, mergeSchedule - snapshotSchedule));
            int mergeEnd = cache.IndexOf("private void", mergeSchedule + 1,
                                         StringComparison.Ordinal);
            Assert.Greater(mergeEnd, mergeSchedule);
            StringAssert.Contains("SourceStep = SourceStep",
                cache.Substring(mergeSchedule, mergeEnd - mergeSchedule));
        }

'''
arch.write_text(a.replace(anchor, '\n' + block + anchor, 1))

# Document this as a correctness fix, not as completion of feature-preserving step-8 LOD.
doc = Path('docs/ASYNC_GEOMETRY_PIPELINE.md')
d = doc.read_text()
needle = '- [ ] Replace the temporary exact step-8 fallback with a feature-preserving render LOD representation (surface-aware/SDF/min-max/HLOD).\n'
addition = needle + '  - [x] Correct the existing faceted LOD path so snapshot sampling, neighbour tests, and greedy quad extents all honor `SourceStep`; this removes a separate coarse-ring architectural scaling bug before changing representation.\n'
if d.count(needle) != 1:
    raise SystemExit(f'doc LOD anchor expected once, found {d.count(needle)}')
doc.write_text(d.replace(needle, addition, 1))

print('faceted LOD stride fix applied')
