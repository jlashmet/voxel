using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Core.Storage;

namespace VoxelEngine.Rendering
{
    /// <summary>
    /// The GPU mirror of the sparse brickmap: a window of region slots, the brick pointer grids
    /// for the regions occupying those slots, and the voxel bytes of the brick pool.
    ///
    /// The raymarch needs to answer "what brick is at this coordinate" per step, so the CPU-side
    /// hash map is replaced by a flat window of region slots centred on the camera. A window
    /// entry is a slot index or -1; a slot holds one region's 262,144 brick references. Regions
    /// outside the window are simply not visible to the raymarch, which is the same statement as
    /// residency — they hold no data to march through anyway.
    ///
    /// Uploads are incremental in both tiers. Brick pointers go up per region when that region's
    /// contents change; voxel bytes go up per brick from <see cref="BrickPool.DirtyBricks"/>.
    /// Re-uploading either tier wholesale would cost tens of megabytes per edit.
    /// </summary>
    public sealed class VoxelGpuBuffers : IDisposable
    {
        /// <summary>
        /// ComputeBuffers alive right now, across every instance.
        ///
        /// A ledger rather than a memory measurement, because memory measurement does not work
        /// here: Profiler.GetAllocatedMemoryForGraphicsDriver never decreases when a buffer is
        /// released, and process RSS does not move at all for GPU allocations in a headless
        /// editor. Both report a flat line for a real leak or a leak for correct code, depending
        /// which one you pick. Counting create against release tests the actual contract — every
        /// buffer this type allocates is handed back — and it can fail, which is the property
        /// that matters.
        /// </summary>
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

        /// <summary>Region slots along each window axis. Terrain is thin in y, so the window is too.</summary>
        public const int WindowX = 16;

        public const int WindowY = 4;
        public const int WindowZ = 16;

        private const int WindowCells = WindowX * WindowY * WindowZ;

        /// <summary>Region slots held on the GPU. Each costs 1 MB of brick pointers.</summary>
        public const int MaxSlots = 48;

        /// <summary>512 voxel bytes per brick, packed four to a uint.</summary>
        private const int UintsPerBrick = VoxelDimensions.VoxelsPerBrick / 4;

        private ComputeBuffer _windowBuffer;   // WindowCells ints: slot index or -1
        private ComputeBuffer _brickRefBuffer; // MaxSlots * BricksPerRegion ints
        private ComputeBuffer _voxelBuffer;    // poolCapacity * 128 uints

        private readonly int[] _window = new int[WindowCells];
        private readonly Dictionary<int3, int> _slotOfRegion = new();
        private readonly int3[] _regionOfSlot = new int3[MaxSlots];
        private readonly bool[] _slotUsed = new bool[MaxSlots];

        private NativeArray<int> _brickRefScratch;
        private NativeArray<uint> _voxelScratch;

        private int3 _windowOrigin;
        private int _poolCapacity;

        public ComputeBuffer WindowBuffer => _windowBuffer;
        public ComputeBuffer BrickRefBuffer => _brickRefBuffer;
        public ComputeBuffer VoxelBuffer => _voxelBuffer;
        public int3 WindowOrigin => _windowOrigin;

        /// <summary>Region slots currently mapped.</summary>
        public int ResidentSlots { get; private set; }

        /// <summary>Bricks uploaded during the most recent sync.</summary>
        public int LastBricksUploaded { get; private set; }

        /// <summary>Regions whose pointer grid was uploaded during the most recent sync.</summary>
        public int LastRegionsUploaded { get; private set; }

        public bool IsCreated => _voxelBuffer != null;

        /// <summary>
        /// Bricks uploaded per sync, and the size of the staging scratch.
        ///
        /// Raising this to cover a whole region in one call was tried and made the raymarch
        /// render nothing: a single 30 MB SetData does not land before the dispatch that reads it
        /// in the same frame. Bricks not yet uploaded raymarch as empty, so the world fades in.
        /// </summary>
        private const int MaxBricksPerSync = 8192;

        /// <summary>
        /// Hard cap on SetData calls per sync.
        ///
        /// This is the load-bearing limit. Each SetData is a driver staging allocation, and
        /// issuing thousands per frame grows process memory without bound — a castle that dirtied
        /// 130,000 scattered bricks took a machine to 200 GB while per-process RSS reporting
        /// showed under 4 GB.
        ///
        /// Run coalescing helps when pool indices happen to be contiguous and does nothing when
        /// they are not, so it cannot be relied on. Uploading contiguous *spans* — re-sending a
        /// few clean bricks caught between dirty ones — costs bandwidth and bounds the call count
        /// absolutely, which is the trade worth making.
        /// </summary>
        private const int MaxUploadCallsPerSync = 4;

        /// <summary>Refuses to mirror a pool larger than this. 262144 slots is 134 MB of VRAM.</summary>
        public const int MaxMirroredBricks = 262144;

