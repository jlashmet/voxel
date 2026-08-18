using Unity.Mathematics;

namespace VoxelEngine.Rendering.Runtime.SurfaceExtraction
{
    /// <summary>Computes transition faces from the actual active LOD antichain.</summary>
    internal static class SurfaceLodTransitionMask
    {
        public const int FaceCount = 6;

        public static byte Compute(in SurfaceLodNodeKey node,
                                   SurfaceLodActiveCoverage activeCoverage)
        {
            if (activeCoverage == null || node.SourceStep <= SurfaceLodHierarchy.FinestSourceStep)
                return 0;
            byte mask = 0;
            for (int face = 0; face < FaceCount; face++)
            {
                int axis = face >> 1;
                int direction = (face & 1) == 0 ? -1 : 1;
                int3 neighbourCoordinate = node.Coordinate;
                neighbourCoordinate[axis] += direction;
                var neighbourRegion = new SurfaceLodNodeKey(node.SourceStep, neighbourCoordinate);
                if (activeCoverage.HasActiveDescendant(neighbourRegion))
                    mask |= (byte)(1 << face);
            }
            return mask;
        }
    }
}
