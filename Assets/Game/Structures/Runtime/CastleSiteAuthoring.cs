using Game.Materials.Api;
using Game.Structures.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using TerrainSampler = VoxelEngine.Terrain.Api.TerrainQuery;
using Random = Unity.Mathematics.Random;

namespace Game.Structures.Runtime
{
    /// <summary>Resumable state for the game-owned castle site sculpt.</summary>
    public struct CastleSiteAuthoringState
    {
        internal int Phase;
        internal int Cursor;
        internal Random Random;

        public bool IsComplete => Phase > 2;
    }

    /// <summary>
    /// Authors the crag, lower river, and optional bounded moat that belong to the castle content.
    /// The implementation uses only the generic structure-authoring API and game-selected material
    /// indices; no engine-owned castle or terrain-builder abstraction is introduced here.
    /// </summary>
    public static class CastleSiteAuthoring
    {
        public static bool Step(
            IStructureAuthoringSession authoring,
            in CastlePlan plan,
            in CastleComponentConfig components,
            uint terrainSeed,
            ref CastleSiteAuthoringState state)
        {
            if (!components.IsWellFormed)
                throw new System.ArgumentException(
                    "Castle component configuration is invalid.", nameof(components));

            return StepCore(
                authoring,
                in plan,
                in components.Moat,
                in components.Palette,
                terrainSeed,
                ref state);
        }

        /// <summary>
        /// Compatibility entry point for older callers. It resolves the current compatibility
        /// component graph, whose moat is disabled, so historical site output remains unchanged.
        /// </summary>
        public static bool Step(
            IStructureAuthoringSession authoring,
            in CastlePlan plan,
            uint terrainSeed,
            ref CastleSiteAuthoringState state)
        {
            StructureMaterialPalette palette = CastleStructurePalette.Compatibility;
            CastleComponentConfig components = CastleComponentPresets.Compatibility(in plan, in palette);
            return Step(authoring, in plan, in components, terrainSeed, ref state);
        }

        private static bool StepCore(
            IStructureAuthoringSession authoring,
            in CastlePlan plan,
            in CastleMoatConfig moat,
            in StructureMaterialPalette palette,
            uint terrainSeed,
            ref CastleSiteAuthoringState state)
        {
            if (authoring == null) throw new System.ArgumentNullException(nameof(authoring));
            if (state.IsComplete) return true;

            int top = plan.Centre.y + plan.PlateauHeight;
            int radius = plan.PlateauRadius;
            int cliffDrop = plan.CliffDrop;
            int skirt = radius + cliffDrop;

            if (state.Phase == 0)
            {
                if (state.Cursor == 0)
                    state.Random = new Random(plan.Seed ^ 0x51E5u);

                int rowEnd = math.min(skirt * 2 + 1, state.Cursor + 4);
                for (; state.Cursor < rowEnd; state.Cursor++)
                {
                    int z = state.Cursor - skirt;
                    for (int x = -skirt; x <= skirt; x++)
                    {
                        int wx = plan.Centre.x + x;
                        int wz = plan.Centre.z + z;
                        float d = math.sqrt(x * x + z * z);

                        float angle = math.atan2(z, x);
                        float wobble = math.sin(angle * 3.7f) * 18f
                                     + math.sin(angle * 8.3f) * 9f
                                     + math.sin(angle * 17.1f) * 4f;

                        float edge = radius + wobble;
                        if (d > edge + cliffDrop) continue;

                        int ground = TerrainSampler.HeightAt(wx, wz, terrainSeed);
                        int target;
                        if (d <= edge)
                        {
                            target = top;
                        }
                        else
                        {
                            float t = (d - edge) / cliffDrop;
                            float broken = math.pow(t, 1.7f)
                                         + math.sin(angle * 11f + t * 6f) * 0.10f;
                            target = (int)math.round(math.lerp(
                                top, ground - 14, math.saturate(broken)));
                        }

                        if (target <= ground)
                        {
                            authoring.FillColumnBulk(
                                wx, target + 1, ground + 1, wz, GameMaterialIds.Empty);
                        }
                        else
                        {
                            int stoneBottom = math.max(ground, target - 2);
                            authoring.FillColumnBulk(
                                wx, ground, stoneBottom, wz, GameMaterialIds.DarkStone);
                            authoring.FillColumnBulk(
                                wx, stoneBottom, target + 1, wz, GameMaterialIds.Stone);
                        }

                        if (d < edge - 12 && state.Random.NextInt(0, 100) < 92)
                            authoring.FillColumnBulk(
                                wx, target, target + 1, wz, GameMaterialIds.Grass);
                    }
                }

                if (state.Cursor <= skirt * 2) return false;
                state.Phase = 1;
                state.Cursor = 0;
            }

            if (state.Phase == 1)
            {
                int reach = plan.PlateauRadius + cliffDrop - 8;
                int columnEnd = math.min(reach * 2 + 1, state.Cursor + 2);
                LowerRiverGorge(authoring, in plan, top, state.Cursor, columnEnd, reach);
                state.Cursor = columnEnd;
                if (state.Cursor <= reach * 2) return false;

                state.Phase = 2;
                state.Cursor = 0;
            }

            if (!moat.Enabled)
            {
                state.Phase = 3;
                return true;
            }

            int2 outer = moat.OuterHalfExtents;
            int totalRows = outer.y * 2 + 1;
            int moatRowEnd = math.min(totalRows, state.Cursor + 2);
            AuthorMoatRows(
                authoring,
                in plan,
                in moat,
                in palette,
                top,
                state.Cursor,
                moatRowEnd);
            state.Cursor = moatRowEnd;
            if (state.Cursor < totalRows) return false;

            state.Phase = 3;
            return true;
        }

