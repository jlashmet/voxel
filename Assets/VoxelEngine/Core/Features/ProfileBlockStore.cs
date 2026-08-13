using System;
using System.Collections.Generic;
using Unity.Mathematics;
using VoxelEngine.Core.Storage;

namespace VoxelEngine.Core.Features
{
    /// <summary>
    /// A shallow, individually jointed block whose visible face follows an authored 2D profile.
    /// Structural occupancy remains in the voxel field; this retained surface description keeps
    /// sub-voxel joints and curvature that a single boundary sample per voxel cannot represent.
    /// </summary>
    public struct ProfileBlock
    {
        public int3 Centre;
        public int InnerRadiusQ4;
        public int OuterRadiusQ4;
        public int FrontQ4;
        public int BackQ4;
        public int2 StartDirection;
        public int2 EndDirection;
        public byte Axis;
        public byte Material;
        public ushort SurfaceStyle;
        public byte Coating;
        public byte SurfaceDetail;
        public byte JointHalfWidthQ4;
        public byte BevelQ4;

        public void Bounds(out int3 min, out int3 max)
        {
            int radius = (OuterRadiusQ4 + 15) >> 4;
            min = Centre - radius;
            max = Centre + radius;
            min[Axis] = FloorQ4(math.min(FrontQ4, BackQ4));
            max[Axis] = CeilQ4(math.max(FrontQ4, BackQ4));
        }

        private static int FloorQ4(int value) => value >= 0 ? value >> 4 : -((-value + 15) >> 4);
        private static int CeilQ4(int value) => value >= 0 ? (value + 15) >> 4 : -((-value) >> 4);
    }

    /// <summary>
    /// Versioned retained surface primitives. Features add blocks during generation; rendering
    /// snapshots them when the version changes and reconstructs them through the solid mesh cache.
    /// </summary>
    public sealed class ProfileBlockStore
    {
        private readonly List<ProfileBlock> _blocks = new();

        public uint Version { get; private set; }
        public int Count => _blocks.Count;
        public ProfileBlock this[int index] => _blocks[index];

        public void Add(in ProfileBlock block)
        {
            if (block.Axis > 2 || block.Material == VoxelDimensions.MaterialEmpty
                || block.OuterRadiusQ4 <= block.InnerRadiusQ4
                || block.BackQ4 <= block.FrontQ4)
                throw new ArgumentException("Invalid profile block.", nameof(block));
            _blocks.Add(block);
            Version++;
        }

        public ProfileBlock[] Snapshot() => _blocks.ToArray();
    }
}
