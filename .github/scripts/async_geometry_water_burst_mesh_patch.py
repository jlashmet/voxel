from pathlib import Path


def once(text, old, new, label):
    n = text.count(old)
    if n != 1:
        raise SystemExit(f'{label}: expected one match, found {n}')
    return text.replace(old, new, 1)

root = Path('Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction')

# -----------------------------------------------------------------------------
# Worker-side greedy water emitter. Snapshot layout per brick:
# 512 local voxels + 6 * 64 neighbour-face material samples.
# -----------------------------------------------------------------------------
job_path = root / 'WaterBrickMeshBatchJob.cs'
job_path.write_text(r'''using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace VoxelEngine.Rendering.Runtime.SurfaceExtraction
{
    /// <summary>
    /// Greedy water surface extraction over a small immutable material snapshot batch. All mask,
    /// merge, winding and vertex emission work runs off the frame thread. The main thread copies
    /// only 896 material bytes per selected 8^3 brick (local payload + six boundary faces).
    /// </summary>
    [BurstCompile]
    internal struct WaterBrickMeshBatchJob : IJob
    {
        internal const int Edge = 8;
        internal const int VoxelsPerBrick = Edge * Edge * Edge;
        internal const int FaceArea = Edge * Edge;
        internal const int FaceCount = 6;
        internal const int SnapshotStride = VoxelsPerBrick + FaceCount * FaceArea;
        internal const int MaxBatch = 8;
        private const uint FullyLitOcclusion = 0x0000FF00u;

        [ReadOnly] public NativeArray<int3> BrickBaseVoxels;
        [ReadOnly] public NativeArray<byte> SnapshotMaterials;
        public int BatchCount;
        public float VoxelSize;
        public NativeArray<byte> MaskScratch;
        public NativeList<SmoothSurfaceVertex> Vertices;
        public NativeList<uint> Indices;
        public NativeArray<int> Overflow;

        public void Execute()
        {
            Overflow[0] = 0;
            for (int batch = 0; batch < BatchCount; batch++)
            {
                if (!EmitBrick(batch, BrickBaseVoxels[batch])) return;
            }
        }

        private bool EmitBrick(int batch, int3 brickBaseVoxel)
        {
            for (int axis = 0; axis < 3; axis++)
            {
                int axisA = (axis + 1) % 3;
                int axisB = (axis + 2) % 3;
                for (int sign = -1; sign <= 1; sign += 2)
                for (int layer = 0; layer < Edge; layer++)
                {
                    BuildMask(batch, axis, axisA, axisB, sign, layer);
                    if (!MergeMask(brickBaseVoxel, axis, axisA, axisB,
                                   sign, layer))
                        return false;
                }
            }
            return true;
        }

        private void BuildMask(int batch, int axis, int axisA, int axisB,
                               int sign, int layer)
        {
            int strideAxis = Stride(axis);
            int strideA = Stride(axisA);
            int strideB = Stride(axisB);
            int neighbourLayer = layer + sign;
            bool crossesBrick = (uint)neighbourLayer >= Edge;
            int layerBase = layer * strideAxis;
            int snapshotBase = batch * SnapshotStride;
            int face = axis * 2 + (sign > 0 ? 1 : 0);
            int faceBase = snapshotBase + VoxelsPerBrick + face * FaceArea;

            for (int b = 0; b < Edge; b++)
            for (int a = 0; a < Edge; a++)
            {
                int index = layerBase + b * strideB + a * strideA;
                byte material = SnapshotMaterials[snapshotBase + index];
                if (!IsWater(material))
                {
                    MaskScratch[a + b * Edge] = 0;
                    continue;
                }

                byte neighbour = crossesBrick
                    ? SnapshotMaterials[faceBase + a + b * Edge]
                    : SnapshotMaterials[snapshotBase + index + sign * strideAxis];
                MaskScratch[a + b * Edge] = neighbour == 0 ? material : (byte)0;
            }
        }

        private bool MergeMask(int3 brickBaseVoxel, int axis, int axisA, int axisB,
                               int sign, int layer)
        {
            for (int b = 0; b < Edge; b++)
            for (int a = 0; a < Edge; a++)
            {
                byte material = MaskScratch[a + b * Edge];
                if (material == 0) continue;

                int width = 1;
                while (a + width < Edge
                       && MaskScratch[a + width + b * Edge] == material)
                    width++;

                int height = 1;
                bool extend = true;
                while (b + height < Edge && extend)
                {
                    for (int k = 0; k < width; k++)
                    {
                        if (MaskScratch[a + k + (b + height) * Edge] == material)
                            continue;
                        extend = false;
                        break;
                    }
                    if (extend) height++;
                }

                for (int hb = 0; hb < height; hb++)
                for (int ha = 0; ha < width; ha++)
                    MaskScratch[a + ha + (b + hb) * Edge] = 0;

                if (!EmitQuad(material, brickBaseVoxel, axis, axisA, axisB,
                              sign, layer, a, b, width, height))
                    return false;
            }
            return true;
        }

        private bool EmitQuad(byte material, int3 brickBaseVoxel,
                              int axis, int axisA, int axisB, int sign, int layer,
                              int a, int b, int width, int height)
        {
            if (Vertices.Length + 4 > Vertices.Capacity
                || Indices.Length + 6 > Indices.Capacity)
            {
                Overflow[0] = 1;
                return false;
            }

            int planeVoxel = brickBaseVoxel[axis] + layer + (sign > 0 ? 1 : 0);
            int a0 = brickBaseVoxel[axisA] + a;
            int b0 = brickBaseVoxel[axisB] + b;
            float3 p0 = Corner(axis, axisA, axisB, planeVoxel, a0, b0);
            float3 p1 = Corner(axis, axisA, axisB, planeVoxel, a0 + width, b0);
            float3 p2 = Corner(axis, axisA, axisB, planeVoxel, a0 + width, b0 + height);
            float3 p3 = Corner(axis, axisA, axisB, planeVoxel, a0, b0 + height);
            float3 normal = float3.zero;
            normal[axis] = sign;

            uint baseIndex = (uint)Vertices.Length;
            uint m = material;
            Vertices.AddNoResize(Vertex(p0, normal, m));
            Vertices.AddNoResize(Vertex(p1, normal, m));
            Vertices.AddNoResize(Vertex(p2, normal, m));
            Vertices.AddNoResize(Vertex(p3, normal, m));

            bool flip = math.dot(math.cross(p1 - p0, p2 - p0), normal) < 0f;
            if (flip)
            {
                Indices.AddNoResize(baseIndex);
                Indices.AddNoResize(baseIndex + 2);
                Indices.AddNoResize(baseIndex + 1);
                Indices.AddNoResize(baseIndex);
                Indices.AddNoResize(baseIndex + 3);
                Indices.AddNoResize(baseIndex + 2);
            }
            else
            {
                Indices.AddNoResize(baseIndex);
                Indices.AddNoResize(baseIndex + 1);
                Indices.AddNoResize(baseIndex + 2);
                Indices.AddNoResize(baseIndex);
                Indices.AddNoResize(baseIndex + 2);
                Indices.AddNoResize(baseIndex + 3);
            }
            return true;
        }

        private float3 Corner(int axis, int axisA, int axisB, int plane, int a, int b)
        {
            float3 v = float3.zero;
            v[axis] = plane * VoxelSize;
            v[axisA] = a * VoxelSize;
            v[axisB] = b * VoxelSize;
            return v;
        }

        private static SmoothSurfaceVertex Vertex(float3 position, float3 normal, uint material) =>
            new()
            {
                Position = new Vector3(position.x, position.y, position.z),
                Normal = new Vector3(normal.x, normal.y, normal.z),
                Material = material,
                Active = FullyLitOcclusion,
            };

        private static int Stride(int axis) => axis == 0 ? 1 : axis == 1 ? Edge : Edge * Edge;
        private static bool IsWater(byte material) => material == 11 || material == 16;
    }
}
''')
(root / 'WaterBrickMeshBatchJob.cs.meta').write_text(
    'fileFormatVersion: 2\nguid: 38ce57071d6249209cc0c776a48473b8\n')

