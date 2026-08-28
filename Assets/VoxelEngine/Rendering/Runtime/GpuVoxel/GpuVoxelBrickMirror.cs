using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Rendering.Runtime.GpuVoxel
{
    public enum GpuBrickPublish
    {
        Uploaded = 0,
        AlreadyResident = 1,
        MetadataOnly = 2,
        NoSlot = 3,
        Stale = 4,
        PayloadMissing = 5,
    }

    /// <summary>
    /// Persistent GPU copy of authoritative voxel bricks plus a GPU-readable world-coordinate
    /// directory. Payload is updated from Storage deltas; chunk extraction never owns publication.
    /// </summary>
    public sealed class GpuVoxelBrickMirror : IDisposable
    {
        public const uint PersistentLookupMagic = 0x47505542u; // 'GPUB'
        public const int DirectoryWordsPerEntry = 5;
        private const uint DirectoryOccupied = 1u;
        private const uint DirectoryTombstone = 2u;

        public struct BrickMetadata
        {
            public int Slot;
            public uint Content;
            public uint UniformMaterial;
            public uint Flags;
            public const int Stride = sizeof(int) + sizeof(uint) * 3;
        }

        private readonly GpuBrickSlotTable _slots;
        private readonly ComputeBuffer _materials;
        private readonly ComputeBuffer _surfaceSemantics;
        private readonly ComputeBuffer _boundarySamples;
        private readonly ComputeBuffer _occupancy;
        private readonly ComputeBuffer _metadata;

        private NativeArray<uint> _materialStaging;
        private NativeArray<uint> _surfaceStaging;
        private NativeArray<uint> _boundaryStaging;
        private NativeArray<BrickMetadata> _metadataStaging;
        private NativeArray<uint> _directoryStaging;
        private readonly bool[] _dirtySlots;
        private int _dirtyMin = int.MaxValue;
        private int _dirtyMax = -1;
        private bool _directoryDirty;
        private bool _disposed;

        public int SlotCapacity { get; }
        public int DirectoryCapacity { get; }
        public int DirectoryMask => DirectoryCapacity - 1;
        public int DirectoryWordOffset { get; }
        public int ResidentBricks => _slots.ResidentCount;
        public long CommittedBytes { get; }

        public ulong UploadedBricks { get; private set; }
        public ulong UploadedBytes { get; private set; }
        public ulong SkippedAlreadyResident { get; private set; }
        public ulong RefusedNoSlot => _slots.RefusedCount;
        public ulong RejectedStale => _slots.StaleCount;
        public ulong Evictions => _slots.EvictionCount;
        public ulong DirectoryRefusals { get; private set; }

        public ComputeBuffer Materials => FlushAndGet(_materials);
        public ComputeBuffer SurfaceSemantics => FlushAndGet(_surfaceSemantics);
        public ComputeBuffer BoundarySamples => FlushAndGet(_boundarySamples);
        public ComputeBuffer Occupancy => FlushAndGet(_occupancy);
        public ComputeBuffer Metadata => FlushAndGet(_metadata);

        public GpuVoxelBrickMirror(int slotCapacity)
        {
            if (slotCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(slotCapacity));

            SlotCapacity = slotCapacity;
            DirectoryCapacity = NextPowerOfTwo(Math.Max(1024, slotCapacity * 4));
            DirectoryWordOffset = slotCapacity * GpuBrickBufferLayout.MaterialWordsPerBrick;
            long directoryBytes = (long)DirectoryCapacity * DirectoryWordsPerEntry * sizeof(uint);
            CommittedBytes = GpuBrickBufferLayout.CommittedBytes(slotCapacity) + directoryBytes;
            _slots = new GpuBrickSlotTable(slotCapacity);

            _materials = new ComputeBuffer(
                DirectoryWordOffset + DirectoryCapacity * DirectoryWordsPerEntry,
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
                DirectoryWordOffset, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            _surfaceStaging = new NativeArray<uint>(
                slotCapacity * GpuBrickBufferLayout.SurfaceWordsPerBrick,
                Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            _boundaryStaging = new NativeArray<uint>(
                slotCapacity * GpuBrickBufferLayout.BoundaryWordsPerBrick,
                Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            _metadataStaging = new NativeArray<BrickMetadata>(
                slotCapacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            _directoryStaging = new NativeArray<uint>(
                DirectoryCapacity * DirectoryWordsPerEntry,
                Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _dirtySlots = new bool[slotCapacity];
        }

        public bool TryGetSlot(int3 coordinate, out int slot) => _slots.TryGetSlot(coordinate, out slot);
        public bool Pin(int3 coordinate) => _slots.Pin(coordinate);
        public bool Unpin(int3 coordinate) => _slots.Unpin(coordinate);
        public void Touch(int3 coordinate) => _slots.Touch(coordinate);

        public GpuBrickPublish Publish(in VoxelBrickDelta delta, in PinnedVoxelReadBlock payload)
        {
            bool hasPayload = payload.Kind == VoxelReadBlockKind.Mixed
                           && payload.MixedVoxels.IsCreated
                           && payload.MixedSurfaceSemantics.IsCreated
                           && payload.MixedBoundarySamples.IsCreated;
            return Publish(delta, payload.MixedVoxels, payload.MixedSurfaceSemantics,
                           payload.MixedBoundarySamples, payload.MixedOffset, hasPayload);
        }

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
                    if (!PublishLookup(delta, -1)) return GpuBrickPublish.NoSlot;
                    return GpuBrickPublish.MetadataOnly;
                case GpuBrickAdmission.Resident:
                    if (!_slots.TryGetSlot(delta.Coordinate, out slot)
                        || !PublishLookup(delta, slot))
                        return GpuBrickPublish.NoSlot;
                    SkippedAlreadyResident++;
                    return GpuBrickPublish.AlreadyResident;
            }

            if (!hasPayload)
            {
                _slots.Release(delta.Coordinate);
                RemoveLookup(delta.Coordinate);
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

            if (!PublishLookup(delta, slot))
            {
                _slots.Release(delta.Coordinate);
                return GpuBrickPublish.NoSlot;
            }

            UploadedBricks++;
            UploadedBytes += (ulong)GpuBrickBufferLayout.BytesPerMixedBrick;
            return GpuBrickPublish.Uploaded;
        }

        /// <summary>Removes a world coordinate from GPU lookup and releases its mixed payload slot.</summary>
        public void Remove(int3 coordinate)
        {
            ThrowIfDisposed();
            _slots.Release(coordinate);
            RemoveLookup(coordinate);
        }

        private bool PublishLookup(in VoxelBrickDelta delta, int slot)
        {
            int index = FindDirectoryIndex(delta.Coordinate, forInsert: true);
            if (index < 0)
            {
                DirectoryRefusals++;
                return false;
            }

            int word = index * DirectoryWordsPerEntry;
            _directoryStaging[word + 0] = unchecked((uint)delta.Coordinate.x);
            _directoryStaging[word + 1] = unchecked((uint)delta.Coordinate.y);
            _directoryStaging[word + 2] = unchecked((uint)delta.Coordinate.z);
            _directoryStaging[word + 3] = GpuSurfaceExtractor.PackBrickCacheEntry(
                delta.Content, delta.UniformMaterial, slot);
            _directoryStaging[word + 4] = DirectoryOccupied;
            _directoryDirty = true;
            return true;
        }

        private void RemoveLookup(int3 coordinate)
        {
            int index = FindDirectoryIndex(coordinate, forInsert: false);
            if (index < 0) return;
            _directoryStaging[index * DirectoryWordsPerEntry + 4] = DirectoryTombstone;
            _directoryDirty = true;
        }

        private int FindDirectoryIndex(int3 coordinate, bool forInsert)
        {
            int firstTombstone = -1;
            int index = (int)(HashCoordinate(coordinate) & (uint)DirectoryMask);
            for (int probe = 0; probe < DirectoryCapacity; probe++)
            {
                int candidate = (index + probe) & DirectoryMask;
                int word = candidate * DirectoryWordsPerEntry;
                uint state = _directoryStaging[word + 4];
                if (state == 0u)
                    return forInsert ? (firstTombstone >= 0 ? firstTombstone : candidate) : -1;
                if (state == DirectoryTombstone)
                {
                    if (forInsert && firstTombstone < 0) firstTombstone = candidate;
                    continue;
                }
                if (unchecked((int)_directoryStaging[word + 0]) == coordinate.x
                    && unchecked((int)_directoryStaging[word + 1]) == coordinate.y
                    && unchecked((int)_directoryStaging[word + 2]) == coordinate.z)
                    return candidate;
            }
            return forInsert ? firstTombstone : -1;
        }

        public static uint HashCoordinate(int3 coordinate)
        {
            unchecked
            {
                uint h = (uint)coordinate.x * 0x8da6b343u;
                h ^= (uint)coordinate.y * 0xd8163841u;
                h ^= (uint)coordinate.z * 0xcb1ab31fu;
                h ^= h >> 16;
                h *= 0x7feb352du;
                h ^= h >> 15;
                return h;
            }
        }

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
            if (slot < 0) return;
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

        private void FlushPendingUploads()
        {
            if (_dirtyMax >= _dirtyMin)
            {
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
                    _materials.SetData(_materialStaging, materialOffset, materialOffset,
                        slotCount * GpuBrickBufferLayout.MaterialWordsPerBrick);
                    _surfaceSemantics.SetData(_surfaceStaging, surfaceOffset, surfaceOffset,
                        slotCount * GpuBrickBufferLayout.SurfaceWordsPerBrick);
                    _boundarySamples.SetData(_boundaryStaging, boundaryOffset, boundaryOffset,
                        slotCount * GpuBrickBufferLayout.BoundaryWordsPerBrick);
                    _metadata.SetData(_metadataStaging, first, first, slotCount);
                }
                _dirtyMin = int.MaxValue;
                _dirtyMax = -1;
            }

            if (_directoryDirty)
            {
                _materials.SetData(_directoryStaging, 0, DirectoryWordOffset,
                                   _directoryStaging.Length);
                _directoryDirty = false;
            }
        }

        private static int NextPowerOfTwo(int value)
        {
            int result = 1;
            while (result < value && result < (1 << 30)) result <<= 1;
            return result;
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
            if (_directoryStaging.IsCreated) _directoryStaging.Dispose();
            _slots.Clear();
        }
    }
}
