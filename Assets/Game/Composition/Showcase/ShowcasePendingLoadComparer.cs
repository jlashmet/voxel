using System.Collections.Generic;
using Unity.Mathematics;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Reusable ordering for the showcase's pending terrain loads.
    ///
    /// The wanted set is rebuilt every streaming step. Keeping this comparer as an owned object
    /// avoids a capturing comparison delegate on that frame path, while the castle membership
    /// set keeps landmark-priority checks constant-time during List.Sort's repeated comparisons.
    /// </summary>
    internal sealed class ShowcasePendingLoadComparer : IComparer<int3>
    {
        public int3 Centre;
        public bool PrioritizeCastle;
        public HashSet<int3> CastleRegions;

        public int Compare(int3 a, int3 b)
        {
            bool aCastle = PrioritizeCastle && CastleRegions != null && CastleRegions.Contains(a);
            bool bCastle = PrioritizeCastle && CastleRegions != null && CastleRegions.Contains(b);
            if (aCastle != bCastle) return aCastle ? -1 : 1;

            long adx = a.x - (long)Centre.x;
            long adz = a.z - (long)Centre.z;
            long bdx = b.x - (long)Centre.x;
            long bdz = b.z - (long)Centre.z;
            long da = adx * adx + adz * adz;
            long db = bdx * bdx + bdz * bdz;
            return da.CompareTo(db);
        }
    }
}