        private static void AuthorMoatRows(
            IStructureAuthoringSession authoring,
            in CastlePlan plan,
            in CastleMoatConfig moat,
            in StructureMaterialPalette palette,
            int top,
            int firstRow,
            int endRow)
        {
            int2 inner = moat.InnerHalfExtents;
            int2 outer = moat.OuterHalfExtents;
            int bottom = top - moat.Depth;
            byte bedMaterial = palette.Resolve(moat.BedMaterialRole);

            for (int row = firstRow; row < endRow; row++)
            {
                int localZ = -outer.y + row;
                int absZ = math.abs(localZ);

                for (int localX = -outer.x; localX <= outer.x; localX++)
                {
                    int absX = math.abs(localX);
                    if (absX <= inner.x && absZ <= inner.y)
                        continue;

                    int wx = plan.Centre.x + localX;
                    int wz = plan.Centre.z + localZ;
                    int existingSurface = HighestSolid(authoring, wx, wz, top + 8, bottom - 8);
                    if (existingSurface < bottom)
                        continue;

                    // The vertical edit interval is fixed by config and the already-bounded local
                    // ring. No terrain ray/scan can expand the horizontal footprint.
                    authoring.FillColumnBulk(
                        wx,
                        bottom + 1,
                        math.max(top + 9, existingSurface + 2),
                        wz,
                        GameMaterialIds.Empty);
                    authoring.FillColumnBulk(
                        wx,
                        bottom,
                        bottom + 1,
                        wz,
                        bedMaterial);

                    if (moat.WaterDepth > 0)
                    {
                        authoring.FillColumnBulk(
                            wx,
                            bottom + 1,
                            bottom + moat.WaterDepth + 1,
                            wz,
                            GameMaterialIds.Water);
                    }
                }
            }
        }

        private static void LowerRiverGorge(
            IStructureAuthoringSession authoring,
            in CastlePlan plan,
            int top,
            int firstColumn,
            int endColumn,
            int reach)
        {
            int gateZ = plan.Centre.z - plan.BaileyHalfZ;
            int riverZ = gateZ - plan.WallThickness - 92;
            const int halfWidth = 90;
            const int waterHalfWidth = 42;
            int riverY = top - CastleLayout.LowerRiverDepth;

            for (int column = firstColumn; column < endColumn; column++)
            {
                int x = plan.Centre.x - reach + column;
                int meander = (int)math.round(math.sin((x - plan.Centre.x) * 0.028f) * 8f
                                            + math.sin((x - plan.Centre.x) * 0.071f) * 3f);
                int channelZ = riverZ + meander;

                for (int dz = -halfWidth; dz <= halfWidth; dz++)
                {
                    int z = channelZ + dz;
                    int existingSurface = HighestSolid(
                        authoring, x, z, top + 5, riverY - 30);
                    if (existingSurface < riverY - 20) continue;

                    float across = math.abs(dz) / (float)halfWidth;
                    float bank = math.smoothstep(0.18f, 1f, across);
                    int authoredTerrace = dz < 0 ? top - 32 : top - 1;
                    int terraceTop = math.min(authoredTerrace, existingSurface);
                    int surface = (int)math.round(math.lerp(riverY - 9, terraceTop, bank));

                    authoring.FillColumnBulk(
                        x, surface + 1, math.max(top + 8, existingSurface + 2), z,
                        GameMaterialIds.Empty);

                    int dirtDepth = across > 0.46f ? 5 : 2;
                    authoring.FillColumnBulk(
                        x, surface - dirtDepth, surface, z,
                        across > 0.38f ? GameMaterialIds.Dirt : GameMaterialIds.DarkStone);
                    if (across > 0.56f)
                        authoring.FillColumnBulk(
                            x, surface, surface + 1, z, GameMaterialIds.Grass);

                    if (math.abs(dz) <= waterHalfWidth)
                    {
                        int bed = riverY - 10
                                + (int)math.round(math.abs(dz) * 4f / waterHalfWidth);
                        authoring.FillColumnBulk(
                            x, bed, riverY + 1, z, GameMaterialIds.Water);
                    }
                }
            }
        }

        private static int HighestSolid(
            IStructureAuthoringSession authoring,
            int x,
            int z,
            int fromY,
            int minY)
        {
            for (int y = fromY; y >= minY; y--)
                if (authoring.IsSolid(x, y, z)) return y;
            return minY;
        }
    }
}