# -----------------------------------------------------------------------------
# Water cache stages immutable snapshots and never emits mesh topology on frame thread.
# -----------------------------------------------------------------------------
path = root / 'CpuWaterSurfaceChunkCache.cs'
s = path.read_text()
s = once(s,
'''        private const int BricksPerSlice = 8;
        private const int BuildSelectionCandidatesPerPrepare = 32;''',
'''        private const int BuildSelectionCandidatesPerPrepare = 32;''', 'remove synchronous brick slice')

# Replace output/scratch fields.
s = once(s,
'''        private NativeList<SmoothSurfaceVertex> _vertices;
        private NativeList<uint> _indices;
        private readonly NativeArray<byte> _brickMaterials =
            new(VoxelReadGrid.VoxelsPerBlock, Allocator.Persistent);
        private readonly NativeArray<ushort> _surfaceScratch =
            new(VoxelReadGrid.VoxelsPerBlock, Allocator.Persistent);
        private readonly NativeArray<byte> _boundaryScratch =
            new(VoxelReadGrid.VoxelsPerBlock, Allocator.Persistent);
        private readonly byte[] _mask = new byte[E * E];
        private BuildState _build;''',
'''        private NativeList<SmoothSurfaceVertex> _vertices;
        private NativeList<uint> _indices;
        private readonly NativeArray<byte> _brickMaterials =
            new(VoxelReadGrid.VoxelsPerBlock, Allocator.Persistent);
        private readonly NativeArray<ushort> _surfaceScratch =
            new(VoxelReadGrid.VoxelsPerBlock, Allocator.Persistent);
        private readonly NativeArray<byte> _boundaryScratch =
            new(VoxelReadGrid.VoxelsPerBlock, Allocator.Persistent);
        private NativeArray<int3> _waterBatchBrickBases;
        private NativeArray<byte> _waterBatchMaterials;
        private NativeArray<byte> _waterMeshMask;
        private NativeArray<int> _waterMeshOverflow;
        private JobHandle _waterMeshJobHandle;
        private bool _waterMeshJobScheduled;
        private bool _discardBuildAfterMeshJob;
        private int _waterBatchCount;
        public ulong MeshOverflowCount { get; private set; }
        private BuildState _build;''', 'water async job fields')

