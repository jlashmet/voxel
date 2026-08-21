using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Rendering.Runtime.SurfaceExtraction
{
    /// <summary>
    /// Derived raster mesh for authoritative water (material 11) and cascade (material 16) voxels.
    /// Water remains presentation-only derived geometry; authoritative voxel memory is read through
    /// Storage.Api and no physical pool/region representation crosses into Rendering.
    /// </summary>
    public sealed class CpuWaterSurfaceChunkCache : IDisposable
    {
        private const int E = VoxelReadGrid.BlockEdge;
        private const int BricksPerAxis = 16;
        private const int ChunkShift = 4;
        private const int VoxelsPerAxis = BricksPerAxis * E;
        private const int BuildSelectionCandidatesPerPrepare = 32;
        private const int ResidencyChecksPerPrepare = 16;
        private const int RegionInvalidationCandidatesPerPrepare = 64;
        private const int WaterChunksPerRegion = VoxelGrid.RegionVoxelEdge / VoxelsPerAxis;
        private const int ArenaVertexCapacity = 256 * 1024;
        private const int ArenaIndexCapacity = 768 * 1024;
        public const int ArenaDrawCapacity = 2048;
        private const uint FullyLitOcclusion = 0x0000FF00u;

        private static readonly int s_SurfaceVertices = Shader.PropertyToID("_SurfaceVertices");
        private static readonly int s_SurfaceIndices = Shader.PropertyToID("_SurfaceIndices");
        private static readonly int s_SurfaceIndexBase = Shader.PropertyToID("_SurfaceIndexBase");
        private static readonly int[] s_Strides = { 1, E, E * E };

        public sealed class Entry : IDisposable
        {
            public int3 Coordinate { get; private set; }
            private readonly SurfaceGeometryArena _arena;
            private SurfaceGeometryLease _liveLease;
            private SurfaceGeometryLease _stagingLease;
            private int _stagingVertexCursor;
            private int _stagingIndexCursor;
            public ComputeBuffer Vertices => _arena.Vertices;
            public ComputeBuffer Indices => _arena.Indices;
            public ComputeBuffer Args => _arena.Args;
            public bool Ready;
            public int IndexCount;
            public long GpuBytes { get; private set; }
            public ulong SourceVersion { get; internal set; }
            internal bool WaitingForArena { get; private set; }

            internal Entry(int3 coordinate, SurfaceGeometryArena arena)
            {
                Coordinate = coordinate;
                _arena = arena ?? throw new ArgumentNullException(nameof(arena));
            }

            internal void Reinitialize(int3 coordinate)
            {
                if (Ready || _liveLease.IsValid || _stagingLease.IsValid)
                    throw new InvalidOperationException(
                        "A water entry must release its arena leases before reuse.");
                Coordinate = coordinate;
                IndexCount = 0;
                GpuBytes = 0;
                SourceVersion = 0;
                WaitingForArena = false;
                _stagingVertexCursor = 0;
                _stagingIndexCursor = 0;
            }

            internal int RemainingUploadBytes(int vertexCount, int indexCount)
            {
                int verticesRemaining = math.max(0, vertexCount - _stagingVertexCursor);
                int indicesRemaining = math.max(0, indexCount - _stagingIndexCursor);
                return verticesRemaining * SmoothSurfaceVertex.Stride
                     + indicesRemaining * sizeof(uint)
                     + SurfaceGeometryArena.ArgsWordsPerDraw * sizeof(uint);
            }

            internal bool AdvanceUpload(NativeList<SmoothSurfaceVertex> vertices,
                                        NativeList<uint> indices,
                                        int byteBudget,
                                        out int uploadedBytes)
            {
                uploadedBytes = 0;
                if (byteBudget <= 0 || !EnsureUploadStaging(vertices.Length, indices.Length))
                    return false;

                int remainingBudget = byteBudget;
                int vertexRemaining = vertices.Length - _stagingVertexCursor;
                if (vertexRemaining > 0 && remainingBudget >= SmoothSurfaceVertex.Stride)
                {
                    int count = math.min(vertexRemaining,
                        remainingBudget / SmoothSurfaceVertex.Stride);
                    _arena.UploadVertices(vertices.AsArray(), _stagingVertexCursor,
                                          in _stagingLease, count);
                    int bytes = count * SmoothSurfaceVertex.Stride;
                    _stagingVertexCursor += count;
                    remainingBudget -= bytes;
                    uploadedBytes += bytes;
                }

                int indexRemaining = indices.Length - _stagingIndexCursor;
                if (_stagingVertexCursor == vertices.Length && indexRemaining > 0
                    && remainingBudget >= sizeof(uint))
                {
                    int count = math.min(indexRemaining, remainingBudget / sizeof(uint));
                    _arena.UploadIndices(indices.AsArray(), _stagingIndexCursor,
                                         in _stagingLease, count);
                    int bytes = count * sizeof(uint);
                    _stagingIndexCursor += count;
                    remainingBudget -= bytes;
                    uploadedBytes += bytes;
                }

                const int argsBytes = SurfaceGeometryArena.ArgsWordsPerDraw * sizeof(uint);
                if (_stagingVertexCursor != vertices.Length
                    || _stagingIndexCursor != indices.Length
                    || remainingBudget < argsBytes)
                    return false;

                _arena.UploadArgs((uint)indices.Length, in _stagingLease);
                uploadedBytes += argsBytes;

                SurfaceGeometryLease previous = _liveLease;
                _liveLease = _stagingLease;
                _stagingLease = default;
                _stagingVertexCursor = 0;
                _stagingIndexCursor = 0;
                IndexCount = indices.Length;
                GpuBytes = _arena.ReservedBytes(in _liveLease);
                Ready = true;
                WaitingForArena = false;
                _arena.Release(in previous);
                return true;
            }

            private bool EnsureUploadStaging(int vertexCount, int indexCount)
            {
                if (_stagingLease.IsValid) return true;
                if (!_arena.TryAcquire(vertexCount, indexCount, out _stagingLease))
                {
                    WaitingForArena = true;
                    return false;
                }
                WaitingForArena = false;
                _stagingVertexCursor = 0;
                _stagingIndexCursor = 0;
                return true;
            }

            internal void CancelUpload()
            {
                _arena.Release(in _stagingLease);
                _stagingLease = default;
                _stagingVertexCursor = 0;
                _stagingIndexCursor = 0;
                WaitingForArena = false;
            }

            public Bounds WorldBounds(float voxelSize)
            {
                float size = VoxelsPerAxis * voxelSize;
                Vector3 min = new Vector3(Coordinate.x, Coordinate.y, Coordinate.z) * size;
                return new Bounds(min + Vector3.one * (size * 0.5f), Vector3.one * size);
            }

            public void Draw(CommandBuffer commandBuffer, Material material,
                             MaterialPropertyBlock properties)
            {
                if (!Ready || IndexCount == 0) return;

                properties.SetBuffer(s_SurfaceVertices, _arena.Vertices);
                properties.SetBuffer(s_SurfaceIndices, _arena.Indices);
                properties.SetInt(s_SurfaceIndexBase, _liveLease.IndexStart);
                commandBuffer.DrawProceduralIndirect(Matrix4x4.identity, material, 0,
                    MeshTopology.Triangles, _arena.Args,
                    _liveLease.ArgsWordStart * sizeof(uint), properties);
            }

            public void Dispose()
            {
                CancelUpload();
                _arena.Release(in _liveLease);
                _liveLease = default;
                Ready = false;
                IndexCount = 0;
                GpuBytes = 0;
            }
        }

        private struct BuildState
        {
            public bool Active;
            public int3 Coordinate;
            public int Cursor;
            public ulong SourceVersion;
            public bool PendingPublication;
        }

        private readonly Dictionary<int3, HashSet<int3>> _waterBricks = new();
        private readonly Dictionary<int3, Entry> _entries = new();
        private readonly Stack<Entry> _entryPool = new();

        private readonly HashSet<int3> _dirty = new();
        private readonly Queue<int3> _dirtyQueue = new();
        private readonly HashSet<int3> _queuedDirty = new();
        private readonly Dictionary<int3, ulong> _desiredVersions = new();
        private ulong _versionCounter;

        private readonly Queue<int3> _residencyQueue = new();
        private readonly HashSet<int3> _queuedResidency = new();

        private readonly Queue<int3> _regionInvalidationQueue = new();
        private readonly HashSet<int3> _queuedRegionInvalidations = new();
        private readonly HashSet<int3> _rescanRegionInvalidations = new();
        private bool _hasActiveRegionInvalidation;
        private int3 _activeRegionInvalidation;
        private int3 _activeRegionMinChunk;
        private int _activeRegionCandidateCursor;

        private readonly List<Entry> _visible = new();
        private readonly Plane[] _frustumPlanes = new Plane[6];
        private readonly SurfaceGeometryArena _geometryArena;
        private NativeList<SmoothSurfaceVertex> _vertices;
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
        private BuildState _build;

        public CpuWaterSurfaceChunkCache()
        {
            _geometryArena = new SurfaceGeometryArena(
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
                                                      NativeArrayOptions.ClearMemory);
        }

        public int ResidentCount => _entries.Count;
        public int DirtyCount => _dirty.Count + (_build.Active ? 1 : 0);
        public ulong CompletedBuildCount { get; private set; }
        public ulong StaleBuildCount { get; private set; }
        public ulong UploadedGeometryBytes { get; private set; }
        private ulong _framePathBlockingCompletionViolations;
        public ulong FramePathBlockingCompletionViolations => _framePathBlockingCompletionViolations;
        public int RunningJobCount => _waterMeshJobScheduled ? 1 : 0;
        public long ResidentGpuBytes
        {
            get
            {
                long total = 0;
                foreach (Entry entry in _entries.Values) total += entry.GpuBytes;
                return total;
            }
        }
        public IReadOnlyList<Entry> Visible => _visible;
        public int PendingUploadCount => _build.Active && _build.PendingPublication ? 1 : 0;
        public int PendingUploadBytes
        {
            get
            {
                if (!_build.Active || !_build.PendingPublication) return 0;
                if (!_entries.TryGetValue(_build.Coordinate, out Entry entry))
                    return _vertices.Length * SmoothSurfaceVertex.Stride
                         + _indices.Length * sizeof(uint)
                         + SurfaceGeometryArena.ArgsWordsPerDraw * sizeof(uint);
                return entry.RemainingUploadBytes(_vertices.Length, _indices.Length);
            }
        }
        public ulong ArenaAllocationFailures => _geometryArena.AllocationFailureCount;

        public void InvalidateSurfaceBricks(IRegionReadSource storage,
                                            IReadOnlyList<int3> worldBricks)
        {
            if (storage == null || worldBricks == null) return;

            RegionReadView cachedRegion = default;
            for (int i = 0; i < worldBricks.Count; i++)
            {
                int3 worldBrick = worldBricks[i];
                int3 chunk = WorldBrickChunk(worldBrick);
                bool containsWater = TryLoadBrickMaterials(storage, worldBrick, ref cachedRegion)
                                  && LoadedBrickContainsWater();

                if (containsWater)
                {
                    if (!_waterBricks.TryGetValue(chunk, out HashSet<int3> set))
                    {
                        set = new HashSet<int3>();
                        _waterBricks.Add(chunk, set);
                        TrackResidentChunk(chunk);
                    }
                    if (set.Add(worldBrick)) Invalidate(chunk);
                }
                else if (_waterBricks.TryGetValue(chunk, out HashSet<int3> existing)
                         && existing.Remove(worldBrick))
                {
                    Invalidate(chunk);
                }

                int rx = worldBrick.x & (BricksPerAxis - 1);
                int ry = worldBrick.y & (BricksPerAxis - 1);
                int rz = worldBrick.z & (BricksPerAxis - 1);
                if (rx == 0) MarkKnownDirty(chunk + new int3(-1, 0, 0));
                if (rx == BricksPerAxis - 1) MarkKnownDirty(chunk + new int3(1, 0, 0));
                if (ry == 0) MarkKnownDirty(chunk + new int3(0, -1, 0));
                if (ry == BricksPerAxis - 1) MarkKnownDirty(chunk + new int3(0, 1, 0));
                if (rz == 0) MarkKnownDirty(chunk + new int3(0, 0, -1));
                if (rz == BricksPerAxis - 1) MarkKnownDirty(chunk + new int3(0, 0, 1));
            }
        }

        public void InvalidateDirtyRegions(HashSet<int3> dirtyRegions)
        {
            if (dirtyRegions != null)
            {
                foreach (int3 region in dirtyRegions)
                {
                    if (_hasActiveRegionInvalidation && region.Equals(_activeRegionInvalidation))
                    {
                        _rescanRegionInvalidations.Add(region);
                        continue;
                    }
                    if (_queuedRegionInvalidations.Add(region))
                        _regionInvalidationQueue.Enqueue(region);
                }
            }
            StepRegionInvalidation();
        }

        private void StepRegionInvalidation()
        {
            int remaining = RegionInvalidationCandidatesPerPrepare;
            int span = WaterChunksPerRegion * 3;
            int total = span * span * span;
            while (remaining > 0)
            {
                if (!_hasActiveRegionInvalidation)
                {
                    if (_regionInvalidationQueue.Count == 0) return;
                    _activeRegionInvalidation = _regionInvalidationQueue.Dequeue();
                    _activeRegionMinChunk = (_activeRegionInvalidation - 1)
                                          * WaterChunksPerRegion;
                    _activeRegionCandidateCursor = 0;
                    _hasActiveRegionInvalidation = true;
                }

                while (remaining > 0 && _activeRegionCandidateCursor < total)
                {
                    int linear = _activeRegionCandidateCursor++;
                    int x = linear % span;
                    int y = (linear / span) % span;
                    int z = linear / (span * span);
                    int3 chunk = _activeRegionMinChunk + new int3(x, y, z);
                    remaining--;
                    if (_waterBricks.ContainsKey(chunk)) Invalidate(chunk);
                }

                if (_activeRegionCandidateCursor < total) return;
                int3 completed = _activeRegionInvalidation;
                bool rescan = _rescanRegionInvalidations.Remove(completed);
                _queuedRegionInvalidations.Remove(completed);
                _hasActiveRegionInvalidation = false;
                _activeRegionCandidateCursor = 0;
                if (rescan && _queuedRegionInvalidations.Add(completed))
                    _regionInvalidationQueue.Enqueue(completed);
            }
        }

        public void Prepare(IRegionReadSource storage, Camera camera,
                            float voxelSize, double budgetMs = 0.15)
        {
            if (storage == null) return;
            StepResidencyPrune(storage);
            if (camera == null || _build.PendingPublication
                || (_dirty.Count == 0 && !_build.Active)) return;

            double deadline = Time.realtimeSinceStartupAsDouble
                            + math.max(0.0, budgetMs) * 0.001;
            while (Time.realtimeSinceStartupAsDouble < deadline)
            {
                if (!_build.Active
                    && !BeginNearestBuild(camera.transform.position, voxelSize, deadline)) break;
                if (!StepBuild(storage, voxelSize, deadline)) break;
                FinishCpuBuild();
                if (_build.PendingPublication) break;
            }
        }

        public bool TryPublishPending(int byteBudget, out int uploadedBytes)
        {
            uploadedBytes = 0;
            if (!_build.Active || !_build.PendingPublication || byteBudget <= 0) return false;

            if (_desiredVersions.TryGetValue(_build.Coordinate, out ulong desired)
                && desired > _build.SourceVersion)
            {
                StaleBuildCount++;
                if (_entries.TryGetValue(_build.Coordinate, out Entry stale)) stale.CancelUpload();
                ResetBuildOutput();
                return false;
            }

            if (_indices.Length == 0)
            {
                if (_entries.TryGetValue(_build.Coordinate, out Entry empty)) empty.Dispose();
                _entries.Remove(_build.Coordinate);
                _desiredVersions.Remove(_build.Coordinate);
                CompletedBuildCount++;
                ResetBuildOutput();
                return true;
            }

            if (!_entries.TryGetValue(_build.Coordinate, out Entry entry))
            {
                entry = AcquireEntry(_build.Coordinate);
                _entries.Add(_build.Coordinate, entry);
            }

            bool complete = entry.AdvanceUpload(_vertices, _indices, byteBudget, out uploadedBytes);
            UploadedGeometryBytes += (ulong)uploadedBytes;
            if (!complete) return false;

            entry.SourceVersion = _build.SourceVersion;
            _desiredVersions.Remove(_build.Coordinate);
            CompletedBuildCount++;
            ResetBuildOutput();
            return true;
        }

        public IReadOnlyList<Entry> CollectVisible(Camera camera, float voxelSize)
        {
            _visible.Clear();
            if (camera == null) return _visible;

            GeometryUtility.CalculateFrustumPlanes(camera, _frustumPlanes);
            foreach (Entry entry in _entries.Values)
            {
                if (!entry.Ready || entry.IndexCount == 0) continue;
                if (!GeometryUtility.TestPlanesAABB(_frustumPlanes, entry.WorldBounds(voxelSize)))
                    continue;
                _visible.Add(entry);
            }
            return _visible;
        }

        private bool BeginNearestBuild(Vector3 cameraWorldPosition, float voxelSize,
                                       double deadline)
        {
            if (_dirty.Count == 0 || _dirtyQueue.Count == 0
                || Time.realtimeSinceStartupAsDouble >= deadline)
                return false;

            int3 best = default;
            bool hasBest = false;
            float bestDistance = float.PositiveInfinity;
            float chunkMetres = VoxelsPerAxis * voxelSize;
            int candidates = math.min(BuildSelectionCandidatesPerPrepare, _dirtyQueue.Count);
            for (int i = 0; i < candidates; i++)
            {
                int3 candidate = _dirtyQueue.Dequeue();
                _queuedDirty.Remove(candidate);
                if (!_dirty.Contains(candidate)) continue;

                if (!_waterBricks.TryGetValue(candidate, out HashSet<int3> set) || set.Count == 0)
                {
                    _dirty.Remove(candidate);
                    RemoveWaterChunk(candidate);
                    continue;
                }

                Vector3 centre = (new Vector3(candidate.x, candidate.y, candidate.z)
                                + Vector3.one * 0.5f) * chunkMetres;
                float distance = (centre - cameraWorldPosition).sqrMagnitude;
                if (!hasBest || distance < bestDistance)
                {
                    if (hasBest) RequeueDirty(best);
                    best = candidate;
                    bestDistance = distance;
                    hasBest = true;
                }
                else
                {
                    RequeueDirty(candidate);
                }

                if (Time.realtimeSinceStartupAsDouble >= deadline) break;
            }

            if (!hasBest) return false;
            _dirty.Remove(best);
            _vertices.Clear();
            _indices.Clear();
            _build = new BuildState
            {
                Active = true,
                Coordinate = best,
                Cursor = 0,
                SourceVersion = _desiredVersions.TryGetValue(best, out ulong version)
                    ? version : 0
            };
            return true;
        }

        private bool StepBuild(IRegionReadSource storage, float voxelSize, double deadline)
        {
            if (_waterMeshJobScheduled)
            {
                if (!_waterMeshJobHandle.IsCompleted) return false;
                if (!GeometryFrameJobCompletionGuard.TryCompleteReady(
                        _waterMeshJobHandle, ref _framePathBlockingCompletionViolations))
                    return false;
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

        private void FinishCpuBuild()
        {
            if (_desiredVersions.TryGetValue(_build.Coordinate, out ulong desired)
                && desired > _build.SourceVersion)
            {
                StaleBuildCount++;
                ResetBuildOutput();
                return;
            }
            _build.PendingPublication = true;
        }

        private void ResetBuildOutput()
        {
            if (_waterMeshJobScheduled)
                throw new InvalidOperationException(
                    "Water build output reset while Burst mesh job is still running.");
            _build = default;
            _discardBuildAfterMeshJob = false;
            _waterBatchCount = 0;
            _vertices.Clear();
            _indices.Clear();
        }

        private bool TryLoadBrickMaterials(IRegionReadSource storage, int3 worldBrick,
                                           ref RegionReadView cachedRegion)
        {
            if (!cachedRegion.IsCreated || !cachedRegion.ContainsWorldBlock(worldBrick))
            {
                if (!storage.TryAcquireRegionContainingBlock(worldBrick, out cachedRegion))
                {
                    cachedRegion = default;
                    return false;
                }
            }

            return cachedRegion.TryCopyWorldBlock(
                worldBrick, _brickMaterials, _surfaceScratch, _boundaryScratch, 0);
        }

        private bool LoadedBrickContainsWater()
        {
            for (int i = 0; i < VoxelReadGrid.VoxelsPerBlock; i++)
                if (IsWater(_brickMaterials[i])) return true;
            return false;
        }

        private static bool TryReadWorldMaterial(IRegionReadSource storage,
                                                 ref RegionReadView cachedRegion,
                                                 int3 worldVoxel,
                                                 out byte material)
        {
            int3 regionCoord = worldVoxel >> VoxelGrid.RegionVoxelEdgeLog2;
            if (!cachedRegion.IsCreated || math.any(cachedRegion.RegionCoord != regionCoord))
            {
                if (!storage.TryAcquireRegion(regionCoord, out cachedRegion))
                {
                    cachedRegion = default;
                    material = VoxelGrid.MaterialEmpty;
                    return false;
                }
            }

            int3 localVoxel = worldVoxel - (regionCoord << VoxelGrid.RegionVoxelEdgeLog2);
            if (!cachedRegion.TryReadCell(localVoxel, out VoxelCell cell))
            {
                material = VoxelGrid.MaterialEmpty;
                return false;
            }
            material = cell.BaseMaterialId;
            return true;
        }

        private static bool IsWater(byte material) => material == 11 || material == 16;

        private void MarkKnownDirty(int3 chunk)
        {
            if (_waterBricks.ContainsKey(chunk)) Invalidate(chunk);
        }

        private void Invalidate(int3 chunk)
        {
            _desiredVersions[chunk] = ++_versionCounter;
            MarkDirty(chunk);
        }

        private void MarkDirty(int3 chunk)
        {
            _dirty.Add(chunk);
            RequeueDirty(chunk);
        }

        private void RequeueDirty(int3 chunk)
        {
            if (!_dirty.Contains(chunk) || !_queuedDirty.Add(chunk)) return;
            _dirtyQueue.Enqueue(chunk);
        }

        private static int3 WorldBrickChunk(int3 worldBrick) =>
            new(worldBrick.x >> ChunkShift, worldBrick.y >> ChunkShift, worldBrick.z >> ChunkShift);

        private static int3 ChunkRegion(int3 chunk) =>
            chunk >> (VoxelReadGrid.BlocksPerRegionEdgeLog2 - ChunkShift);

        private void TrackResidentChunk(int3 chunk)
        {
            if (!_queuedResidency.Add(chunk)) return;
            _residencyQueue.Enqueue(chunk);
        }

        private void StepResidencyPrune(IRegionReadSource storage)
        {
            int checks = math.min(ResidencyChecksPerPrepare, _residencyQueue.Count);
            for (int i = 0; i < checks; i++)
            {
                int3 chunk = _residencyQueue.Dequeue();
                _queuedResidency.Remove(chunk);
                if (!_waterBricks.ContainsKey(chunk)) continue;
                if (storage.IsRegionResident(ChunkRegion(chunk)))
                {
                    TrackResidentChunk(chunk);
                    continue;
                }
                RemoveWaterChunk(chunk);
            }
        }

        private void RemoveWaterChunk(int3 chunk)
        {
            _waterBricks.Remove(chunk);
            _dirty.Remove(chunk);
            _queuedDirty.Remove(chunk);
            _queuedResidency.Remove(chunk);
            _desiredVersions.Remove(chunk);
            if (_entries.TryGetValue(chunk, out Entry entry))
            {
                _entries.Remove(chunk);
                ReleaseEntry(entry);
            }
            if (_build.Active && _build.Coordinate.Equals(chunk))
            {
                if (_entries.TryGetValue(chunk, out Entry pending)) pending.CancelUpload();
                if (_waterMeshJobScheduled && !_waterMeshJobHandle.IsCompleted)
                {
                    _discardBuildAfterMeshJob = true;
                    return;
                }
                if (_waterMeshJobScheduled)
                {
                    if (!GeometryFrameJobCompletionGuard.TryCompleteReady(
                            _waterMeshJobHandle, ref _framePathBlockingCompletionViolations))
                    {
                        _discardBuildAfterMeshJob = true;
                        return;
                    }
                    _waterMeshJobScheduled = false;
                }
                ResetBuildOutput();
            }
        }

        private Entry AcquireEntry(int3 coordinate)
        {
            if (_entryPool.Count == 0) return new Entry(coordinate, _geometryArena);
            Entry entry = _entryPool.Pop();
            entry.Reinitialize(coordinate);
            return entry;
        }

        private void ReleaseEntry(Entry entry)
        {
            if (entry == null) return;
            entry.Dispose();
            _entryPool.Push(entry);
        }

        internal bool TryEvictOneForArenaPressure(Camera camera, float voxelSize)
        {
            if (_entries.Count == 0) return false;
            if (camera != null) GeometryUtility.CalculateFrustumPlanes(camera, _frustumPlanes);
            Vector3 cameraPosition = camera != null ? camera.transform.position : Vector3.zero;
            int3 victim = default;
            float farthest = -1f;
            float chunkMetres = VoxelsPerAxis * voxelSize;
            foreach (var pair in _entries)
            {
                if (_build.Active && pair.Key.Equals(_build.Coordinate)) continue;
                if (camera != null
                    && GeometryUtility.TestPlanesAABB(_frustumPlanes, pair.Value.WorldBounds(voxelSize)))
                    continue;
                Vector3 centre = (new Vector3(pair.Key.x, pair.Key.y, pair.Key.z)
                                + Vector3.one * 0.5f) * chunkMetres;
                float distance = (centre - cameraPosition).sqrMagnitude;
                if (distance <= farthest) continue;
                farthest = distance;
                victim = pair.Key;
            }
            if (farthest < 0f) return false;
            if (!_entries.TryGetValue(victim, out Entry entry)) return false;

            // Arena pressure is publication backpressure, not authoritative water eviction.
            // Keep the discovered brick set + residency record so the chunk is rebuilt later.
            _entries.Remove(victim);
            ReleaseEntry(entry);
            MarkDirty(victim);
            return true;
        }

        public void Dispose()
        {
            if (_waterMeshJobScheduled)
            {
                _waterMeshJobHandle.Complete();
                _waterMeshJobScheduled = false;
            }
            foreach (Entry entry in _entries.Values) entry.Dispose();
            foreach (Entry entry in _entryPool) entry.Dispose();
            _entries.Clear();
            _entryPool.Clear();
            _waterBricks.Clear();
            _dirty.Clear();
            _dirtyQueue.Clear();
            _queuedDirty.Clear();
            _residencyQueue.Clear();
            _queuedResidency.Clear();
            _regionInvalidationQueue.Clear();
            _queuedRegionInvalidations.Clear();
            _rescanRegionInvalidations.Clear();
            _desiredVersions.Clear();
            _visible.Clear();
            if (_vertices.IsCreated) _vertices.Dispose();
            if (_indices.IsCreated) _indices.Dispose();
            if (_waterBatchBrickBases.IsCreated) _waterBatchBrickBases.Dispose();
            if (_waterBatchMaterials.IsCreated) _waterBatchMaterials.Dispose();
            if (_waterMeshMask.IsCreated) _waterMeshMask.Dispose();
            if (_waterMeshOverflow.IsCreated) _waterMeshOverflow.Dispose();
            _geometryArena.Dispose();
            if (_brickMaterials.IsCreated) _brickMaterials.Dispose();
            if (_surfaceScratch.IsCreated) _surfaceScratch.Dispose();
            if (_boundaryScratch.IsCreated) _boundaryScratch.Dispose();
            _build = default;
        }
    }
}
