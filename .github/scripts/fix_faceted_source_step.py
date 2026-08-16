from pathlib import Path


def once(text, old, new, label):
    count = text.count(old)
    if count != 1:
        raise SystemExit(f'{label}: expected one match, found {count}')
    return text.replace(old, new, 1)

cache_path = Path('Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/CpuTransvoxelChunkCache.cs')
s = cache_path.read_text()

s = once(s,
'''                BrickCacheEdge = BrickCacheEdge,
                CellsPerAxis = CellsPerAxis,
                FaceMasks = _facetedMasks,
''',
'''                BrickCacheEdge = BrickCacheEdge,
                CellsPerAxis = CellsPerAxis,
                SourceStep = SourceStep,
                FaceMasks = _facetedMasks,
''',
'snapshot faceted source step')

s = once(s,
'''                ChunkOrigin = _build.Coordinate * VoxelsPerAxis,
                CellsPerAxis = CellsPerAxis,
                VoxelSize = voxelSize,
''',
'''                ChunkOrigin = _build.Coordinate * VoxelsPerAxis,
                CellsPerAxis = CellsPerAxis,
                SourceStep = SourceStep,
                VoxelSize = voxelSize,
''',
'faceted merge source step')

s = once(s,
'''                int3 voxel = chunkOrigin + local;
                ReadSnapshotCell(voxel, out byte material, out uint surface,
''',
'''                int3 voxel = chunkOrigin + local * SourceStep;
                ReadSnapshotCell(voxel, out byte material, out uint surface,
''',
'main-thread faceted sample stride')

s = once(s,
'''                int3 neighbour = voxel;
                neighbour[axis] += sign;
                ReadSnapshotCell(neighbour, out byte neighbourMaterial, out _,
''',
'''                int3 neighbour = voxel;
                neighbour[axis] += sign * SourceStep;
                ReadSnapshotCell(neighbour, out byte neighbourMaterial, out _,
''',
'main-thread faceted neighbour stride')

s = once(s,
'''            float3 p0 = chunkOrigin;
            p0[axis] += layer + (sign > 0 ? 1 : 0);
            p0[axisA] += a;
            p0[axisB] += b;
            float3 p1 = p0;
            float3 p2 = p0;
            float3 p3 = p0;
            p1[axisA] += width;
            p2[axisA] += width;
            p2[axisB] += height;
            p3[axisB] += height;
''',
'''            float3 p0 = chunkOrigin;
            p0[axis] += (layer + (sign > 0 ? 1 : 0)) * SourceStep;
            p0[axisA] += a * SourceStep;
            p0[axisB] += b * SourceStep;
            float3 p1 = p0;
            float3 p2 = p0;
            float3 p3 = p0;
            p1[axisA] += width * SourceStep;
            p2[axisA] += width * SourceStep;
            p2[axisB] += height * SourceStep;
            p3[axisB] += height * SourceStep;
''',
'main-thread faceted emit stride')
cache_path.write_text(s)

arch_path = Path('Assets/Tests/EditMode/GeometryPipelineArchitectureTests.cs')
a = arch_path.read_text()
if 'CoarseFacetedGeometryUsesRingSourceStep' not in a:
    marker = '\n\n        [Test]\n        public void CompletedJobResultsAreMergedUnderDeadline()'
    addition = r'''

        [Test]
        public void CoarseFacetedGeometryUsesRingSourceStep()
        {
            string cache = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "CpuTransvoxelChunkCache.cs"));
            string mask = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "Transvoxel", "SnapshotFacetedMaskJob.cs"));
            string merge = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "Transvoxel", "FacetedMergeJob.cs"));

            StringAssert.Contains("SourceStep = SourceStep", cache);
            StringAssert.Contains("local * SourceStep", cache);
            StringAssert.Contains("sign * SourceStep", cache);
            StringAssert.Contains("public int SourceStep;", mask);
            StringAssert.Contains("ChunkOriginVoxel + local * step", mask);
            StringAssert.Contains("side == 0 ? -step : step", mask);
            StringAssert.Contains("public int SourceStep;", merge);
            StringAssert.Contains("width * step", merge);
            StringAssert.Contains("height * step", merge);
        }
'''
    if marker not in a:
        raise SystemExit('architecture insertion marker missing')
    a = a.replace(marker, addition + marker, 1)
arch_path.write_text(a)

assert s.count('SourceStep = SourceStep') >= 2
print('faceted source-step wiring applied')
