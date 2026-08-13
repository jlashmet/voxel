using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Storage;

namespace VoxelEngine.Tests.Features
{
    /// <summary>
    /// Compares a volume generated whole against the same volume generated as disjoint pieces.
    ///
    /// This is the acceptance criterion behind FR-008. A feature spanning four regions is
    /// generated four times, once per region, each producing only its own slice — so "the pieces
    /// equal the whole" is not a nice property, it is the definition of correct.
    ///
    /// A failure here appears in the world as a seam: a wall that stops at a region border, or a
    /// doorway that exists on one side and not the other.
    /// </summary>
    public static class SubVolumeEquality
    {
        /// <summary>Splits a volume into eight octants, for use as the disjoint tiling.</summary>
        public static (int3 min, int3 max)[] Octants(int3 min, int3 max)
        {
            int3 mid = min + (max - min) / 2;

            return new[]
            {
                (new int3(min.x, min.y, min.z), new int3(mid.x, mid.y, mid.z)),
                (new int3(mid.x, min.y, min.z), new int3(max.x, mid.y, mid.z)),
                (new int3(min.x, mid.y, min.z), new int3(mid.x, max.y, mid.z)),
                (new int3(mid.x, mid.y, min.z), new int3(max.x, max.y, mid.z)),
                (new int3(min.x, min.y, mid.z), new int3(mid.x, mid.y, max.z)),
                (new int3(mid.x, min.y, mid.z), new int3(max.x, mid.y, max.z)),
                (new int3(min.x, mid.y, mid.z), new int3(mid.x, max.y, max.z)),
                (new int3(mid.x, mid.y, mid.z), new int3(max.x, max.y, max.z)),
            };
        }

        /// <summary>Reads material, style, coating, and flags into a flat array for comparison.</summary>
        public static byte[] Snapshot(ref RegionTable table, in BrickPool pool, int3 min, int3 max)
        {
            int3 size = max - min;
            var result = new byte[size.x * size.y * size.z * 3];

            int i = 0;
            for (int z = min.z; z < max.z; z++)
            for (int y = min.y; y < max.y; y++)
            for (int x = min.x; x < max.x; x++)
            {
                VoxelCell cell = VoxelAccess.GetCell(ref table, in pool, new int3(x, y, z));
                ushort surface = cell.Surface.PackedStorage;
                result[i++] = cell.BaseMaterialId;
                result[i++] = (byte)surface;
                result[i++] = (byte)(surface >> 8);
            }

            return result;
        }

        /// <summary>Index of the first differing voxel, or -1 when the snapshots match.</summary>
        public static int FirstDifference(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return 0;

            for (var i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return i;

            return -1;
        }
    }
}
