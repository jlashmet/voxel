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
    /// material.
    ///
    /// The old castle also contains long pure-stone spans. A material hit therefore claims the
    /// surrounding recovery render chunks (one chunk in each direction) rather than only the
    /// brick containing the accent. This neighborhood expansion is deliberately legacy-only; new
    /// semantic generation will assign geometry mode explicitly and will not need inference.
    /// </summary>
    public static class LegacyHardSurfaceClassifier
    {
        private const int BricksPerRenderChunk = 16;
        private const int RenderChunkShift = 4;
        private const int LegacyChunkExpansion = 1;

        public static int TagAuthoredSurfaceBricks(ref RegionTable table, in BrickPool pool,
                                                   IReadOnlyList<int3> worldBricks)
        {
            if (worldBricks == null || worldBricks.Count == 0) return 0;
            int tagged = 0;

            for (int i = 0; i < worldBricks.Count; i++)
            {
                int3 worldBrick = worldBricks[i];
                if (!TryGetBrick(ref table, worldBrick, out BrickRef brick)) continue;
                if (!ContainsAuthoredMaterial(in pool, brick)) continue;

                int3 centreChunk = new(worldBrick.x >> RenderChunkShift,
                                       worldBrick.y >> RenderChunkShift,
                                       worldBrick.z >> RenderChunkShift);

                for (int dz = -LegacyChunkExpansion; dz <= LegacyChunkExpansion; dz++)
                for (int dy = -LegacyChunkExpansion; dy <= LegacyChunkExpansion; dy++)
                for (int dx = -LegacyChunkExpansion; dx <= LegacyChunkExpansion; dx++)
                {
                    int3 chunk = centreChunk + new int3(dx, dy, dz);
                    int3 representativeWorldBrick = chunk * BricksPerRenderChunk;
                    if (MarkRepresentativeBrick(ref table, representativeWorldBrick)) tagged++;
                }
            }

            return tagged;
        }

        private static bool TryGetBrick(ref RegionTable table, int3 worldBrick, out BrickRef brick)
        {
            int3 regionCoord = new(worldBrick.x >> VoxelDimensions.RegionEdgeLog2,
                                   worldBrick.y >> VoxelDimensions.RegionEdgeLog2,
                                   worldBrick.z >> VoxelDimensions.RegionEdgeLog2);
            if (!table.TryGetRegion(regionCoord, out Region region))
            {
                brick = BrickRef.Empty;
                return false;
            }

            int bx = worldBrick.x & VoxelDimensions.RegionEdgeMask;
            int by = worldBrick.y & VoxelDimensions.RegionEdgeMask;
            int bz = worldBrick.z & VoxelDimensions.RegionEdgeMask;
            brick = region.GetBrick(bx, by, bz);
            return true;
        }

        private static bool MarkRepresentativeBrick(ref RegionTable table, int3 worldBrick)
        {
            int3 regionCoord = new(worldBrick.x >> VoxelDimensions.RegionEdgeLog2,
                                   worldBrick.y >> VoxelDimensions.RegionEdgeLog2,
                                   worldBrick.z >> VoxelDimensions.RegionEdgeLog2);
            if (!table.TryGetRegion(regionCoord, out Region region)) return false;

            int bx = worldBrick.x & VoxelDimensions.RegionEdgeMask;
            int by = worldBrick.y & VoxelDimensions.RegionEdgeMask;
            int bz = worldBrick.z & VoxelDimensions.RegionEdgeMask;
            int brickIndex = Region.BrickIndex(bx, by, bz);
            return region.MarkHardSurfaceBrick(brickIndex);
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
