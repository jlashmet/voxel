using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Rendering.Runtime.GpuVoxel
{
    /// <summary>Outcome of publishing one brick into the mirror.</summary>
    public enum GpuBrickPublish
    {
        /// <summary>Payload uploaded into a slot.</summary>
        Uploaded = 0,

        /// <summary>Already resident at this generation. Nothing was sent.</summary>
        AlreadyResident = 1,

        /// <summary>Empty or uniform: recorded in metadata, no payload exists to send.</summary>
        MetadataOnly = 2,

        /// <summary>Every slot is pinned. The caller should retry once coverage releases one.</summary>
        NoSlot = 3,

        /// <summary>Older than what the slot holds; publishing would move backwards.</summary>
        Stale = 4,

        /// <summary>The delta claims a payload the supplied read block does not carry.</summary>
        PayloadMissing = 5,
    }

    /// <summary>
    /// The GPU's copy of authoritative voxel bricks.
    ///
    /// This is the foundation the compute mesher stands on: instead of the CPU extracting geometry
    /// and uploading vertices, the CPU uploads *voxels* and the GPU extracts. The distinction is the
    /// whole point of the migration — voxel payload is bounded by what actually changed, whereas
    /// generated geometry is bounded by surface area and has to be regenerated wholesale whenever a
    /// chunk is touched.
    ///
    /// Only mixed bricks occupy a slot. Empty and uniform bricks are recorded in the metadata buffer
    /// and cost nothing else, matching how storage already treats them.
    ///
    /// Nothing here reads back. Uploads go one way, and the geometry the mesher derives stays
    /// GPU-resident, per the plan's no-readback invariant.
    /// </summary>
    public sealed class GpuVoxelBrickMirror : IDisposable
    {
        /// <summary>
        /// Per-brick metadata as the GPU sees it. Four words, so the lookup a ray or mesher does
        /// first is a single aligned fetch.
        /// </summary>
        public struct BrickMetadata
        {
            public int Slot;             // -1 when the brick carries no payload
            public uint Content;         // VoxelBrickContent
            public uint UniformMaterial;
            public uint Flags;           // bit 0: has any solid voxel

            public const int Stride = sizeof(int) + sizeof(uint) * 3;
        }

        private readonly GpuBrickSlotTable _slots;
        private readonly ComputeBuffer _materials;
        private readonly ComputeBuffer _surfaceSemantics;
        private readonly ComputeBuffer _boundarySamples;
        private readonly ComputeBuffer _occupancy;
        private readonly ComputeBuffer _metadata;

        // SetData on Metal is not just a memcpy: each call can synchronize the resource with the
        // driver. A chunk can publish hundreds of mixed bricks, so issuing three payload writes plus
        // metadata per brick turns one admission into hundreds of tiny synchronous GPU uploads.
        // Own a bounded CPU mirror of the slots instead. Publish copies into it immediately (so the
        // caller may release its pinned source), and the first GPU consumer coalesces adjacent dirty
        // slots into bulk writes. Production sizes this mirror to exactly BrickCacheCount slots.
        private readonly NativeArray<uint> _materialStaging;
        private readonly NativeArray<uint> _surfaceStaging;
        private readonly NativeArray<uint> _boundaryStaging;
        private readonly NativeArray<BrickMetadata> _metadataStaging;
        private readonly bool[] _dirtySlots;
        private int _dirtyMin = int.MaxValue;
        private int _dirtyMax = -1;
        private bool _disposed;

        public int SlotCapacity { get; }
        public int ResidentBricks => _slots.ResidentCount;
        public long CommittedBytes { get; }

        public ulong UploadedBricks { get; private set; }
        public ulong UploadedBytes { get; private set; }
        public ulong SkippedAlreadyResident { get; private set; }
        public ulong RefusedNoSlot => _slots.RefusedCount;
        public ulong RejectedStale => _slots.StaleCount;
        public ulong Evictions => _slots.EvictionCount;

        public ComputeBuffer Materials => FlushAndGet(_materials);
        public ComputeBuffer SurfaceSemantics => FlushAndGet(_surfaceSemantics);
        public ComputeBuffer BoundarySamples => FlushAndGet(_boundarySamples);
        public ComputeBuffer Occupancy => FlushAndGet(_occupancy);
        public ComputeBuffer Metadata => FlushAndGet(_metadata);

        public GpuVoxelBrickMirror(int slotCapacity)
        {
            if (slotCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(slotCapacity));

            SlotCapacity = slotCapacity;
            CommittedBytes = GpuBrickBufferLayout.CommittedBytes(slotCapacity);
            _slots = new GpuBrickSlotTable(slotCapacity);

            // Allocated once and never resized. A mirror that grew on demand would reallocate
            // hundreds of megabytes mid-frame at exactly the moment the world is busiest.
            _materials = new ComputeBuffer(
                slotCapacity * GpuBrickBufferLayout.MaterialWordsPerBrick,
                sizeof(uint), ComputeBufferType.Structured);
            _surfaceSemantics = new ComputeBuffer(
                slotCapacity * GpuBrickBufferLayout.SurfaceWordsPerBrick,
                sizeof(uint), ComputeBufferType.Structured);
            _boundarySamples = new ComputeBuffer(
                slotCapacity * GpuBrickBufferLayout.BoundaryWordsPerBrick,
                sizeof(uint), ComputeBufferType.Structured);
            _occupancy = new ComputeBuffer(
                slotCapacity * GpuBrickBufferLayout.OccupancyGpuWordsPerBrick,
                sizeof(uint), ComputeBufferType.Structured);
            _metadata = new ComputeBuffer(slotCapacity, BrickMetadata.Stride,
                                          ComputeBufferType.Structured);

            _materialStaging = new NativeArray<uint>(
                slotCapacity * GpuBrickBufferLayout.MaterialWordsPerBrick,
                Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            _surfaceStaging = new NativeArray<uint>(
                slotCapacity * GpuBrickBufferLayout.SurfaceWordsPerBrick,
                Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            _boundaryStaging = new NativeArray<uint>(
                slotCapacity * GpuBrickBufferLayout.BoundaryWordsPerBrick,
                Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            _metadataStaging = new NativeArray<BrickMetadata>(
                slotCapacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            _dirtySlots = new bool[slotCapacity];
        }

        public bool TryGetSlot(int3 coordinate, out int slot) => _slots.TryGetSlot(coordinate, out slot);
        public bool Pin(int3 coordinate) => _slots.Pin(coordinate);
        public bool Unpin(int3 coordinate) => _slots.Unpin(coordinate);
        public void Touch(int3 coordinate) => _slots.Touch(coordinate);

        /// <summary>
        /// Publishes one brick.
        ///
        /// <paramref name="payload"/> is only read for a mixed brick. Its three channels are copied
        /// into mirror-owned staging at the destination slot immediately, so the caller's pinned
        /// block can be released as soon as publication returns. The GPU transfer is deliberately
        /// deferred until a consumer requests one of the mirror buffers, where adjacent dirty slots
        /// are uploaded in bulk rather than as hundreds of tiny driver calls.
        /// </summary>
        public GpuBrickPublish Publish(in VoxelBrickDelta delta, in PinnedVoxelReadBlock payload)
        {
            bool hasPayload = payload.Kind == VoxelReadBlockKind.Mixed
                           && payload.MixedVoxels.IsCreated
                           && payload.MixedSurfaceSemantics.IsCreated
                           && payload.MixedBoundarySamples.IsCreated;
            return Publish(delta,
                           payload.MixedVoxels, payload.MixedSurfaceSemantics,
                           payload.MixedBoundarySamples, payload.MixedOffset, hasPayload);
        }

        /// <summary>
        /// Publishes from raw channel arrays rather than a pinned block, for callers that hold
        /// storage directly. The pinned overload delegates here; the two must not diverge.
        /// </summary>
        public GpuBrickPublish Publish(in VoxelBrickDelta delta,
                                       NativeArray<byte> voxels,
                                       NativeArray<ushort> surfaceSemantics,
                                       NativeArray<byte> boundarySamples,
                                       int elementOffset,
                                       bool hasPayload)
        {
            ThrowIfDisposed();

            GpuBrickAdmission admission = _slots.TryAdmit(in delta, out int slot);
            switch (admission)
            {
                case GpuBrickAdmission.Stale:
                    return GpuBrickPublish.Stale;
                case GpuBrickAdmission.Full:
                    return GpuBrickPublish.NoSlot;
                case GpuBrickAdmission.NoPayload:
                    WriteMetadata(delta, slot: -1);
                    return GpuBrickPublish.MetadataOnly;
                case GpuBrickAdmission.Resident:
                    SkippedAlreadyResident++;
                    return GpuBrickPublish.AlreadyResident;
            }

            if (!hasPayload)
            {
                // The slot was admitted on the delta's word; without the payload it would hold
                // whatever the previous tenant left. Release it rather than publish a lie.
                _slots.Release(delta.Coordinate);
                return GpuBrickPublish.PayloadMissing;
            }

            StageChannel(_materialStaging, voxels, elementOffset,
                         GpuBrickBufferLayout.VoxelsPerBrick,
                         GpuBrickBufferLayout.MaterialWordOffset(slot));
            StageChannel(_surfaceStaging, surfaceSemantics, elementOffset,
                         GpuBrickBufferLayout.VoxelsPerBrick,
                         GpuBrickBufferLayout.SurfaceWordOffset(slot));
            StageChannel(_boundaryStaging, boundarySamples, elementOffset,
                         GpuBrickBufferLayout.VoxelsPerBrick,
                         GpuBrickBufferLayout.BoundaryWordOffset(slot));

            WriteMetadata(delta, slot);

            UploadedBricks++;
            UploadedBytes += (ulong)GpuBrickBufferLayout.BytesPerMixedBrick;
            return GpuBrickPublish.Uploaded;
        }

        /// <summary>
        /// Copies one brick's worth of a channel into the CPU staging mirror, reinterpreting the
        /// source as 32-bit words. Blocks start on a block boundary, so every offset is word-aligned.
        /// </summary>
        private static void StageChannel<T>(NativeArray<uint> destination, NativeArray<T> source,
                                            int elementOffset, int elementCount, int wordOffset)
            where T : struct
        {
            NativeArray<T> block = source.GetSubArray(elementOffset, elementCount);
            NativeArray<uint> words = block.Reinterpret<uint>(UnsafeElementSize<T>());
            NativeArray<uint>.Copy(words, 0, destination, wordOffset, words.Length);
        }

        private static int UnsafeElementSize<T>() where T : struct =>
            Unity.Collections.LowLevel.Unsafe.UnsafeUtility.SizeOf<T>();

        private void WriteMetadata(in VoxelBrickDelta delta, int slot)
        {
            if (slot < 0 && !_slots.TryGetSlot(delta.Coordinate, out slot)) slot = -1;
            if (slot < 0) return;   // nothing addressable to describe yet

            _metadataStaging[slot] = new BrickMetadata
            {
                Slot = slot,
                Content = (uint)delta.Content,
                UniformMaterial = delta.UniformMaterial,
                Flags = delta.HasSolid ? 1u : 0u,
            };
            MarkDirty(slot);
        }

        private void MarkDirty(int slot)
        {
            _dirtySlots[slot] = true;
            if (slot < _dirtyMin) _dirtyMin = slot;
            if (slot > _dirtyMax) _dirtyMax = slot;
        }

        private ComputeBuffer FlushAndGet(ComputeBuffer buffer)
        {
            ThrowIfDisposed();
            FlushPendingUploads();
            return buffer;
        }

        /// <summary>
        /// Flushes adjacent dirty slots as contiguous buffer ranges. Fresh admissions use ascending
        /// slots, so the common chunk publication collapses from four GPU writes per mixed brick to
        /// four writes for the whole run. Fragmented eviction still remains correct and bounded by
        /// the number of dirty ranges rather than the number of bricks.
        /// </summary>
        private void FlushPendingUploads()
        {
            if (_dirtyMax < _dirtyMin) return;

            int slot = _dirtyMin;
            while (slot <= _dirtyMax)
            {
                while (slot <= _dirtyMax && !_dirtySlots[slot]) slot++;
                if (slot > _dirtyMax) break;

                int first = slot;
                while (slot <= _dirtyMax && _dirtySlots[slot])
                {
                    _dirtySlots[slot] = false;
                    slot++;
                }

                int slotCount = slot - first;
                int materialOffset = GpuBrickBufferLayout.MaterialWordOffset(first);
                int surfaceOffset = GpuBrickBufferLayout.SurfaceWordOffset(first);
                int boundaryOffset = GpuBrickBufferLayout.BoundaryWordOffset(first);

                _materials.SetData(
                    _materialStaging, materialOffset, materialOffset,
                    slotCount * GpuBrickBufferLayout.MaterialWordsPerBrick);
                _surfaceSemantics.SetData(
                    _surfaceStaging, surfaceOffset, surfaceOffset,
                    slotCount * GpuBrickBufferLayout.SurfaceWordsPerBrick);
                _boundarySamples.SetData(
                    _boundaryStaging, boundaryOffset, boundaryOffset,
                    slotCount * GpuBrickBufferLayout.BoundaryWordsPerBrick);
                _metadata.SetData(_metadataStaging, first, first, slotCount);
            }

            _dirtyMin = int.MaxValue;
            _dirtyMax = -1;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(GpuVoxelBrickMirror));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _materials?.Release();
            _surfaceSemantics?.Release();
            _boundarySamples?.Release();
            _occupancy?.Release();
            _metadata?.Release();
            if (_materialStaging.IsCreated) _materialStaging.Dispose();
            if (_surfaceStaging.IsCreated) _surfaceStaging.Dispose();
            if (_boundaryStaging.IsCreated) _boundaryStaging.Dispose();
            if (_metadataStaging.IsCreated) _metadataStaging.Dispose();
            _slots.Clear();
        }
    }
}