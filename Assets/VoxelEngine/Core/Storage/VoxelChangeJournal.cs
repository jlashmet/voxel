using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace VoxelEngine.Core.Storage
{
    [Flags]
    public enum VoxelChangeKind : byte
    {
        None = 0,
        Occupancy = 1 << 0,
        BaseMaterial = 1 << 1,
        SurfaceStyle = 1 << 2,
        Coating = 1 << 3,
        Water = 1 << 4,
        Residency = 1 << 5,
        All = byte.MaxValue,
    }

    public readonly struct VoxelChangeRecord
    {
        public readonly ulong Version;
        public readonly int3 Region;
        public readonly int3 MinVoxel;
        public readonly int3 MaxVoxelExclusive;
        public readonly VoxelChangeKind Kind;

        public VoxelChangeRecord(ulong version, int3 region, int3 minVoxel,
                                 int3 maxVoxelExclusive, VoxelChangeKind kind)
        {
            Version = version;
            Region = region;
            MinVoxel = minVoxel;
            MaxVoxelExclusive = maxVoxelExclusive;
            Kind = kind;
        }
    }

    /// <summary>
    /// Bounded append-only world-change stream. Consumers own cursors; no consumer clears state
    /// needed by another. Falling behind the retained window is reported explicitly.
    /// </summary>
    public sealed class VoxelChangeJournal
    {
        private readonly VoxelChangeRecord[] _records;
        private int _start;
        private int _count;

        public VoxelChangeJournal(int capacity = 4096)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _records = new VoxelChangeRecord[capacity];
        }

        public ulong CurrentVersion { get; private set; }
        public ulong OldestRetainedVersion => _count == 0
            ? CurrentVersion + 1 : _records[_start].Version;
        public int RetainedCount => _count;

        public ulong Publish(int3 region, int3 minVoxel, int3 maxVoxelExclusive,
                             VoxelChangeKind kind)
        {
            ulong version = ++CurrentVersion;
            var record = new VoxelChangeRecord(version, region, minVoxel,
                                               maxVoxelExclusive, kind);
            if (_count < _records.Length)
            {
                _records[(_start + _count) % _records.Length] = record;
                _count++;
            }
            else
            {
                _records[_start] = record;
                _start = (_start + 1) % _records.Length;
            }
            return version;
        }

        public ulong PublishRegion(int3 region, VoxelChangeKind kind = VoxelChangeKind.All)
        {
            int3 min = region * VoxelDimensions.RegionVoxelEdge;
            return Publish(region, min, min + VoxelDimensions.RegionVoxelEdge, kind);
        }

        /// <summary>Reads records newer than cursor and advances it to the current version.</summary>
        public bool ReadSince(ref ulong cursor, List<VoxelChangeRecord> destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            destination.Clear();
            bool overflowed = _count > 0 && cursor + 1 < OldestRetainedVersion;
            for (int i = 0; i < _count; i++)
            {
                VoxelChangeRecord record = _records[(_start + i) % _records.Length];
                if (overflowed || record.Version > cursor) destination.Add(record);
            }
            cursor = CurrentVersion;
            return !overflowed;
        }
    }
}
