using System;
using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Compact planner-owned courtyard material choices. The polygon bounds are stored once and
    /// DirtMask uses one bit per local X/Z cell; unset bits are stone. Runtime only reads this
    /// payload and never draws paving randomness while mutating voxels.
    /// </summary>
    public sealed class CastleCourtyardPavingPlan
    {
        private readonly byte[] _dirtMask;

        public int2 Minimum { get; }
        public int Width { get; }
        public int Depth { get; }
        public byte[] DirtMask => _dirtMask;

        internal CastleCourtyardPavingPlan(
            int2 minimum,
            int width,
            int depth,
            byte[] dirtMask)
        {
            Minimum = minimum;
            Width = width;
            Depth = depth;
            _dirtMask = dirtMask ?? Array.Empty<byte>();
        }

        public bool Contains(int2 local) =>
            local.x >= Minimum.x && local.y >= Minimum.y &&
            local.x < Minimum.x + Width && local.y < Minimum.y + Depth;

        public bool IsDirt(int2 local)
        {
            if (!Contains(local)) return false;
            int x = local.x - Minimum.x;
            int z = local.y - Minimum.y;
            int index = x + z * Width;
            return (_dirtMask[index >> 3] & (1 << (index & 7))) != 0;
        }
    }
}