# Ensure using Unity.Jobs.
s = once(s,
'''using Unity.Collections;
using Unity.Mathematics;''',
'''using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;''', 'water jobs import')

# Constructor preallocates final outputs to arena capacity so Burst uses AddNoResize only.
s = once(s,
'''            _geometryArena = new SurfaceGeometryArena(
                ArenaVertexCapacity, ArenaIndexCapacity, ArenaDrawCapacity);
            _vertices = new NativeList<SmoothSurfaceVertex>(4096, Allocator.Persistent);
            _indices = new NativeList<uint>(6144, Allocator.Persistent);''',
'''            _geometryArena = new SurfaceGeometryArena(
                ArenaVertexCapacity, ArenaIndexCapacity, ArenaDrawCapacity);
            _vertices = new NativeList<SmoothSurfaceVertex>(ArenaVertexCapacity, Allocator.Persistent);
            _indices = new NativeList<uint>(ArenaIndexCapacity, Allocator.Persistent);
            _waterBatchBrickBases = new NativeArray<int3>(
                WaterBrickMeshBatchJob.MaxBatch, Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
            _waterBatchMaterials = new NativeArray<byte>(
                WaterBrickMeshBatchJob.MaxBatch * WaterBrickMeshBatchJob.SnapshotStride,
                Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            _waterMeshMask = new NativeArray<byte>(
                WaterBrickMeshBatchJob.FaceArea, Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
            _waterMeshOverflow = new NativeArray<int>(1, Allocator.Persistent,
                                                      NativeArrayOptions.ClearMemory);''',
'water async scratch allocation')

