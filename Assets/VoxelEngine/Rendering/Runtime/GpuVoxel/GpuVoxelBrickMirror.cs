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
        private readonly BrickMetadata[] _metadataStaging;
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

        public ComputeBuffer Materials => _materials;
        public ComputeBuffer SurfaceSemantics => _surfaceSemantics;
        public ComputeBuffer BoundarySamples => _boundarySamples;
        public ComputeBuffer Occupancy => _occupancy;
        public ComputeBuffer Metadata => _metadata;

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
            _metadataStaging = new BrickMetadata[1];
        }

        public bool TryGetSlot(int3 coordinate, out int slot) => _slots.TryGetSlot(coordinate, out slot);
        public bool Pin(int3 coordinate) => _slots.Pin(coordinate);
        public bool Unpin(int3 coordinate) => _slots.Unpin(coordinate);
        public void Touch(int3 coordinate) => _slots.Touch(coordinate);

        /// <summary>
        /// Publishes one brick.
        ///
        /// <paramref name="payload"/> is only read for a mixed brick, and is copied straight from
        /// the pinned block's arrays without a staging pass: the authoritative channels are already
        /// laid out the way the mirror wants them, so the payload is reinterpreted as 32-bit words
        /// in place. Copying 2 KB per brick through an intermediate buffer would double the cost of
        /// the one operation this whole type exists to make cheap.
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

            UploadChannel(_materials, voxels, elementOffset,
                          GpuBrickBufferLayout.VoxelsPerBrick,
                          GpuBrickBufferLayout.MaterialWordOffset(slot));
            UploadChannel(_surfaceSemantics, surfaceSemantics, elementOffset,
                          GpuBrickBufferLayout.VoxelsPerBrick,
                          GpuBrickBufferLayout.SurfaceWordOffset(slot));
            UploadChannel(_boundarySamples, boundarySamples, elementOffset,
                          GpuBrickBufferLayout.VoxelsPerBrick,
                          GpuBrickBufferLayout.BoundaryWordOffset(slot));

            WriteMetadata(delta, slot);

            UploadedBricks++;
            UploadedBytes += (ulong)GpuBrickBufferLayout.BytesPerMixedBrick;
            return GpuBrickPublish.Uploaded;
        }

        /// <summary>
        /// Copies one brick's worth of a channel, reinterpreting the source as 32-bit words.
        ///
        /// Blocks are 512 elements and start on a block boundary, so every offset divides evenly
        /// into words and no realignment is ever needed.
        /// </summary>
        private static void UploadChannel<T>(ComputeBuffer destination, NativeArray<T> source,
                                             int elementOffset, int elementCount, int wordOffset)
            where T : struct
        {
            NativeArray<T> block = source.GetSubArray(elementOffset, elementCount);
            NativeArray<uint> words = block.Reinterpret<uint>(UnsafeElementSize<T>());
            destination.SetData(words, 0, wordOffset, words.Length);
        }

        private static int UnsafeElementSize<T>() where T : struct =>
            Unity.Collections.LowLevel.Unsafe.UnsafeUtility.SizeOf<T>();

        private void WriteMetadata(in VoxelBrickDelta delta, int slot)
        {
            if (slot < 0 && !_slots.TryGetSlot(delta.Coordinate, out slot)) slot = -1;
            if (slot < 0) return;   // nothing addressable to describe yet

            _metadataStaging[0] = new BrickMetadata
            {
                Slot = slot,
                Content = (uint)delta.Content,
                UniformMaterial = delta.UniformMaterial,
                Flags = delta.HasSolid ? 1u : 0u,
            };
            _metadata.SetData(_metadataStaging, 0, slot, 1);
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
            _slots.Clear();
        }
    }
}
