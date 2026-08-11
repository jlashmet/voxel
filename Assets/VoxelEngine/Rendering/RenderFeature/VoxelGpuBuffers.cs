using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Core.Storage;

namespace VoxelEngine.Rendering
{
    public sealed class VoxelGpuBuffers : IDisposable
    {
        public static int LiveBuffers { get; private set; }

        private static ComputeBuffer Allocate(int count, int stride, ComputeBufferType type)
        {
            var buffer = new ComputeBuffer(count, stride, type);
            LiveBuffers++;
            return buffer;
        }

        private static void ReleaseTracked(ref ComputeBuffer buffer)
        {
            if (buffer == null) return;
            buffer.Release();
            buffer = null;
            LiveBuffers--;
        }

        public const int WindowX = 16;
        public const int WindowY = 4;
        public const int WindowZ = 16;
        private const int WindowCells = WindowX * WindowY * WindowZ;
        public const int MaxSlots = 48;
        private const int UintsPerBrick = VoxelDimensions.VoxelsPerBrick / 4;

        private ComputeBuffer _windowBuffer;
        private ComputeBuffer _brickRefBuffer;
        private ComputeBuffer _voxelBuffer;
        private ComputeBuffer _densityBuffer;
        private ComputeBuffer _densityJobBuffer;

        private readonly int[] _window = new int[WindowCells];
        private readonly Dictionary<int3, int> _slotOfRegion = new();
        private readonly int3[] _regionOfSlot = new int3[MaxSlots];
        private readonly bool[] _slotUsed = new bool[MaxSlots];
        private readonly Dictionary<int, int3> _worldBrickOfPool = new();
        private readonly Dictionary<int3, int> _poolOfWorldBrick = new();
        private readonly HashSet<int> _pendingDensity = new();
        private readonly List<int3> _lastDensityWorldBricks = new(MaxBricksPerSync);
        private readonly List<int3> _lastSurfaceWorldBricks = new(MaxBricksPerSync);

        private NativeArray<int> _brickRefScratch;
        private NativeArray<uint> _voxelScratch;
        private NativeArray<int4> _densityJobScratch;

        private int3 _windowOrigin;
        private int _poolCapacity;

        public ComputeBuffer WindowBuffer => _windowBuffer;
        public ComputeBuffer BrickRefBuffer => _brickRefBuffer;
        public ComputeBuffer VoxelBuffer => _voxelBuffer;
        public ComputeBuffer DensityBuffer => _densityBuffer;
        public ComputeBuffer DensityJobBuffer => _densityJobBuffer;
        public int3 WindowOrigin => _windowOrigin;
        public int DensityJobCount { get; private set; }
        public int PendingDensityCount => _pendingDensity.Count;
        public IReadOnlyList<int3> LastDensityWorldBricks => _lastDensityWorldBricks;

        /// <summary>
        /// Surface-bearing world bricks discovered directly from resident region pointer grids.
        /// This is independent of density jobs: uniform solid bricks do not own density storage,
        /// but a solid/air boundary aligned to a brick face still needs a Surface Nets chunk.
        /// </summary>
        public IReadOnlyList<int3> LastSurfaceWorldBricks => _lastSurfaceWorldBricks;

        public int ResidentSlots { get; private set; }
        public int LastBricksUploaded { get; private set; }
        public int LastRegionsUploaded { get; private set; }
        public bool IsCreated => _voxelBuffer != null;

        private const int MaxBricksPerSync = 8192;
        private const int MaxUploadCallsPerSync = 4;
        public const int MaxMirroredBricks = 262144;

