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
        private const int DirectoryDeltaWords = 6; // entry index + final five directory words
        private const int DirectoryDeltaCapacity = 4096;
        // One recovery slice publishes at most 64 mixed bricks. Keep payload transfer bounded to
        // that same unit so slot fragmentation cannot multiply driver calls inside one Prepare.
        private const int PayloadDeltaCapacity = 64;
        private const int PayloadMaterialOffset = 1;
        private const int PayloadSurfaceOffset =
            PayloadMaterialOffset + GpuBrickBufferLayout.MaterialWordsPerBrick;
        private const int PayloadBoundaryOffset =
            PayloadSurfaceOffset + GpuBrickBufferLayout.SurfaceWordsPerBrick;
        private const int PayloadMetadataOffset =
            PayloadBoundaryOffset + GpuBrickBufferLayout.BoundaryWordsPerBrick;
        private const int PayloadDeltaWords = PayloadMetadataOffset + 4;
        private const int PayloadCopyThreadsPerBrick =
            GpuBrickBufferLayout.MaterialWordsPerBrick
          + GpuBrickBufferLayout.SurfaceWordsPerBrick
          + GpuBrickBufferLayout.BoundaryWordsPerBrick + 1;
        private const uint DirectoryOccupied = 1u;
        private const uint DirectoryTombstone = 2u;
        private const string DirectoryUpdaterResourcePath = "VoxelBrickDirectoryUpdater";
        private const int ThreadGroupSize = 64;

        private static readonly int IdBrickMaterials = Shader.PropertyToID("_BrickMaterials");
        private static readonly int IdBrickSurfaceSemantics =
            Shader.PropertyToID("_BrickSurfaceSemantics");
        private static readonly int IdBrickBoundarySamples =
            Shader.PropertyToID("_BrickBoundarySamples");
        private static readonly int IdBrickMetadata = Shader.PropertyToID("_BrickMetadata");
        private static readonly int IdDirectoryDeltas = Shader.PropertyToID("_DirectoryDeltas");
        private static readonly int IdPayloadDeltas = Shader.PropertyToID("_PayloadDeltas");
        private static readonly int IdDirectoryWordOffset = Shader.PropertyToID("_DirectoryWordOffset");
        private static readonly int IdDirectoryCapacity = Shader.PropertyToID("_DirectoryCapacity");
        private static readonly int IdDirectoryDeltaCount = Shader.PropertyToID("_DirectoryDeltaCount");
        private static readonly int IdPayloadDeltaCount = Shader.PropertyToID("_PayloadDeltaCount");

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
        private readonly ComputeBuffer _directoryDeltas;
        private readonly ComputeBuffer _payloadDeltas;
        private readonly ComputeShader _directoryUpdater;
        private readonly int _applyDirectoryKernel;
        private readonly int _applyPayloadKernel;
        private readonly int _clearDirectoryKernel;

        private NativeArray<uint> _materialStaging;
        private NativeArray<uint> _surfaceStaging;
        private NativeArray<uint> _boundaryStaging;
        private NativeArray<BrickMetadata> _metadataStaging;
        private NativeArray<uint> _directoryStaging;
        private NativeArray<uint> _directoryDeltaStaging;
        private NativeArray<uint> _payloadDeltaStaging;
        private readonly bool[] _dirtySlots;
        private readonly bool[] _dirtyDirectoryEntries;
        private int _dirtyMin = int.MaxValue;
        private int _dirtyMax = -1;
        private int _directoryDirtyMin = int.MaxValue;
        private int _directoryDirtyMax = -1;
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
        public ulong DirectoryUploadBytes { get; private set; }
        public ulong DirectoryUploadBatches { get; private set; }
        public ulong PayloadUploadBytes { get; private set; }
        public ulong PayloadUploadBatches { get; private set; }

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
            long payloadDeltaBytes =
                (long)PayloadDeltaCapacity * PayloadDeltaWords * sizeof(uint);
            CommittedBytes = GpuBrickBufferLayout.CommittedBytes(slotCapacity) + directoryBytes
                           + (long)DirectoryDeltaCapacity * DirectoryDeltaWords * sizeof(uint)
                           + payloadDeltaBytes;
            _slots = new GpuBrickSlotTable(slotCapacity);

            _directoryUpdater = Resources.Load<ComputeShader>(DirectoryUpdaterResourcePath);
            if (_directoryUpdater == null)
                throw new InvalidOperationException(
                    $"Missing Resources/{DirectoryUpdaterResourcePath}.compute for GPU brick mirror.");
            _applyDirectoryKernel = _directoryUpdater.FindKernel("CSApplyDirectoryDeltas");
            _applyPayloadKernel = _directoryUpdater.FindKernel("CSApplyPayloadDeltas");
            _clearDirectoryKernel = _directoryUpdater.FindKernel("CSClearDirectory");

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
            _directoryDeltas = new ComputeBuffer(
                DirectoryDeltaCapacity * DirectoryDeltaWords,
                sizeof(uint), ComputeBufferType.Structured);
            _payloadDeltas = new ComputeBuffer(
                PayloadDeltaCapacity * PayloadDeltaWords,
                sizeof(uint), ComputeBufferType.Structured);

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
            _directoryDeltaStaging = new NativeArray<uint>(
                DirectoryDeltaCapacity * DirectoryDeltaWords,
                Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            _payloadDeltaStaging = new NativeArray<uint>(
                PayloadDeltaCapacity * PayloadDeltaWords,
                Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            _dirtySlots = new bool[slotCapacity];
            _dirtyDirectoryEntries = new bool[DirectoryCapacity];

            // ComputeBuffer contents are not specified on allocation. Clear the hash states once on
            // the GPU so an uninitialised word can never masquerade as a live directory entry.
            ClearGpuDirectory();
        }

        public bool TryGetSlot(int3 coordinate, out int slot) => _slots.TryGetSlot(coordinate, out slot);
        public bool Pin(int3 coordinate) => _slots.Pin(coordinate);
        public bool Unpin(int3 coordinate) => _slots.Unpin(coordinate);
        public void Touch(int3 coordinate) => _slots.Touch(coordinate);

        /// <summary>
        /// Drops all logical residency without reallocating the large GPU buffers. Payload words may
        /// remain physically present, but the GPU directory is cleared so they are unreachable.
        /// This is a world/recovery boundary, never a chunk-admission path.
        /// </summary>
        public void Clear()
        {
            ThrowIfDisposed();
            _slots.Clear();
            Array.Clear(_dirtySlots, 0, _dirtySlots.Length);
            Array.Clear(_dirtyDirectoryEntries, 0, _dirtyDirectoryEntries.Length);
            _dirtyMin = int.MaxValue;
            _dirtyMax = -1;
            _directoryDirtyMin = int.MaxValue;
            _directoryDirtyMax = -1;
            for (int i = 0; i < _directoryStaging.Length; i++)
                _directoryStaging[i] = 0u;
            ClearGpuDirectory();
        }

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
                    // Absence from a ready region is the canonical GPU representation of empty.
                    // Uniform bricks still need one compact directory entry; empty bricks do not.
                    if (delta.Content == VoxelBrickContent.Empty)
                    {
                        RemoveLookup(delta.Coordinate);
                        return GpuBrickPublish.MetadataOnly;
                    }
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
            MarkDirectoryDirty(index);
            return true;
        }

        private void RemoveLookup(int3 coordinate)
        {
            int index = FindDirectoryIndex(coordinate, forInsert: false);
            if (index < 0) return;
            _directoryStaging[index * DirectoryWordsPerEntry + 4] = DirectoryTombstone;
            MarkDirectoryDirty(index);
        }

        private void MarkDirectoryDirty(int index)
        {
            _dirtyDirectoryEntries[index] = true;
            if (index < _directoryDirtyMin) _directoryDirtyMin = index;
            if (index > _directoryDirtyMax) _directoryDirtyMax = index;
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
            FlushPayloadSlots();
            FlushDirectoryDeltas();
        }

        /// <summary>
        /// Packs dirty payload slots into a fixed contiguous transfer buffer, then scatters them on
        /// the GPU. Slot reuse is intentionally free to be fragmented; transfer cost must therefore
        /// depend on the bounded recovery count, not on how many contiguous slot runs happen to
        /// exist after streaming.
        /// </summary>
        private void FlushPayloadSlots()
        {
            if (_dirtyMax < _dirtyMin) return;

            int batchCount = 0;
            for (int slot = _dirtyMin; slot <= _dirtyMax; slot++)
            {
                if (!_dirtySlots[slot]) continue;
                _dirtySlots[slot] = false;
                PackPayloadDelta(batchCount, slot);
                batchCount++;

                if (batchCount == PayloadDeltaCapacity)
                {
                    DispatchPayloadBatch(batchCount);
                    batchCount = 0;
                }
            }

            if (batchCount > 0) DispatchPayloadBatch(batchCount);
            _dirtyMin = int.MaxValue;
            _dirtyMax = -1;
        }

        private void PackPayloadDelta(int batchIndex, int slot)
        {
            int destination = batchIndex * PayloadDeltaWords;
            _payloadDeltaStaging[destination] = (uint)slot;

            NativeArray<uint>.Copy(
                _materialStaging, GpuBrickBufferLayout.MaterialWordOffset(slot),
                _payloadDeltaStaging, destination + PayloadMaterialOffset,
                GpuBrickBufferLayout.MaterialWordsPerBrick);
            NativeArray<uint>.Copy(
                _surfaceStaging, GpuBrickBufferLayout.SurfaceWordOffset(slot),
                _payloadDeltaStaging, destination + PayloadSurfaceOffset,
                GpuBrickBufferLayout.SurfaceWordsPerBrick);
            NativeArray<uint>.Copy(
                _boundaryStaging, GpuBrickBufferLayout.BoundaryWordOffset(slot),
                _payloadDeltaStaging, destination + PayloadBoundaryOffset,
                GpuBrickBufferLayout.BoundaryWordsPerBrick);

            BrickMetadata metadata = _metadataStaging[slot];
            _payloadDeltaStaging[destination + PayloadMetadataOffset + 0] =
                unchecked((uint)metadata.Slot);
            _payloadDeltaStaging[destination + PayloadMetadataOffset + 1] = metadata.Content;
            _payloadDeltaStaging[destination + PayloadMetadataOffset + 2] = metadata.UniformMaterial;
            _payloadDeltaStaging[destination + PayloadMetadataOffset + 3] = metadata.Flags;
        }

        private void DispatchPayloadBatch(int count)
        {
            int wordCount = count * PayloadDeltaWords;
            _payloadDeltas.SetData(_payloadDeltaStaging, 0, 0, wordCount);
            _directoryUpdater.SetBuffer(_applyPayloadKernel, IdPayloadDeltas, _payloadDeltas);
            _directoryUpdater.SetBuffer(_applyPayloadKernel, IdBrickMaterials, _materials);
            _directoryUpdater.SetBuffer(
                _applyPayloadKernel, IdBrickSurfaceSemantics, _surfaceSemantics);
            _directoryUpdater.SetBuffer(
                _applyPayloadKernel, IdBrickBoundarySamples, _boundarySamples);
            _directoryUpdater.SetBuffer(_applyPayloadKernel, IdBrickMetadata, _metadata);
            _directoryUpdater.SetInt(IdPayloadDeltaCount, count);
            _directoryUpdater.Dispatch(
                _applyPayloadKernel, Groups(count * PayloadCopyThreadsPerBrick), 1, 1);
            PayloadUploadBatches++;
            PayloadUploadBytes += (ulong)wordCount * sizeof(uint);
        }

        /// <summary>
        /// Packs the final state of every dirty hash entry into one contiguous delta stream. The
        /// CPU never performs scattered SetData calls into the directory; one bounded buffer upload
        /// feeds a tiny compute scatter, so world edits and streaming do not recreate the old
        /// per-brick driver-synchronisation problem under a different name.
        /// </summary>
        private void FlushDirectoryDeltas()
        {
            if (_directoryDirtyMax < _directoryDirtyMin) return;

            int batchCount = 0;
            for (int entry = _directoryDirtyMin; entry <= _directoryDirtyMax; entry++)
            {
                if (!_dirtyDirectoryEntries[entry]) continue;
                _dirtyDirectoryEntries[entry] = false;

                int destination = batchCount * DirectoryDeltaWords;
                int source = entry * DirectoryWordsPerEntry;
                _directoryDeltaStaging[destination + 0] = (uint)entry;
                _directoryDeltaStaging[destination + 1] = _directoryStaging[source + 0];
                _directoryDeltaStaging[destination + 2] = _directoryStaging[source + 1];
                _directoryDeltaStaging[destination + 3] = _directoryStaging[source + 2];
                _directoryDeltaStaging[destination + 4] = _directoryStaging[source + 3];
                _directoryDeltaStaging[destination + 5] = _directoryStaging[source + 4];
                batchCount++;

                if (batchCount == DirectoryDeltaCapacity)
                {
                    DispatchDirectoryBatch(batchCount);
                    batchCount = 0;
                }
            }

            if (batchCount > 0) DispatchDirectoryBatch(batchCount);
            _directoryDirtyMin = int.MaxValue;
            _directoryDirtyMax = -1;
        }

        private void DispatchDirectoryBatch(int count)
        {
            int wordCount = count * DirectoryDeltaWords;
            _directoryDeltas.SetData(_directoryDeltaStaging, 0, 0, wordCount);
            BindDirectoryUpdater(_applyDirectoryKernel);
            _directoryUpdater.SetBuffer(_applyDirectoryKernel, IdDirectoryDeltas, _directoryDeltas);
            _directoryUpdater.SetInt(IdDirectoryDeltaCount, count);
            _directoryUpdater.Dispatch(_applyDirectoryKernel, Groups(count), 1, 1);
            DirectoryUploadBatches++;
            DirectoryUploadBytes += (ulong)wordCount * sizeof(uint);
        }

        private void ClearGpuDirectory()
        {
            BindDirectoryUpdater(_clearDirectoryKernel);
            _directoryUpdater.Dispatch(_clearDirectoryKernel, Groups(DirectoryCapacity), 1, 1);
        }

        private void BindDirectoryUpdater(int kernel)
        {
            _directoryUpdater.SetBuffer(kernel, IdBrickMaterials, _materials);
            _directoryUpdater.SetInt(IdDirectoryWordOffset, DirectoryWordOffset);
            _directoryUpdater.SetInt(IdDirectoryCapacity, DirectoryCapacity);
        }

        private static int Groups(int items) => (items + ThreadGroupSize - 1) / ThreadGroupSize;

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
            _directoryDeltas?.Release();
            _payloadDeltas?.Release();
            if (_materialStaging.IsCreated) _materialStaging.Dispose();
            if (_surfaceStaging.IsCreated) _surfaceStaging.Dispose();
            if (_boundaryStaging.IsCreated) _boundaryStaging.Dispose();
            if (_metadataStaging.IsCreated) _metadataStaging.Dispose();
            if (_directoryStaging.IsCreated) _directoryStaging.Dispose();
            if (_directoryDeltaStaging.IsCreated) _directoryDeltaStaging.Dispose();
            if (_payloadDeltaStaging.IsCreated) _payloadDeltaStaging.Dispose();
            _slots.Clear();
        }
    }
}
