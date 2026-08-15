using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using VoxelEngine.Core.Occupancy;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Core.Storage
{
    /// <summary>
    /// Fixed-capacity pool of mixed bricks, backed by parallel flat native arrays and a
    /// free list.
    ///
    /// Capacity comes from the device tier budget (device-matrix.md: 1.5 GB on PC,
    /// 384 MB on Mobile-HE) and never grows. That is Constitution Principle V: memory
    /// is bounded by configuration rather than by world size or session length. When
    /// the pool is exhausted the streaming layer evicts a region; allocation itself
    /// never fails, because a failure path here would have to be handled in the middle
    /// of an edit, which is exactly where it cannot be handled well.
    ///
    /// Storage is deliberately one large allocation rather than an array per region:
    /// region streaming would otherwise churn allocations continuously, which is the
    /// cost this design exists to avoid.
    /// </summary>
    public struct BrickPool : IDisposable
    {
        /// <summary>Voxel bytes: Capacity * 512, contiguous.</summary>
        public NativeArray<byte> Voxels;

        /// <summary>
        /// Packed <see cref="VoxelSurfaceSemantics"/> values parallel to <see cref="Voxels"/>.
        /// A zero value means the material default style and no coating. Keeping this beside the
        /// mixed-brick payload allows curved and planar cells to coexist in one brick without a
        /// second brick classifier.
        /// </summary>
        public NativeArray<ushort> SurfaceSemantics;

        /// <summary>
        /// Quantized authored boundary distances parallel to <see cref="Voxels"/>. This payload
        /// belongs to geometry, not presentation. Zero falls back to occupancy reconstruction.
        /// </summary>
        public NativeArray<byte> BoundarySamples;

        /// <summary>Occupancy words: Capacity * 8, contiguous and parallel to Voxels.</summary>
        public NativeArray<ulong> Occupancy;

        private NativeList<int> _freeList;
        // Handle-like allocator state. BrickPool is copied into Storage capability objects, so
        // scalar allocator bookkeeping must live in shared native memory just like the payloads.
        private NativeArray<int> _highWaterState;

        private int HighWater
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _highWaterState.IsCreated ? _highWaterState[0] : 0;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => _highWaterState[0] = value;
        }

        public int Capacity { get; private set; }

        /// <summary>Bricks currently allocated. The number to watch in a soak test.</summary>
        public int AllocatedCount => HighWater - _freeList.Length;

        public bool IsCreated => Voxels.IsCreated;

        /// <summary>
        /// True when the pool is full enough that the streaming layer should evict a
        /// region before the next edit. Checked by the caller rather than enforced
        /// here, so that eviction policy stays in the streaming layer where it belongs.
        /// </summary>
        public bool IsUnderPressure => AllocatedCount >= (Capacity - (Capacity >> 4));

        public BrickPool(int capacity, Allocator allocator)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));

            Capacity = capacity;
            Voxels = new NativeArray<byte>(capacity * VoxelDimensions.VoxelsPerBrick,
                                           allocator, NativeArrayOptions.ClearMemory);
            SurfaceSemantics = new NativeArray<ushort>(capacity * VoxelDimensions.VoxelsPerBrick,
                                                     allocator, NativeArrayOptions.ClearMemory);
            BoundarySamples = new NativeArray<byte>(capacity * VoxelDimensions.VoxelsPerBrick,
                                                    allocator, NativeArrayOptions.ClearMemory);
            Occupancy = new NativeArray<ulong>(capacity * VoxelDimensions.OccupancyWordsPerBrick,
                                              allocator, NativeArrayOptions.ClearMemory);
            _freeList = new NativeList<int>(capacity >> 4, allocator);
            _highWaterState = new NativeArray<int>(1, allocator, NativeArrayOptions.ClearMemory);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int VoxelOffset(int brickIndex) => brickIndex * VoxelDimensions.VoxelsPerBrick;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int OccupancyOffset(int brickIndex) => brickIndex * VoxelDimensions.OccupancyWordsPerBrick;

        /// <summary>
        /// Reserve a zeroed brick slot.
        ///
        /// Contract (contracts/module-interfaces.md): this never fails. Callers must
        /// keep the pool below capacity by evicting regions; see
        /// <see cref="IsUnderPressure"/>. Exhaustion is a streaming defect, not an
        /// allocation defect, and is surfaced as such.
        /// </summary>
        public int Allocate()
        {
            int index;

            if (_freeList.Length > 0)
            {
                index = _freeList[_freeList.Length - 1];
                _freeList.RemoveAt(_freeList.Length - 1);
            }
            else
            {
                int highWater = HighWater;
                if (highWater >= Capacity)
                {
                    throw new InvalidOperationException(
                        $"BrickPool exhausted at capacity {Capacity}. The streaming layer " +
                        "must evict regions before the pool fills; reaching this point means " +
                        "eviction is not keeping pace (see IsUnderPressure).");
                }

                index = highWater;
                HighWater = highWater + 1;
            }

            ClearBrick(index);
            return index;
        }

        /// <summary>
        /// Return a slot to the pool.
        ///
        /// Called whenever a brick becomes uniform (see <see cref="VoxelAccess"/>).
        /// Skipping that collapse is the slow leak this design is most susceptible to:
        /// nothing breaks, memory simply climbs over a long session, which is the most
        /// expensive place to find a defect.
        /// </summary>
        public void Free(int brickIndex)
        {
            if ((uint)brickIndex >= (uint)HighWater)
                throw new ArgumentOutOfRangeException(nameof(brickIndex));

            _freeList.Add(brickIndex);
        }

        public void ClearBrick(int brickIndex)
        {
            var vo = VoxelOffset(brickIndex);
            for (var i = 0; i < VoxelDimensions.VoxelsPerBrick; i++)
            {
                Voxels[vo + i] = VoxelDimensions.MaterialEmpty;
                SurfaceSemantics[vo + i] = 0;
                BoundarySamples[vo + i] = 0;
            }

            var oo = OccupancyOffset(brickIndex);
            for (var i = 0; i < VoxelDimensions.OccupancyWordsPerBrick; i++)
                Occupancy[oo + i] = 0UL;

        }

        /// <summary>Fill a freshly allocated brick with a single material, e.g. when a uniform brick is being split.</summary>
        public void FillBrick(int brickIndex, byte material)
        {
            var vo = VoxelOffset(brickIndex);
            for (var i = 0; i < VoxelDimensions.VoxelsPerBrick; i++)
            {
                Voxels[vo + i] = material;
                SurfaceSemantics[vo + i] = 0;
                BoundarySamples[vo + i] = 0;
            }

            var oo = OccupancyOffset(brickIndex);
            var occupied = material != VoxelDimensions.MaterialEmpty;
            for (var i = 0; i < VoxelDimensions.OccupancyWordsPerBrick; i++)
                Occupancy[oo + i] = occupied ? ulong.MaxValue : 0UL;

        }

        /// <summary>Fills a mixed slot with one logical cell value, retaining its semantics.</summary>
        public void FillBrick(int brickIndex, in VoxelCell cell)
        {
            int vo = VoxelOffset(brickIndex);
            ushort surface = cell.IsSolid ? cell.Surface.PackedStorage : (ushort)0;
            for (int i = 0; i < VoxelDimensions.VoxelsPerBrick; i++)
            {
                Voxels[vo + i] = cell.BaseMaterialId;
                SurfaceSemantics[vo + i] = surface;
                BoundarySamples[vo + i] = cell.Boundary.Packed;
            }

            int oo = OccupancyOffset(brickIndex);
            for (int i = 0; i < VoxelDimensions.OccupancyWordsPerBrick; i++)
                Occupancy[oo + i] = cell.IsSolid ? ulong.MaxValue : 0UL;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte GetVoxel(int brickIndex, int voxelIndex) =>
            Voxels[VoxelOffset(brickIndex) + voxelIndex];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VoxelSurfaceSemantics GetSurface(int brickIndex, int voxelIndex) =>
            VoxelSurfaceSemantics.FromStorage(SurfaceSemantics[VoxelOffset(brickIndex) + voxelIndex]);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VoxelBoundarySample GetBoundary(int brickIndex, int voxelIndex) => new()
        {
            Packed = BoundarySamples[VoxelOffset(brickIndex) + voxelIndex]
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetVoxel(int brickIndex, int voxelIndex, byte material)
        {
            int offset = VoxelOffset(brickIndex) + voxelIndex;
            Voxels[offset] = material;
            // Material-only writes preserve independent surface state while occupied. Destruction
            // clears it so curvature/coatings cannot remain attached to empty space.
            if (material == VoxelDimensions.MaterialEmpty)
            {
                SurfaceSemantics[offset] = 0;
                BoundarySamples[offset] = 0;
            }
            var occ = Occupancy;
            OccupancyMask.Set(ref occ, OccupancyOffset(brickIndex), voxelIndex,
                              material != VoxelDimensions.MaterialEmpty);

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetCell(int brickIndex, int voxelIndex, in VoxelCell cell)
        {
            int offset = VoxelOffset(brickIndex) + voxelIndex;
            Voxels[offset] = cell.BaseMaterialId;
            SurfaceSemantics[offset] = cell.IsSolid ? cell.Surface.PackedStorage : (ushort)0;
            BoundarySamples[offset] = cell.Boundary.Packed;
            var occ = Occupancy;
            OccupancyMask.Set(ref occ, OccupancyOffset(brickIndex), voxelIndex, cell.IsSolid);
        }

        /// <summary>
        /// Returns the single material filling this brick, or false if it is mixed.
        /// Drives the collapse-to-uniform check after every edit.
        /// </summary>
        public bool TryGetUniformMaterial(int brickIndex, out byte material)
        {
            var vo = VoxelOffset(brickIndex);
            var first = Voxels[vo];

            for (var i = 1; i < VoxelDimensions.VoxelsPerBrick; i++)
            {
                if (Voxels[vo + i] != first)
                {
                    material = 0;
                    return false;
                }
            }

            // BrickRef can encode only a uniform material. Any surface override therefore keeps
            // the brick mixed even when every material byte is identical.
            for (var i = 0; i < VoxelDimensions.VoxelsPerBrick; i++)
            {
                if (SurfaceSemantics[vo + i] != 0)
                {
                    material = 0;
                    return false;
                }
                if (BoundarySamples[vo + i] != 0)
                {
                    material = 0;
                    return false;
                }
            }

            material = first;
            return true;
        }

        public void Dispose()
        {
            if (Voxels.IsCreated) Voxels.Dispose();
            if (SurfaceSemantics.IsCreated) SurfaceSemantics.Dispose();
            if (BoundarySamples.IsCreated) BoundarySamples.Dispose();
            if (Occupancy.IsCreated) Occupancy.Dispose();
            if (_freeList.IsCreated) _freeList.Dispose();
            if (_highWaterState.IsCreated) _highWaterState.Dispose();
            Capacity = 0;
        }
    }
}