        public void EnsureCreated(int poolCapacity)
        {
            if (_voxelBuffer != null && _poolCapacity == poolCapacity) return;

            if (poolCapacity <= 0 || poolCapacity > MaxMirroredBricks)
            {
                Debug.LogError($"VoxelGpuBuffers: refusing to mirror a pool of {poolCapacity} " +
                               $"bricks (limit {MaxMirroredBricks}). The renderer will not draw.");
                Dispose();
                _poolCapacity = 0;
                return;
            }

            Dispose();
            _poolCapacity = poolCapacity;

            _windowBuffer = Allocate(WindowCells, sizeof(int), ComputeBufferType.Structured);
            _brickRefBuffer = Allocate(MaxSlots * VoxelDimensions.BricksPerRegion, sizeof(int),
                                       ComputeBufferType.Structured);
            _voxelBuffer = Allocate(poolCapacity * UintsPerBrick, sizeof(uint),
                                    ComputeBufferType.Structured);
            _densityBuffer = Allocate(poolCapacity * UintsPerBrick, sizeof(uint),
                                      ComputeBufferType.Structured);
            _densityJobBuffer = Allocate(MaxBricksPerSync, sizeof(int) * 4,
                                         ComputeBufferType.Structured);

            _brickRefScratch = new NativeArray<int>(VoxelDimensions.BricksPerRegion, Allocator.Persistent,
                                                    NativeArrayOptions.UninitializedMemory);
            _voxelScratch = new NativeArray<uint>(MaxBricksPerSync * UintsPerBrick, Allocator.Persistent,
                                                  NativeArrayOptions.UninitializedMemory);
            _densityJobScratch = new NativeArray<int4>(MaxBricksPerSync, Allocator.Persistent,
                                                       NativeArrayOptions.UninitializedMemory);

            for (var i = 0; i < WindowCells; i++) _window[i] = -1;
            for (var i = 0; i < MaxSlots; i++) _slotUsed[i] = false;
            _slotOfRegion.Clear();
            _worldBrickOfPool.Clear();
            _poolOfWorldBrick.Clear();
            _pendingDensity.Clear();
            ResidentSlots = 0;
        }

        public void Sync(ref RegionTable table, ref BrickPool pool, int3 cameraRegion,
                         HashSet<int3> regionsNeedingRefresh)
        {
            EnsureCreated(pool.Capacity);
            if (_voxelBuffer == null) return;

            _windowOrigin = new int3(cameraRegion.x - WindowX / 2,
                                     cameraRegion.y - WindowY / 2,
                                     cameraRegion.z - WindowZ / 2);

            LastRegionsUploaded = 0;
            LastBricksUploaded = 0;
            DensityJobCount = 0;
            _lastDensityWorldBricks.Clear();
            _lastSurfaceWorldBricks.Clear();

            ReleaseSlotsOutsideWindow();
            AssignSlots(ref table, ref pool, regionsNeedingRefresh);
            regionsNeedingRefresh?.Clear();

            UploadDirtyBricks(ref pool);
            UploadDensityJobs();
            _windowBuffer.SetData(_window);
        }

        private void ReleaseSlotsOutsideWindow()
        {
            for (var i = 0; i < WindowCells; i++) _window[i] = -1;
            List<int3> dropped = null;

            foreach (var kv in _slotOfRegion)
            {
                if (TryWindowIndex(kv.Key, out var cell))
                {
                    _window[cell] = kv.Value;
                    continue;
                }
                (dropped ??= new List<int3>()).Add(kv.Key);
            }

            if (dropped == null) return;
            foreach (var coord in dropped)
            {
                _slotUsed[_slotOfRegion[coord]] = false;
                _slotOfRegion.Remove(coord);
                ResidentSlots--;
            }
        }

        private void AssignSlots(ref RegionTable table, ref BrickPool pool,
                                 HashSet<int3> regionsNeedingRefresh)
        {
            var resident = table.GetResidentCoords(Allocator.Temp);

            for (var i = 0; i < resident.Length; i++)
            {
                var coord = resident[i];
                if (!TryWindowIndex(coord, out var cell)) continue;

                bool isNew = !_slotOfRegion.TryGetValue(coord, out var slot);
                if (isNew)
                {
                    if (!TryTakeSlot(out slot)) continue;
                    _slotOfRegion[coord] = slot;
                    _regionOfSlot[slot] = coord;
                    ResidentSlots++;
                }

                _window[cell] = slot;
                bool refresh = isNew || (regionsNeedingRefresh != null && regionsNeedingRefresh.Contains(coord));
                if (refresh && table.TryGetRegion(coord, out var region))
                    UploadRegionPointers(slot, region, isNew, ref pool);
            }

            resident.Dispose();
        }

