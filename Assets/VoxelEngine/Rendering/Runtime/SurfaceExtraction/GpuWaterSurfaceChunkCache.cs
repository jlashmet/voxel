using System;
using System.Collections.Generic;
using Unity.Collections;
using VoxelEngine.Rendering.Runtime.GpuVoxel;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Rendering.Runtime.SurfaceExtraction
{
    /// <summary>
    /// GPU-derived raster mesh for water materials registered by the presentation catalogue.
    /// Water remains presentation-only derived geometry; authoritative voxel memory is read through
    /// Storage.Api and no physical pool/region representation crosses into Rendering.
    /// </summary>
    public sealed class GpuWaterSurfaceChunkCache : IDisposable
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
        private const int SnapshotBytes = GpuWaterSurfaceMesher.SnapshotWords * sizeof(uint);

        public sealed class Entry : IDisposable
        {
            private readonly GpuWaterSurfaceChunkCache _owner;
            public int3 Coordinate { get; private set; }
            internal int Handle { get; private set; } = -1;
            public bool Ready { get; internal set; }
            public ulong SourceVersion { get; internal set; }

            internal Entry(int3 coordinate, GpuWaterSurfaceChunkCache owner)
            { Coordinate = coordinate; _owner = owner; }

            internal void Reinitialize(int3 coordinate)
            {
                if (Handle >= 0) throw new InvalidOperationException("Water handle still owned.");
                Coordinate = coordinate;
                Ready = false;
                SourceVersion = 0;
            }

            internal bool AcquireHandle()
            {
                if (Handle >= 0) return true;
                if (!_owner._geometryArena.TryAcquireHandle(out int handle)) return false;
                Handle = handle;
                return true;
            }

            public Bounds WorldBounds(float voxelSize)
            {
                float size = VoxelsPerAxis * voxelSize;
                Vector3 min = new Vector3(Coordinate.x, Coordinate.y, Coordinate.z) * size;
                // Canonical impact spray can extend outside the supporting water cells.
                return new Bounds(min + Vector3.one * (size * 0.5f),
                    Vector3.one * (size + 12f * voxelSize));
            }

            public void Draw(CommandBuffer commands, Material material, MaterialPropertyBlock properties)
            {
                if (!Ready || Handle < 0) return;
                _owner.BindDraw(properties);
                properties.SetInteger("_WaterDrawHandle", Handle);
                for (int pass = 0; pass < 2; pass++)
                    commands.DrawProceduralIndirect(Matrix4x4.identity, material, pass,
                        MeshTopology.Triangles, _owner._drawArgs, Handle * 16, properties);
            }

            public void Dispose()
            {
                if (Handle >= 0)
                {
                    _owner._geometryArena.QueueRelease(Handle, ++_owner._versionCounter);
                    Handle = -1;
                }
                Ready = false;
            }
        }

        private struct BuildState
        {
            public bool Active;
            public int3 Coordinate;
            public int Cursor;
            public ulong SourceVersion;
            public bool PendingPublication;
            public int BrickCount;
            public int WriteCursor;
            public bool Allocated;
            public int Handle;
            public float VoxelSize;
            public uint WaterMask;
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
        private readonly GpuSurfacePageArena _geometryArena;
        private readonly ComputeShader _mesherShader;
        private readonly ComputeShader _arenaShader;
        private readonly ComputeShader _drawShader;
        private readonly ComputeBuffer _origins;
        private readonly ComputeBuffer _materials;
        private readonly ComputeBuffer _counters;
        private readonly ComputeBuffer _descriptors;
        private readonly ComputeBuffer _outcomes;
        private readonly ComputeBuffer _drawArgs;
        private readonly GpuSubmissionLifetime _lifetime;
        private readonly int4[] _originStaging = new int4[GpuWaterSurfaceMesher.MaximumBricksPerSlice];
        private readonly uint[] _materialStaging = new uint[GpuWaterSurfaceMesher.MaximumBricksPerSlice * GpuWaterSurfaceMesher.SnapshotWords];
        private readonly GpuSurfaceExtractor.BatchChunkDescriptor[] _descriptorStaging = new GpuSurfaceExtractor.BatchChunkDescriptor[1];
        private NativeArray<byte> _brickMaterials = new(VoxelReadGrid.VoxelsPerBlock, Allocator.Persistent);
        private NativeArray<ushort> _surfaceScratch = new(VoxelReadGrid.VoxelsPerBlock, Allocator.Persistent);
        private NativeArray<byte> _boundaryScratch = new(VoxelReadGrid.VoxelsPerBlock, Allocator.Persistent);
        private NativeArray<byte> _waterBatchMaterials = new(GpuWaterSurfaceMesher.MaximumBricksPerSlice * SnapshotBytes, Allocator.Persistent);
        private bool _disposed;
        private bool _submitted;
        private bool _discardBuild;
        private bool _outcomeFailed;
        private uint4 _outcome;
        private BuildState _build;

        public GpuWaterSurfaceChunkCache()
        {
            _mesherShader = UnityEngine.Object.Instantiate(Resources.Load<ComputeShader>("GpuWaterSurfaceMesher"));
            _arenaShader = UnityEngine.Object.Instantiate(Resources.Load<ComputeShader>("GpuSurfacePageArena"));
            _drawShader = UnityEngine.Object.Instantiate(Resources.Load<ComputeShader>("GpuWaterDrawArguments"));
            _geometryArena = new GpuSurfacePageArena(_arenaShader,
                ArenaVertexCapacity, ArenaIndexCapacity, ArenaDrawCapacity, primaryArena: false);
            _origins = new ComputeBuffer(GpuWaterSurfaceMesher.MaximumBricks, 16);
            _materials = new ComputeBuffer(GpuWaterSurfaceMesher.MaximumBricks * GpuWaterSurfaceMesher.SnapshotWords, 4);
            _counters = new ComputeBuffer(21, 4);
            _descriptors = new ComputeBuffer(1, GpuSurfaceExtractor.BatchChunkDescriptor.Stride);
            _outcomes = new ComputeBuffer(1, 16);
            _drawArgs = new ComputeBuffer(ArenaDrawCapacity * 4, 4, ComputeBufferType.IndirectArguments);
            _lifetime = new GpuSubmissionLifetime(ReleaseGpuResources);
            Application.quitting += DisposeForApplicationQuit;
        }

        public int ResidentCount => _entries.Count;
        public int DirtyCount => _dirty.Count + (_build.Active ? 1 : 0);
        public ulong CompletedBuildCount { get; private set; }
        public ulong StaleBuildCount { get; private set; }
        public ulong UploadedGeometryBytes => 0; // Geometry never traverses the CPU.
        public ulong UploadedSnapshotBytes { get; private set; }
        public ulong FramePathBlockingCompletionViolations => 0;
        public int RunningJobCount => 0;
        public long ResidentGpuBytes => (long)ArenaVertexCapacity * 32 + (long)ArenaIndexCapacity * 4;
        public IReadOnlyList<Entry> Visible => _visible;
        public int PendingUploadCount => _build.Active && _build.PendingPublication ? 1 : 0;
        public int PendingUploadBytes => PendingUploadCount * 16; // Publication control only.
        public ulong ArenaAllocationFailures { get; private set; }
        public ulong MeshOverflowCount { get; private set; }

        public void InvalidateSurfaceBricks(IRegionReadSource storage,
                                            IReadOnlyList<int3> worldBricks)
        {
            if (storage == null || worldBricks == null) return;

            RegionReadView cachedRegion = default;
            for (int i = 0; i < worldBricks.Count; i++)
                InvalidateSurfaceBrick(storage, worldBricks[i], ref cachedRegion);
        }

        internal void InvalidateSurfaceBrick(IRegionReadSource storage, int3 worldBrick,
                                             ref RegionReadView cachedRegion)
        {
            if (storage == null) return;

            int3 chunk = WorldBrickChunk(worldBrick);
            if (!cachedRegion.IsCreated || !cachedRegion.ContainsWorldBlock(worldBrick))
            {
                if (!storage.TryAcquireRegionContainingBlock(worldBrick, out cachedRegion))
                    cachedRegion = default;
            }
            bool containsWater = ContainsRegisteredWater(cachedRegion, worldBrick);

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

            // Existing water and same-chunk occluder edits also change exposed faces/halos.
            MarkKnownDirty(chunk);
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
            _geometryArena.FlushHandleCommands(Time.frameCount);
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
                FinishGpuBuild();
                if (_build.PendingPublication) break;
            }
        }

        public bool TryPublishPending(int byteBudget, out int uploadedBytes)
        {
            uploadedBytes = 0;
            if (!_build.Active || !_build.PendingPublication || byteBudget < 16) return false;
            bool stale = _discardBuild || _build.WaterMask != VoxelPresentationCatalogue.WaterMaterialMask
                || !_waterBricks.ContainsKey(_build.Coordinate)
                || (_desiredVersions.TryGetValue(_build.Coordinate, out ulong desired)
                    && desired > _build.SourceVersion);
            bool identity = _outcome.y == (uint)_build.Handle
                && _outcome.z == (uint)_build.SourceVersion
                && _outcome.w == (uint)(_build.SourceVersion >> 32);
            if (stale || _outcomeFailed || _outcome.x != 0 || !identity)
            {
                _geometryArena.AbortPending(_build.Handle, _build.SourceVersion, Time.frameCount);
                if (stale)
                {
                    StaleBuildCount++;
                    if (_waterBricks.ContainsKey(_build.Coordinate)) MarkDirty(_build.Coordinate);
                }
                else
                {
                    if (_outcome.x == 1) ArenaAllocationFailures++;
                    if (_outcome.x == 3) MeshOverflowCount++;
                    MarkDirty(_build.Coordinate);
                }
                ResetBuildOutput();
                return false;
            }
            _geometryArena.CommitPending(_build.Handle, _build.SourceVersion, Time.frameCount);
            Entry entry = _entries[_build.Coordinate];
            entry.SourceVersion = _build.SourceVersion;
            entry.Ready = true;
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
                if (!entry.Ready) continue;
                if (!GeometryUtility.TestPlanesAABB(_frustumPlanes, entry.WorldBounds(voxelSize)))
                    continue;
                _visible.Add(entry);
            }
            if (_visible.Count > 0)
            {
                int kernel = _drawShader.FindKernel("CSWaterDrawArguments");
                _drawShader.SetBuffer(kernel, "_LiveChunkGeometry", _geometryArena.LiveChunkGeometry);
                _drawShader.SetBuffer(kernel, "_WaterDrawArguments", _drawArgs);
                _drawShader.SetInt("_WaterHandleCapacity", ArenaDrawCapacity);
                _drawShader.Dispatch(kernel, (ArenaDrawCapacity + 63) / 64, 1, 1);
            }
            return _visible;
        }

        private void BindDraw(MaterialPropertyBlock properties)
        {
            properties.SetInteger("_WaterPagedDraw", 2);
            properties.SetBuffer("_WaterLiveGeometry", _geometryArena.LiveChunkGeometry);
            properties.SetBuffer("_PagedSurfaceVertices", _geometryArena.Vertices);
            properties.SetBuffer("_PagedSurfaceIndices", _geometryArena.Indices);
            properties.SetBuffer("_PagedVertexPageTable", _geometryArena.VertexPageTable);
            properties.SetBuffer("_PagedIndexPageTable", _geometryArena.IndexPageTable);
            properties.SetInteger("_PagedVertexPageSize", GpuSurfacePageArena.VertexPageSize);
            properties.SetInteger("_PagedIndexPageSize", GpuSurfacePageArena.IndexPageSize);
            properties.SetInteger("_PagedMaxVertexPagesPerChunk", GpuSurfacePageArena.MaxVertexPagesPerChunk);
            properties.SetInteger("_PagedMaxIndexPagesPerChunk", GpuSurfacePageArena.MaxIndexPagesPerChunk);
        }

        // The render pass records this after its final water draw, preserving buffers through
        // the final consumer, including world teardown. This is completion-only metadata.
        internal void RecordDrawCompletion(CommandBuffer commands)
        {
            _lifetime.Retain();
            commands.RequestAsyncReadback(_descriptors, 4, 0, _ => _lifetime.Release());
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
            _build = new BuildState
            {
                Active = true,
                Coordinate = best,
                Cursor = 0,
                Handle = -1,
                VoxelSize = voxelSize,
                WaterMask = VoxelPresentationCatalogue.WaterMaterialMask,
                SourceVersion = _desiredVersions.TryGetValue(best, out ulong version)
                    ? version : 0
            };
            return true;
        }

        private bool StepBuild(IRegionReadSource storage, float voxelSize, double deadline)
        {
            if (_submitted) return false;
            if (_discardBuild || _build.WaterMask != VoxelPresentationCatalogue.WaterMaterialMask
                || (_desiredVersions.TryGetValue(_build.Coordinate, out ulong desired)
                && desired > _build.SourceVersion))
            {
                if (_build.Allocated)
                    _geometryArena.AbortPending(_build.Handle, _build.SourceVersion, Time.frameCount);
                StaleBuildCount++;
                if (_waterBricks.ContainsKey(_build.Coordinate)) MarkDirty(_build.Coordinate);
                ResetBuildOutput();
                return false;
            }
            if (_build.Allocated)
            {
                int count = math.min(GpuWaterSurfaceMesher.MaximumBricksPerSlice,
                    _build.BrickCount - _build.WriteCursor);
                if (count > 0)
                {
                    GpuWaterSurfaceMesher.Write(_mesherShader, _origins, _materials, _counters,
                        _geometryArena, _build.WaterMask, _build.VoxelSize,
                        _build.WriteCursor, count);
                    _build.WriteCursor += count;
                }
                return _build.WriteCursor == _build.BrickCount;
            }
            if (!_waterBricks.TryGetValue(_build.Coordinate, out HashSet<int3> set) || set.Count == 0)
            { ResetBuildOutput(); return false; }
            const int totalBrickSlots = BricksPerAxis * BricksPerAxis * BricksPerAxis;
            RegionReadView cachedRegion = default;
            int batchCount = 0;
            while (_build.Cursor < totalBrickSlots && batchCount < GpuWaterSurfaceMesher.MaximumBricksPerSlice)
            {
                int linear = _build.Cursor++;
                int3 worldBrick = _build.Coordinate * BricksPerAxis
                    + new int3(linear % BricksPerAxis, (linear / BricksPerAxis) % BricksPerAxis,
                        linear / (BricksPerAxis * BricksPerAxis));
                if (set.Contains(worldBrick) && SnapshotWaterBrick(storage, ref cachedRegion, worldBrick, batchCount))
                    batchCount++;
                if (Time.realtimeSinceStartupAsDouble >= deadline) break;
            }
            if (batchCount > 0)
            {
                int words = batchCount * GpuWaterSurfaceMesher.SnapshotWords;
                for (int i = 0; i < words; i++)
                {
                    int b = i * 4;
                    _materialStaging[i] = (uint)(_waterBatchMaterials[b]
                        | _waterBatchMaterials[b + 1] << 8 | _waterBatchMaterials[b + 2] << 16
                        | _waterBatchMaterials[b + 3] << 24);
                }
                _origins.SetData(_originStaging, 0, _build.BrickCount, batchCount);
                _materials.SetData(_materialStaging, 0, _build.BrickCount * GpuWaterSurfaceMesher.SnapshotWords, words);
                GpuWaterSurfaceMesher.Count(_mesherShader, _origins, _materials, _counters,
                    _build.WaterMask, _build.VoxelSize, _build.BrickCount, batchCount);
                _build.BrickCount += batchCount;
                UploadedSnapshotBytes += (ulong)(words * 4 + batchCount * 16);
            }
            if (_build.Cursor < totalBrickSlots) return false;
            if (_build.BrickCount == 0)
            {
                RemoveWaterChunk(_build.Coordinate);
                return false;
            }
            if (!_entries.TryGetValue(_build.Coordinate, out Entry entry))
            {
                entry = AcquireEntry(_build.Coordinate);
                _entries.Add(_build.Coordinate, entry);
            }
            if (!entry.AcquireHandle()) { ArenaAllocationFailures++; return false; }
            _build.Handle = entry.Handle;
            _geometryArena.QueueGeneration(entry.Handle, _build.SourceVersion);
            _geometryArena.FlushHandleCommands(Time.frameCount);
            _descriptorStaging[0] = new GpuSurfaceExtractor.BatchChunkDescriptor
            {
                Handle = (uint)entry.Handle,
                GenerationLow = (uint)_build.SourceVersion,
                GenerationHigh = (uint)(_build.SourceVersion >> 32)
            };
            _descriptors.SetData(_descriptorStaging);
            _geometryArena.AllocateBatch(_descriptors, _counters, 1, 17, Time.frameCount);
            _build.Allocated = true;
            return false;
        }

        private bool SnapshotWaterBrick(IRegionReadSource storage,
                                        ref RegionReadView cachedRegion,
                                        int3 worldBrick, int batchIndex)
        {
            if (!TryLoadBrickMaterials(storage, worldBrick, ref cachedRegion)
                || !LoadedBrickContainsWater())
                return false;

            int snapshotBase = batchIndex * SnapshotBytes;
            NativeArray<byte>.Copy(_brickMaterials, 0, _waterBatchMaterials,
                                   snapshotBase, VoxelReadGrid.VoxelsPerBlock);
            int3 brickBaseVoxel = worldBrick * E;
            _originStaging[batchIndex] = new int4(brickBaseVoxel, 0);

            for (int axis = 0; axis < 3; axis++)
            {
                int axisA = (axis + 1) % 3;
                int axisB = (axis + 2) % 3;
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    int face = axis * 2 + (sign > 0 ? 1 : 0);
                    int faceBase = snapshotBase + VoxelReadGrid.VoxelsPerBlock
                                 + face * (E * E);
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

        private void FinishGpuBuild()
        {
            _geometryArena.PublishBatch(_descriptors, _counters, 1, 17, Time.frameCount);
            _geometryArena.CopyBatchOutcomes(_counters, _outcomes, 1, 17);
            _submitted = true;
            _lifetime.Retain();
            AsyncGPUReadback.Request(_outcomes, request =>
            {
                try
                {
                    if (_disposed) return;
                    _outcomeFailed = request.hasError;
                    if (!_outcomeFailed) _outcome = request.GetData<uint4>()[0];
                    _submitted = false;
                    _build.PendingPublication = true;
                }
                finally { _lifetime.Release(); }
            });
        }

        private void ResetBuildOutput()
        {
            if (_submitted) throw new InvalidOperationException("Water transaction still in flight.");
            _build = default;
            _discardBuild = false;
            _outcomeFailed = false;
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

        private static bool IsWater(byte material) => VoxelPresentationCatalogue.IsWaterMaterial(material);

        private static bool ContainsRegisteredWater(RegionReadView region, int3 worldBrick)
        {
            if (!region.IsCreated) return false;
            uint mask = VoxelPresentationCatalogue.WaterMaterialMask;
            while (mask != 0)
            {
                byte first = (byte)math.tzcnt(mask);
                mask &= mask - 1;
                byte second = first;
                if (mask != 0) { second = (byte)math.tzcnt(mask); mask &= mask - 1; }
                if (region.TryWorldBlockContainsEitherMaterial(worldBrick, first, second, out bool found) && found)
                    return true;
            }
            return false;
        }

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
                if (_submitted) { _discardBuild = true; return; }
                ResetBuildOutput();
            }
        }

        private Entry AcquireEntry(int3 coordinate)
        {
            if (_entryPool.Count == 0) return new Entry(coordinate, this);
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
            if (_disposed) return;
            _disposed = true;

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
            _geometryArena.FlushHandleCommands(Time.frameCount);
            // Count/write slices may have been submitted before the outcome request exists.
            _lifetime.Retain();
            AsyncGPUReadback.Request(_descriptors, 4, 0, _ => _lifetime.Release());
            _lifetime.Dispose();
            if (_waterBatchMaterials.IsCreated) _waterBatchMaterials.Dispose();
            if (_brickMaterials.IsCreated) _brickMaterials.Dispose();
            if (_surfaceScratch.IsCreated) _surfaceScratch.Dispose();
            if (_boundaryScratch.IsCreated) _boundaryScratch.Dispose();
            _build = default;
        }

        // The player loop will no longer deliver deferred disposal callbacks after exit.
        // Keep this subscription until physical release, including caches retired by a scene
        // change immediately before quitting. Normal Dispose and frame work never wait.
        internal void DisposeForApplicationQuit()
        {
            Dispose();
            AsyncGPUReadback.WaitAllRequests();
        }

        private void ReleaseGpuResources()
        {
            Application.quitting -= DisposeForApplicationQuit;
            _geometryArena.Dispose();
            _origins.Release(); _materials.Release(); _counters.Release();
            _descriptors.Release(); _outcomes.Release(); _drawArgs.Release();
            CoreUtils.Destroy(_mesherShader);
            CoreUtils.Destroy(_arenaShader);
            CoreUtils.Destroy(_drawShader);
        }
    }
}
