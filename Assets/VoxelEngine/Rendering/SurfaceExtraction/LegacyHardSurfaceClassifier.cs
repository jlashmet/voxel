using System.Collections.Generic;
using Unity.Mathematics;
using VoxelEngine.Core.Storage;

namespace VoxelEngine.Rendering.SurfaceExtraction
{
    /// <summary>
    /// Migration shim for worlds generated before explicit surface semantics existed.
    ///
    /// New procedural generation should mark hard/smooth geometry directly from its semantic
    /// structure graph. The showcase castle cannot do that yet, so unmistakably authored accent
    /// materials bootstrap a local search for architectural surface bricks.
    ///
    /// This classifier is intentionally one-shot. It is legacy migration work, not part of the
    /// renderer's steady-state frame loop. Re-running the scan while streaming caused repeated
    /// allocations, semantic rescans, and repeated long hard-mesh bootstrap slices.
    /// </summary>
    public static class LegacyHardSurfaceClassifier
    {
        private const int RenderChunkShift = 4; // 16 bricks = 12.8 m
        private const int LegacyChunkExpansion = 1;
        private static bool s_Bootstrapped;

        public static int TagAuthoredSurfaceBricks(ref RegionTable table, in BrickPool pool,
                                                   IReadOnlyList<int3> worldBricks)
        {
            if (s_Bootstrapped || worldBricks == null || worldBricks.Count == 0) return 0;

            var seedChunks = new HashSet<int3>();
            int minimumSeedY = int.MaxValue;

            // Wood is deliberately not a seed: the showcase trees use the same timber material.
            // Roof/window/trim materials locate the castle first; timber is accepted only inside
            // that already-authored neighbourhood.
            for (int i = 0; i < worldBricks.Count; i++)
            {
                int3 worldBrick = worldBricks[i];
                if (!TryGetBrick(ref table, worldBrick, out BrickRef brick)) continue;
                if (!ContainsSeedMaterial(in pool, brick)) continue;

                minimumSeedY = math.min(minimumSeedY, worldBrick.y);
                int3 centreChunk = new(worldBrick.x >> RenderChunkShift,
                                       worldBrick.y >> RenderChunkShift,
                                       worldBrick.z >> RenderChunkShift);
                for (int dz = -LegacyChunkExpansion; dz <= LegacyChunkExpansion; dz++)
                for (int dy = -LegacyChunkExpansion; dy <= LegacyChunkExpansion; dy++)
                for (int dx = -LegacyChunkExpansion; dx <= LegacyChunkExpansion; dx++)
                    seedChunks.Add(centreChunk + new int3(dx, dy, dz));
            }

            // No authored material has reached the GPU mirror yet. Do not latch: a later first
            // upload still gets one chance to migrate the showcase castle.
            if (seedChunks.Count == 0) return 0;

            int minimumArchitecturalY = minimumSeedY - 1;
            int tagged = 0;

            for (int i = 0; i < worldBricks.Count; i++)
            {
                int3 worldBrick = worldBricks[i];
                if (worldBrick.y < minimumArchitecturalY) continue;

                int3 chunk = new(worldBrick.x >> RenderChunkShift,
                                 worldBrick.y >> RenderChunkShift,
                                 worldBrick.z >> RenderChunkShift);
                if (!seedChunks.Contains(chunk)) continue;
                if (!TryGetBrick(ref table, worldBrick, out BrickRef brick)) continue;
                if (!LooksArchitectural(in pool, brick)) continue;
                if (MarkActualBrick(ref table, worldBrick)) tagged++;
            }

            s_Bootstrapped = true;
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

        private static bool MarkActualBrick(ref RegionTable table, int3 worldBrick)
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

        private static bool ContainsSeedMaterial(in BrickPool pool, BrickRef brick)
        {
            if (brick.IsUniform) return IsSeedMaterial(brick.UniformMaterial);

            int offset = pool.VoxelOffset(brick.PoolIndex);
            for (int i = 0; i < VoxelDimensions.VoxelsPerBrick; i++)
                if (IsSeedMaterial(pool.Voxels[offset + i]))
                    return true;
            return false;
        }

        private static bool LooksArchitectural(in BrickPool pool, BrickRef brick)
        {
            if (brick.IsEmpty) return false;
            if (brick.IsUniform) return IsArchitecturalMaterial(brick.UniformMaterial);

            int offset = pool.VoxelOffset(brick.PoolIndex);
            bool sawSolid = false;
            for (int i = 0; i < VoxelDimensions.VoxelsPerBrick; i++)
            {
                byte material = pool.Voxels[offset + i];
                if (material == VoxelDimensions.MaterialEmpty) continue;

                // Do this before accepting timber/masonry. A mixed tree/ground brick or a grass
                // capped plateau brick stays smooth even if it also contains wood/stone.
                if (IsNaturalSurfaceMaterial(material)) return false;
                if (!IsArchitecturalMaterial(material)) return false;
                sawSolid = true;
            }
            return sawSolid;
        }

        private static bool IsSeedMaterial(byte material) =>
            material == 4   // warm glass
            || material == 7   // slate
            || material == 8   // roof tile
            || material == 9   // cloth
            || material == 12  // gold / metal trim
            || material == 15; // leaded window

        private static bool IsArchitecturalMaterial(byte material) =>
            material == 1   // stone masonry
            || material == 2   // timber, but only after a non-tree seed located the castle
            || material == 4
            || material == 6   // dark stone masonry
            || material == 7
            || material == 8
            || material == 9
            || material == 12
            || material == 15;

        private static bool IsNaturalSurfaceMaterial(byte material) =>
            material == 3   // sand
            || material == 10  // grass
            || material == 11  // water
            || material == 13  // dirt
            || material == 14  // moss
            || material == 16; // cascade
    }
}
