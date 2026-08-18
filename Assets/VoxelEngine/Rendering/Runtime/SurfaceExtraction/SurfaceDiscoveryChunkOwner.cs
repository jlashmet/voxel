using Unity.Mathematics;

namespace VoxelEngine.Rendering.Runtime.SurfaceExtraction
{
    /// <summary>
    /// Converts an authoritative surface-brick coordinate into a non-border representative
    /// coordinate inside the same solid-render chunk.
    ///
    /// Initial surface discovery establishes which chunk owns authoritative content; it is not
    /// halo invalidation. The solid cache's generic brick admission also understands border
    /// dependencies, so feeding a discovered brick that lies exactly on a chunk boundary can
    /// create halo-only neighbour chunks. Those neighbours may have no resident owned core even
    /// though their extraction halo touches resident Storage. Canonicalising only the discovery
    /// feed preserves the owning chunk while preventing that accidental neighbour admission.
    /// Mutation invalidation and water discovery continue to consume the original coordinates.
    /// </summary>
    internal static class SurfaceDiscoveryChunkOwner
    {
        public static int3 Canonicalize(int3 worldBrick, int bricksPerChunkAxis)
        {
            int edge = math.max(1, bricksPerChunkAxis);
            int interior = edge / 2;
            return new int3(
                FloorDiv(worldBrick.x, edge) * edge + interior,
                FloorDiv(worldBrick.y, edge) * edge + interior,
                FloorDiv(worldBrick.z, edge) * edge + interior);
        }

        private static int FloorDiv(int value, int divisor)
        {
            int quotient = value / divisor;
            int remainder = value % divisor;
            return remainder < 0 ? quotient - 1 : quotient;
        }
    }
}