# Replace StepBuild with staged snapshot -> Burst job flow.
start = s.index('        private bool StepBuild(IRegionReadSource storage, float voxelSize, double deadline)')
end = s.index('        private void FinishCpuBuild()', start)
step = r'''        private bool StepBuild(IRegionReadSource storage, float voxelSize, double deadline)
        {
            if (_waterMeshJobScheduled)
            {
                if (!_waterMeshJobHandle.IsCompleted) return false;
                _waterMeshJobHandle.Complete();
                _waterMeshJobScheduled = false;
                _waterBatchCount = 0;

                if (_discardBuildAfterMeshJob)
                {
                    _discardBuildAfterMeshJob = false;
                    ResetBuildOutput();
                    return false;
                }
                if (_waterMeshOverflow[0] != 0)
                {
                    int3 retry = _build.Coordinate;
                    MeshOverflowCount++;
                    ResetBuildOutput();
                    MarkDirty(retry);
                    return false;
                }
                if (_desiredVersions.TryGetValue(_build.Coordinate, out ulong desired)
                    && desired > _build.SourceVersion)
                {
                    StaleBuildCount++;
                    ResetBuildOutput();
                    return false;
                }
            }

            if (!_waterBricks.TryGetValue(_build.Coordinate, out HashSet<int3> set)
                || set.Count == 0)
                return true;

            const int totalBrickSlots = BricksPerAxis * BricksPerAxis * BricksPerAxis;
            RegionReadView cachedRegion = default;
            _waterBatchCount = 0;
            while (_build.Cursor < totalBrickSlots
                   && _waterBatchCount < WaterBrickMeshBatchJob.MaxBatch)
            {
                int linear = _build.Cursor++;
                int x = linear % BricksPerAxis;
                int y = (linear / BricksPerAxis) % BricksPerAxis;
                int z = linear / (BricksPerAxis * BricksPerAxis);
                int3 worldBrick = _build.Coordinate * BricksPerAxis + new int3(x, y, z);
                if (set.Contains(worldBrick)
                    && SnapshotWaterBrick(storage, ref cachedRegion, worldBrick,
                                          _waterBatchCount))
                    _waterBatchCount++;

                if (_build.Cursor < totalBrickSlots
                    && Time.realtimeSinceStartupAsDouble >= deadline)
                    break;
            }

            if (_waterBatchCount > 0)
            {
                _waterMeshOverflow[0] = 0;
                _waterMeshJobHandle = new WaterBrickMeshBatchJob
                {
                    BrickBaseVoxels = _waterBatchBrickBases,
                    SnapshotMaterials = _waterBatchMaterials,
                    BatchCount = _waterBatchCount,
                    VoxelSize = voxelSize,
                    MaskScratch = _waterMeshMask,
                    Vertices = _vertices,
                    Indices = _indices,
                    Overflow = _waterMeshOverflow,
                }.Schedule();
                _waterMeshJobScheduled = true;
                // Never spin on freshly scheduled mesh work.
                return false;
            }

            return _build.Cursor >= totalBrickSlots;
        }

        private bool SnapshotWaterBrick(IRegionReadSource storage,
                                        ref RegionReadView cachedRegion,
                                        int3 worldBrick, int batchIndex)
        {
            if (!TryLoadBrickMaterials(storage, worldBrick, ref cachedRegion)
                || !LoadedBrickContainsWater())
                return false;

            int snapshotBase = batchIndex * WaterBrickMeshBatchJob.SnapshotStride;
            NativeArray<byte>.Copy(_brickMaterials, 0, _waterBatchMaterials,
                                   snapshotBase, VoxelReadGrid.VoxelsPerBlock);
            int3 brickBaseVoxel = worldBrick * E;
            _waterBatchBrickBases[batchIndex] = brickBaseVoxel;

            for (int axis = 0; axis < 3; axis++)
            {
                int axisA = (axis + 1) % 3;
                int axisB = (axis + 2) % 3;
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    int face = axis * 2 + (sign > 0 ? 1 : 0);
                    int faceBase = snapshotBase + WaterBrickMeshBatchJob.VoxelsPerBrick
                                 + face * WaterBrickMeshBatchJob.FaceArea;
                    for (int b = 0; b < E; b++)
                    for (int a = 0; a < E; a++)
                    {
                        int3 local = int3.zero;
                        local[axis] = sign < 0 ? -1 : E;
                        local[axisA] = a;
                        local[axisB] = b;
                        byte material = TryReadWorldMaterial(
                            storage, ref cachedRegion, brickBaseVoxel + local, out byte sampled)
                            ? sampled : VoxelGrid.MaterialEmpty;
                        _waterBatchMaterials[faceBase + a + b * E] = material;
                    }
                }
            }
            return true;
        }

'''
s = s[:start] + step + s[end:]

# Reset cannot touch job-owned lists.
s = once(s,
'''        private void ResetBuildOutput()
        {
            _build = default;
            _vertices.Clear();
            _indices.Clear();
        }''',
'''        private void ResetBuildOutput()
        {
            if (_waterMeshJobScheduled)
                throw new InvalidOperationException(
                    "Water build output reset while Burst mesh job is still running.");
            _build = default;
            _discardBuildAfterMeshJob = false;
            _waterBatchCount = 0;
            _vertices.Clear();
            _indices.Clear();
        }''', 'water reset job invariant')

# Delete old frame-thread mesh emission methods, preserving TryLoadBrickMaterials onward.
start = s.index('        private void EmitBrick(')
end = s.index('        private bool TryLoadBrickMaterials(', start)
s = s[:start] + s[end:]

