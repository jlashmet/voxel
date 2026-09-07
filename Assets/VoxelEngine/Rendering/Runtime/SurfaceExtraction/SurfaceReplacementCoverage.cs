using System;
using Unity.Mathematics;

namespace VoxelEngine.Rendering.Runtime.SurfaceExtraction
{
    /// <summary>
    /// Bounded coverage of a feature's voxel bounds by the current camera's selected near draw
    /// set. The caller supplies current publication/empty proofs, never mere residency or a
    /// previous frame's visibility. This class does not infer authoritative occupancy.
    /// </summary>
    internal static class SurfaceReplacementCoverage
    {
        internal const int MaximumFineCells = 4096;

        /// <param name="hasCurrentProof">
        /// True only for a current, selected drawable node or a current known-empty node owned
        /// by that ring. Unknown, stale and unselected geometry must return false.
        /// </param>
        internal static bool Covers(int3 minVoxel, int3 maxVoxelExclusive,
                                    Func<SurfaceLodNodeKey, bool> hasCurrentProof,
                                    out int proofQueries)
        {
            if (hasCurrentProof == null) throw new ArgumentNullException(nameof(hasCurrentProof));
            proofQueries = 0;
            if (math.any(maxVoxelExclusive <= minVoxel)) return false;

            // Arithmetic shifts preserve floor division on negative world coordinates. The
            // exclusive upper bound prevents demanding the adjacent chunk at an exact seam.
            int3 min = minVoxel >> 6;
            int3 max = (maxVoxelExclusive - 1) >> 6;
            long xCount = (long)max.x - min.x + 1;
            long yCount = (long)max.y - min.y + 1;
            long zCount = (long)max.z - min.z + 1;
            if (xCount > MaximumFineCells || yCount > MaximumFineCells
                || zCount > MaximumFineCells
                || xCount * yCount * zCount > MaximumFineCells)
                return false;

            // Prove each coarse subtree once, descending only where its publication is
            // insufficient. Repeating the ancestor chain for every fine cell makes a fully
            // covered step-8 chunk pay for the same proof 512 times.
            int3 rootMin = min >> 3;
            int3 rootMax = max >> 3;
            for (int z = rootMin.z; z <= rootMax.z; z++)
            for (int y = rootMin.y; y <= rootMax.y; y++)
            for (int x = rootMin.x; x <= rootMax.x; x++)
                if (!CoversNode(new SurfaceLodNodeKey(8, new int3(x, y, z)), min, max,
                    hasCurrentProof, ref proofQueries)) return false;
            return true;
        }

        private static bool CoversNode(SurfaceLodNodeKey node, int3 min, int3 max,
                                       Func<SurfaceLodNodeKey, bool> hasCurrentProof,
                                       ref int proofQueries)
        {
            proofQueries++;
            if (hasCurrentProof(node)) return true;
            if (node.SourceStep == 1) return false;
            int childStep = node.SourceStep >> 1;
            // Division must floor for negative coordinates, as in the original fine-cell walk.
            int shift = childStep == 4 ? 2 : childStep == 2 ? 1 : 0;
            int3 childMin = math.max(node.Coordinate * 2, min >> shift);
            int3 childMax = math.min(node.Coordinate * 2 + 1, max >> shift);
            for (int z = childMin.z; z <= childMax.z; z++)
            for (int y = childMin.y; y <= childMax.y; y++)
            for (int x = childMin.x; x <= childMax.x; x++)
                if (!CoversNode(new SurfaceLodNodeKey(childStep, new int3(x, y, z)), min, max,
                    hasCurrentProof, ref proofQueries)) return false;
            return true;
        }
    }
}
