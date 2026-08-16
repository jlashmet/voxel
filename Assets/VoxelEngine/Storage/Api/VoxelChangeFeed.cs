using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace VoxelEngine.Storage.Api
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
    /// Read-only world-change feed. Storage owns publication/retention; consumers own cursors.
    /// </summary>
    public interface IVoxelChangeSource
    {
        ulong CurrentVersion { get; }
        bool ReadSince(ref ulong cursor, List<VoxelChangeRecord> destination);

        /// <summary>
        /// Reads at most <paramref name="maxRecords"/> retained records newer than
        /// <paramref name="cursor"/>. On a valid incremental read, cursor advances only to the
        /// last emitted record and <paramref name="hasMore"/> reports remaining backlog. If the
        /// cursor has fallen behind retention, returns false, advances cursor to CurrentVersion,
        /// clears destination and reports no replay backlog; the consumer must perform its own
        /// bounded full-state recovery.
        /// </summary>
        bool ReadSince(ref ulong cursor, List<VoxelChangeRecord> destination,
                       int maxRecords, out bool hasMore);
    }
}