# RemoveWaterChunk defers output reset if worker still owns lists.
s = once(s,
'''            if (_build.Active && _build.Coordinate.Equals(chunk))
            {
                if (_entries.TryGetValue(chunk, out Entry pending)) pending.CancelUpload();
                ResetBuildOutput();
            }''',
'''            if (_build.Active && _build.Coordinate.Equals(chunk))
            {
                if (_entries.TryGetValue(chunk, out Entry pending)) pending.CancelUpload();
                if (_waterMeshJobScheduled && !_waterMeshJobHandle.IsCompleted)
                {
                    _discardBuildAfterMeshJob = true;
                    return;
                }
                if (_waterMeshJobScheduled)
                {
                    _waterMeshJobHandle.Complete();
                    _waterMeshJobScheduled = false;
                }
                ResetBuildOutput();
            }''', 'water removal defers running mesh job')

# Dispose is allowed to synchronize and owns native scratch lifecycle.
s = once(s,
'''        public void Dispose()
        {
            foreach (Entry entry in _entries.Values) entry.Dispose();''',
'''        public void Dispose()
        {
            if (_waterMeshJobScheduled)
            {
                _waterMeshJobHandle.Complete();
                _waterMeshJobScheduled = false;
            }
            foreach (Entry entry in _entries.Values) entry.Dispose();''', 'water teardown job completion')
s = once(s,
'''            if (_vertices.IsCreated) _vertices.Dispose();
            if (_indices.IsCreated) _indices.Dispose();
            _geometryArena.Dispose();''',
'''            if (_vertices.IsCreated) _vertices.Dispose();
            if (_indices.IsCreated) _indices.Dispose();
            if (_waterBatchBrickBases.IsCreated) _waterBatchBrickBases.Dispose();
            if (_waterBatchMaterials.IsCreated) _waterBatchMaterials.Dispose();
            if (_waterMeshMask.IsCreated) _waterMeshMask.Dispose();
            if (_waterMeshOverflow.IsCreated) _waterMeshOverflow.Dispose();
            _geometryArena.Dispose();''', 'water async scratch disposal')
path.write_text(s)

# -----------------------------------------------------------------------------
# Tests/doc.
# -----------------------------------------------------------------------------
test_path = Path('Assets/Tests/EditMode/GeometryPipelineArchitectureTests.cs')
t = test_path.read_text()
if 'WaterGreedyMeshEmissionRunsInBurst' not in t:
    insert = r'''

        [Test]
        public void WaterGreedyMeshEmissionRunsInBurst()
        {
            string water = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "CpuWaterSurfaceChunkCache.cs"));
            string job = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "WaterBrickMeshBatchJob.cs"));
            StringAssert.Contains("new WaterBrickMeshBatchJob", water);
            StringAssert.Contains("_waterMeshJobHandle.IsCompleted", water);
            StringAssert.Contains("SnapshotWaterBrick", water);
            StringAssert.DoesNotContain("private void EmitBrick", water);
            StringAssert.DoesNotContain("private void MergeMask", water);
            StringAssert.DoesNotContain("private void EmitQuad", water);
            StringAssert.Contains("[BurstCompile]", job);
            StringAssert.Contains("AddNoResize", job);
            StringAssert.Contains("SnapshotStride", job);
        }
'''
    marker = '\n    }\n}'
    pos = t.rfind(marker)
    if pos < 0: raise SystemExit('architecture test closing marker missing')
    t = t[:pos] + insert + t[pos:]
test_path.write_text(t)

doc_path = Path('docs/ASYNC_GEOMETRY_PIPELINE.md')
d = doc_path.read_text()
d = d.replace('  - [ ] Move water extraction itself to owned immutable snapshot + Burst jobs.\n',
              '  - [x] Move water greedy mesh emission to owned immutable material snapshots + Burst jobs.\n', 1)
d = d.replace('- [ ] Apply the same async snapshot/result/publication contract to water geometry.\n',
              '- [x] Apply the same async snapshot/result/publication contract to water geometry.\n', 1)
doc_path.write_text(d)

water = path.read_text()
assert 'private void EmitBrick' not in water
assert 'private void MergeMask' not in water
assert 'private void EmitQuad' not in water
assert 'new WaterBrickMeshBatchJob' in water
assert '_waterMeshJobHandle.IsCompleted' in water
assert 'NativeList<SmoothSurfaceVertex>(ArenaVertexCapacity' in water
