using System.Collections.Generic;
using Unity.Mathematics;
using VoxelEngine.Core.Storage;

namespace VoxelEngine.Rendering.SurfaceExtraction
{
    /// <summary>
    /// Migration shim for worlds generated before explicit surface semantics existed.
    ///
    /// New procedural generation should mark hard/smooth geometry directly from its semantic
    /// structure graph. The showcase castle cannot do that yet, so unmistakably authored
    /// materials bootstrap the tag: timber, glass, slate, roof tile, cloth, gold and leaded
    /// windows. Natural stone is intentionally absent because castle stone and cliffs share that
    /// material; once one authored brick tags a render chunk, the hard mesher rebuilds the whole
    /// chunk exactly, preserving adjacent masonry too.
    /// </summary>
    public static class LegacyHardSurfaceClassifier
    {
        public static int TagAuthoredSurfaceBricks(ref RegionTable table, in BrickPool pool,
                                                   IReadOnlyList<int3> worldBricks)
        {
            if (worldBricks == null || worldBricks.Count == 0) return 0;
            int tagged = 0;

            for (int i = 0; i < worldBricks.Count; i++)
            {
                int3 worldBrick = worldBricks[i];
                int3 regionCoord = new(worldBrick.x >> VoxelDimensions.RegionEdgeLog2,
                                       worldBrick.y >> VoxelDimensions.RegionEdgeLog2,
                                       worldBrick.z >> VoxelDimensions.RegionEdgeLog2);
                if (!table.TryGetRegion(regionCoord, out Region region)) continue;

                int bx = worldBrick.x & VoxelDimensions.RegionEdgeMask;
                int by = worldBrick.y & VoxelDimensions.RegionEdgeMask;
                int bz = worldBrick.z & VoxelDimensions.RegionEdgeMask;
                int brickIndex = Region.BrickIndex(bx, by, bz);
                BrickRef brick = region.BrickRefs[brickIndex];
                if (!ContainsAuthoredMaterial(in pool, brick)) continue;

                if (region.MarkHardSurfaceBrick(brickIndex)) tagged++;
            }

            return tagged;
        }

        private static bool ContainsAuthoredMaterial(in BrickPool pool, BrickRef brick)
        {
            if (brick.IsUniform) return IsAuthoredMaterial(brick.UniformMaterial);

            int offset = pool.VoxelOffset(brick.PoolIndex);
            for (int i = 0; i < VoxelDimensions.VoxelsPerBrick; i++)
                if (IsAuthoredMaterial(pool.Voxels[offset + i]))
                    return true;
            return false;
        }

        private static bool IsAuthoredMaterial(byte material)
        {
            return material == 2   // timber
                || material == 4   // warm glass
                || material == 7   // slate
                || material == 8   // roof tile
                || material == 9   // cloth
                || material == 12  // gold / metal trim
                || material == 15; // leaded window
        }
    }
}