        public void EnsureCreated(int poolCapacity)
        {
            if (_voxelBuffer != null && _poolCapacity == poolCapacity) return;

            // A bad capacity here becomes a multi-gigabyte ComputeBuffer, which takes the whole
            // machine with it rather than failing locally. Refuse instead.
            if (poolCapacity <= 0 || poolCapacity > MaxMirroredBricks)
            {
                Debug.LogError($"VoxelGpuBuffers: refusing to mirror a pool of {poolCapacity} " +
                               $"bricks (limit {MaxMirroredBricks}). The raymarch will not draw.");
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

            _brickRefScratch = new NativeArray<int>(VoxelDimensions.BricksPerRegion, Allocator.Persistent,
                                                    NativeArrayOptions.UninitializedMemory);
            _voxelScratch = new NativeArray<uint>(MaxBricksPerSync * UintsPerBrick, Allocator.Persistent,
                                                  NativeArrayOptions.UninitializedMemory);

            for (var i = 0; i < WindowCells; i++) _window[i] = -1;
            for (var i = 0; i < MaxSlots; i++) _slotUsed[i] = false;
            _slotOfRegion.Clear();
            ResidentSlots = 0;
        }

        /// <summary>
        /// Brings the GPU mirror in line with the world around <paramref name="cameraRegion"/>.
        ///
        /// Regions that fell out of the window release their slot; regions that entered take one
        /// and upload their pointer grid. Then the pool's dirty bricks go up, bounded per call so
        /// that finishing a region does not produce a frame that uploads 5 MB.
        /// </summary>
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

            ReleaseSlotsOutsideWindow();
            AssignSlots(ref table, regionsNeedingRefresh);

            // Consumed: whatever needed re-sending has been sent. Clearing here rather than at
            // the producer is what keeps the signal alive long enough to be acted on.
            regionsNeedingRefresh?.Clear();

            UploadDirtyBricks(ref pool);

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

        private void AssignSlots(ref RegionTable table, HashSet<int3> regionsNeedingRefresh)
        {
            var resident = table.GetResidentCoords(Allocator.Temp);

            for (var i = 0; i < resident.Length; i++)
            {
                var coord = resident[i];
                if (!TryWindowIndex(coord, out var cell)) continue;

                bool isNew = !_slotOfRegion.TryGetValue(coord, out var slot);

                if (isNew)
                {
                    if (!TryTakeSlot(out slot)) continue; // window is fuller than the slot budget
                    _slotOfRegion[coord] = slot;
                    _regionOfSlot[slot] = coord;
                    ResidentSlots++;
                }

                _window[cell] = slot;

                bool refresh = isNew || (regionsNeedingRefresh != null && regionsNeedingRefresh.Contains(coord));
                if (refresh && table.TryGetRegion(coord, out var region))
                    UploadRegionPointers(slot, region);
            }

            resident.Dispose();
        }

        private void UploadRegionPointers(int slot, in Region region)
        {
            var refs = region.BrickRefs;
            for (var i = 0; i < VoxelDimensions.BricksPerRegion; i++)
                _brickRefScratch[i] = refs[i].Value;

            _brickRefBuffer.SetData(_brickRefScratch, 0, slot * VoxelDimensions.BricksPerRegion,
                                    VoxelDimensions.BricksPerRegion);
            LastRegionsUploaded++;
        }

        /// <summary>Upload calls issued on the most recent sync. One per contiguous run.</summary>
        public int LastUploadCalls { get; private set; }

        private int[] _sortScratch = new int[MaxBricksPerSync];

        /// <summary>
        /// Uploads changed bricks, packing four voxel bytes to a uint.
        ///
        /// Dirty bricks are sorted and uploaded as contiguous runs, one SetData per run. That
        /// grouping is not a micro-optimisation: a SetData is a driver staging allocation, and
        /// issuing thousands of them per frame grew process memory by gigabytes within seconds —
        /// enough to be killed by the run guard at 6 GB. Pool indices are handed out
        /// sequentially, so a freshly generated region is a handful of runs rather than
        /// thousands of separate writes.
        /// </summary>
        private void UploadDirtyBricks(ref BrickPool pool)
        {
            LastUploadCalls = 0;

            var dirty = pool.DirtyBricks;
            if (!dirty.IsCreated || dirty.Length == 0) return;

            int count = math.min(dirty.Length, MaxBricksPerSync);
            for (var i = 0; i < count; i++) _sortScratch[i] = dirty[i];
            System.Array.Sort(_sortScratch, 0, count);

            var voxels = pool.Voxels;
            int consumed = 0;

            // Walk the sorted indices, emitting at most MaxUploadCallsPerSync contiguous spans.
            // A span may include clean bricks between dirty ones; re-uploading those is cheaper
            // than the driver allocation another call would cost.
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

            if (consumed >= dirty.Length)
            {
                pool.ClearDirtyBricks();
            }
            else
            {
                // Partial drain: the consumed prefix must have its flags cleared too, or those
                // slots stay pinned as dirty and never accept another mark.
                for (var i = 0; i < consumed; i++) pool.ClearDirty(_sortScratch[i]);

                for (var i = 0; i < dirty.Length - consumed; i++)
                    dirty[i] = dirty[i + consumed];

                dirty.Length -= consumed;
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

            if (_brickRefScratch.IsCreated) _brickRefScratch.Dispose();
            if (_voxelScratch.IsCreated) _voxelScratch.Dispose();

            _slotOfRegion.Clear();
            ResidentSlots = 0;
        }
    }
}
