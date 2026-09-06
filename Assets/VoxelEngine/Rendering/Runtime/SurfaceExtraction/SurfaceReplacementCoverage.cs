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

            for (int z = min.z; z <= max.z; z++)
            for (int y = min.y; y <= max.y; y++)
            for (int x = min.x; x <= max.x; x++)
            {
                var node = new SurfaceLodNodeKey(1, new int3(x, y, z));
                bool covered = false;
                while (true)
                {
                    proofQueries++;
                    if (hasCurrentProof(node)) { covered = true; break; }
                    if (!SurfaceLodHierarchy.TryGetParentSourceStep(node.SourceStep, out int parentStep))
                        break;
                    node = new SurfaceLodNodeKey(parentStep,
                        SurfaceLodHierarchy.ParentCoordinate(node.Coordinate));
                }
                if (!covered) return false;
            }
            return true;
        }
    }
}