        private void UploadRegionPointers(int slot, in Region region, bool ensureVoxelUpload,
                                          ref BrickPool pool)
        {
            var refs = region.BrickRefs;
            int edge = VoxelDimensions.RegionEdge;
            int yStride = edge;
            int zStride = edge * edge;
            int3 regionBrickOrigin = region.Coord * edge;

            for (var i = 0; i < VoxelDimensions.BricksPerRegion; i++)
            {
                BrickRef brick = refs[i];
                _brickRefScratch[i] = brick.Value;

                int bx = i & VoxelDimensions.RegionEdgeMask;
                int by = (i >> VoxelDimensions.RegionEdgeLog2) & VoxelDimensions.RegionEdgeMask;
                int bz = i >> (VoxelDimensions.RegionEdgeLog2 * 2);
                int3 worldBrick = regionBrickOrigin + new int3(bx, by, bz);

                // Surface discovery must not depend on density jobs. Mixed bricks always contain a
                // surface by storage contract. Uniform solid bricks can also own an exposed face
                // when the solid/air boundary happens to align exactly with a brick boundary.
                if (IsPotentialSurfaceBrick(refs, i, bx, by, bz, yStride, zStride))
                    _lastSurfaceWorldBricks.Add(worldBrick);

                if (!brick.IsMixed) continue;

                int poolIndex = brick.PoolIndex;
                MapPoolBrick(poolIndex, worldBrick);

                // A fresh GPU mirror or newly resident region cannot assume BrickPool's gameplay
                // dirty flag is still set. Force every referenced mixed brick through the bounded
                // uploader at least once.
                if (ensureVoxelUpload) pool.MarkDirty(poolIndex);
            }

            _brickRefBuffer.SetData(_brickRefScratch, 0, slot * VoxelDimensions.BricksPerRegion,
                                    VoxelDimensions.BricksPerRegion);
            LastRegionsUploaded++;
        }

        private static bool IsPotentialSurfaceBrick(NativeArray<BrickRef> refs, int index,
                                                     int bx, int by, int bz,
                                                     int yStride, int zStride)
        {
            BrickRef brick = refs[index];
            if (brick.IsEmpty) return false;
            if (brick.IsMixed) return true;

            // Uniform solid bricks normally represent interior mass. They become surface-bearing
            // when any in-region face touches air or a mixed brick. Checking this during the
            // pointer-grid scan is cheap and catches brick-aligned castle walls/floors that never
            // allocate a mixed brick and therefore never produce a density job.
            if (bx > 0 && IsBoundaryNeighbour(refs[index - 1])) return true;
            if (bx + 1 < VoxelDimensions.RegionEdge && IsBoundaryNeighbour(refs[index + 1])) return true;
            if (by > 0 && IsBoundaryNeighbour(refs[index - yStride])) return true;
            if (by + 1 < VoxelDimensions.RegionEdge && IsBoundaryNeighbour(refs[index + yStride])) return true;
            if (bz > 0 && IsBoundaryNeighbour(refs[index - zStride])) return true;
            if (bz + 1 < VoxelDimensions.RegionEdge && IsBoundaryNeighbour(refs[index + zStride])) return true;
            return false;
        }

        private static bool IsBoundaryNeighbour(BrickRef neighbour)
            => neighbour.IsEmpty || neighbour.IsMixed;

        public int LastUploadCalls { get; private set; }
        private int[] _sortScratch = new int[MaxBricksPerSync];

        private void UploadDirtyBricks(ref BrickPool pool)
        {
            LastUploadCalls = 0;
            var dirty = pool.DirtyBricks;
            if (!dirty.IsCreated || dirty.Length == 0) return;

            int count = math.min(dirty.Length, MaxBricksPerSync);
            for (var i = 0; i < count; i++) _sortScratch[i] = dirty[i];
            System.Array.Sort(_sortScratch, 0, count);
            for (var i = 0; i < count; i++) dirty[i] = _sortScratch[i];

            var voxels = pool.Voxels;
            int consumed = 0;
            int cursor = 0;

            while (cursor < count && LastUploadCalls < MaxUploadCallsPerSync)
            {
                int spanStart = _sortScratch[cursor];
                int spanEnd = spanStart;
                int inSpan = 0;

                while (cursor < count && _sortScratch[cursor] - spanStart < MaxBricksPerSync)
                {
                    spanEnd = _sortScratch[cursor];
                    cursor++;
                    inSpan++;
                }

                int spanLength = spanEnd - spanStart + 1;
                for (var b = 0; b < spanLength; b++)
                {
                    int src = pool.VoxelOffset(spanStart + b);
                    int dst = b * UintsPerBrick;
                    for (var u = 0; u < UintsPerBrick; u++)
                    {
                        int p = src + u * 4;
                        _voxelScratch[dst + u] = voxels[p]
                                               | ((uint)voxels[p + 1] << 8)
                                               | ((uint)voxels[p + 2] << 16)
                                               | ((uint)voxels[p + 3] << 24);
                    }
                }

                _voxelBuffer.SetData(_voxelScratch, 0, spanStart * UintsPerBrick,
                                     spanLength * UintsPerBrick);
                LastUploadCalls++;
                consumed += inSpan;
            }

            LastBricksUploaded = consumed;
            for (var i = 0; i < consumed; i++) QueueDensityWithNeighbours(_sortScratch[i]);

            if (consumed >= dirty.Length)
            {
                pool.ClearDirtyBricks();
            }
            else
            {
                for (var i = 0; i < consumed; i++) pool.ClearDirty(_sortScratch[i]);
                for (var i = 0; i < dirty.Length - consumed; i++)
                    dirty[i] = dirty[i + consumed];
                dirty.Length -= consumed;
            }
        }

