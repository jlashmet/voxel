using System.Runtime.CompilerServices;
using VoxelEngine.Storage.Runtime;

namespace VoxelEngine.Storage.Runtime.Occupancy
{
    /// <summary>
    /// Flattened storage layout for a region's mip pyramid.
    ///
    /// <para><b>Level 0 is deliberately not stored.</b> A level-0 cell is one brick, and its
    /// occupancy aggregate and dominant material are both derivable from <c>BrickRefs</c> plus
    /// the pool. Materialising it would duplicate the single source of truth and, at 262,144
    /// cells, would account for 87% of the pyramid's footprint — 2.4 MB per region, which is
    /// untenable across a 500 m load radius. Storing levels 1..N instead costs ~337 KB per
    /// region, and level 0 is reconstructed on demand by
    /// <see cref="MipBuilder.ReadLevel0"/>.</para>
    ///
    /// Levels are packed in ascending order, so level 1 occupies the front of the array and the
    /// single-cell top level sits at the end. That ordering means a far region can hold a
    /// truncated *tail* of the pyramid — the coarse levels alone, a few hundred bytes — and
    /// refine downward by prepending finer levels as the viewer approaches, which is the
    /// progressive-refinement behaviour described in architecture-notes.md.
    /// </summary>
    public static class RegionMipLayout
    {
        /// <summary>Lowest level held in flattened storage. Level 0 is derived, never stored.</summary>
        public const int FirstStoredLevel = 1;

        /// <summary>Total cells across stored levels 1..<paramref name="levelCount"/>-1.</summary>
        public static int TotalStoredCells(int levelCount)
        {
            int total = 0;
            for (int level = FirstStoredLevel; level < levelCount; level++)
                total += MipBuilder.TotalCellCount(level);
            return total;
        }

        /// <summary>Offset of a stored level's first cell within the flattened array.</summary>
        public static int LevelOffset(int level)
        {
            int offset = 0;
            for (int l = FirstStoredLevel; l < level; l++)
                offset += MipBuilder.TotalCellCount(l);
            return offset;
        }

        /// <summary>Flattened index of a cell coordinate at a stored level.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Index(int level, int x, int y, int z)
        {
            int edge = MipBuilder.RegionEdgeForLevel(level);
            return LevelOffset(level) + MipBuilder.CellIndex(x, y, z, edge);
        }

        /// <summary>Bytes a full pyramid occupies for one region, occupancy plus materials.</summary>
        public static long BytesPerRegion(int levelCount)
        {
            long cells = TotalStoredCells(levelCount);
            return cells * sizeof(ulong) + cells * sizeof(byte);
        }
    }
}
