using System;
using System.Collections.Generic;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Storage.Runtime
{
    /// <summary>
    /// Bounded append-only world-change stream. Consumers own cursors; no consumer clears state
    /// needed by another. Falling behind the retained window is reported explicitly.
    /// </summary>
    public sealed class VoxelChangeJournal : IVoxelChangeSource
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
            int3 min = region * VoxelGrid.RegionVoxelEdge;
            return Publish(region, min, min + VoxelGrid.RegionVoxelEdge, kind);
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

        public bool ReadSince(ref ulong cursor, List<VoxelChangeRecord> destination,
                              int maxRecords, out bool hasMore)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (maxRecords <= 0) throw new ArgumentOutOfRangeException(nameof(maxRecords));
            destination.Clear();

            bool overflowed = _count > 0 && cursor + 1 < OldestRetainedVersion;
            if (overflowed)
            {
                // Exact replay is already impossible. Do not spend frame time copying a retained
                // suffix the consumer must ignore; move it to the recovery boundary immediately.
                cursor = CurrentVersion;
                hasMore = false;
                return false;
            }

            ulong targetVersion = CurrentVersion;
            int emitted = 0;
            for (int i = 0; i < _count && emitted < maxRecords; i++)
            {
                VoxelChangeRecord record = _records[(_start + i) % _records.Length];
                if (record.Version <= cursor) continue;
                destination.Add(record);
                cursor = record.Version;
                emitted++;
            }
            hasMore = cursor < targetVersion;
            return true;
        }
    }
}