        private void UploadDensityJobs()
        {
            if (_pendingDensity.Count == 0) return;

            int count = 0;
            var consumed = new List<int>(math.min(_pendingDensity.Count, MaxBricksPerSync));
            foreach (int poolIndex in _pendingDensity)
            {
                if (count >= MaxBricksPerSync) break;
                consumed.Add(poolIndex);
                if (!_worldBrickOfPool.TryGetValue(poolIndex, out int3 worldBrick)) continue;

                _densityJobScratch[count++] = new int4(poolIndex, worldBrick.x,
                                                        worldBrick.y, worldBrick.z);
                _lastDensityWorldBricks.Add(worldBrick);
            }

            foreach (int poolIndex in consumed) _pendingDensity.Remove(poolIndex);
            if (count == 0) return;

            _densityJobBuffer.SetData(_densityJobScratch, 0, 0, count);
            DensityJobCount = count;
        }

        private void MapPoolBrick(int poolIndex, int3 worldBrick)
        {
            if (_worldBrickOfPool.TryGetValue(poolIndex, out int3 oldCoord)
                && oldCoord.Equals(worldBrick) == false
                && _poolOfWorldBrick.TryGetValue(oldCoord, out int oldPool)
                && oldPool == poolIndex)
                _poolOfWorldBrick.Remove(oldCoord);

            _worldBrickOfPool[poolIndex] = worldBrick;
            _poolOfWorldBrick[worldBrick] = poolIndex;
        }

        private void QueueDensityWithNeighbours(int poolIndex)
        {
            if (!_worldBrickOfPool.TryGetValue(poolIndex, out int3 centre)) return;

            for (int z = -1; z <= 1; z++)
            for (int y = -1; y <= 1; y++)
            for (int x = -1; x <= 1; x++)
            {
                int3 coord = centre + new int3(x, y, z);
                if (!_poolOfWorldBrick.TryGetValue(coord, out int neighbourPool)) continue;
                if (!_worldBrickOfPool.TryGetValue(neighbourPool, out int3 mapped)
                    || !mapped.Equals(coord))
                    continue;
                _pendingDensity.Add(neighbourPool);
            }
        }

        private bool TryTakeSlot(out int slot)
        {
            for (var i = 0; i < MaxSlots; i++)
            {
                if (_slotUsed[i]) continue;
                _slotUsed[i] = true;
                slot = i;
                return true;
            }
            slot = -1;
            return false;
        }

        private bool TryWindowIndex(int3 regionCoord, out int index)
        {
            int x = regionCoord.x - _windowOrigin.x;
            int y = regionCoord.y - _windowOrigin.y;
            int z = regionCoord.z - _windowOrigin.z;

            if ((uint)x >= WindowX || (uint)y >= WindowY || (uint)z >= WindowZ)
            {
                index = -1;
                return false;
            }

            index = x + WindowX * (y + WindowY * z);
            return true;
        }

        public void Dispose()
        {
            ReleaseTracked(ref _windowBuffer);
            ReleaseTracked(ref _brickRefBuffer);
            ReleaseTracked(ref _voxelBuffer);
            ReleaseTracked(ref _densityBuffer);
            ReleaseTracked(ref _densityJobBuffer);

            if (_brickRefScratch.IsCreated) _brickRefScratch.Dispose();
            if (_voxelScratch.IsCreated) _voxelScratch.Dispose();
            if (_densityJobScratch.IsCreated) _densityJobScratch.Dispose();

            _slotOfRegion.Clear();
            _worldBrickOfPool.Clear();
            _poolOfWorldBrick.Clear();
            _pendingDensity.Clear();
            _lastDensityWorldBricks.Clear();
            _lastSurfaceWorldBricks.Clear();
            ResidentSlots = 0;
        }
    }
}
