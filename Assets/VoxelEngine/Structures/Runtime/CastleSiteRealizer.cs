using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using TerrainSampler = VoxelEngine.Terrain.Api.TerrainQuery;
using Random = Unity.Mathematics.Random;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Compatibility realization for the historical castle site recipe. Spatial castles delegate
    /// to CastlePlannedSiteRealizer so planner-owned geometry and variation stay out of this path.
    /// </summary>
    internal static class CastleSiteRealizer
    {
        private const uint SiteRandomElementId = 0x53495445u; // "SITE"

        internal struct State
        {
            public int Phase;
            public int Cursor;
            public Random Random;
        }

        internal static bool Step(
            ref VoxelBrush brush,
            in CastlePlan plan,
            uint terrainSeed,
            ref State state)
        {
            int top = plan.Centre.y + plan.PlateauHeight;
            int radius = plan.PlateauRadius;
            int skirt = radius + plan.CliffDrop;

            if (state.Phase == 0)
            {
                if (state.Cursor == 0)
                {
                    uint siteSeed = CastleSeedPartition.Derive(
                        plan.Seed, CastleSeedDomain.Decor, SiteRandomElementId);
                    state.Random = new Random(siteSeed);
                }

                int rowEnd = math.min(skirt * 2 + 1, state.Cursor + 4);
                for (; state.Cursor < rowEnd; state.Cursor++)
                {
                    int z = state.Cursor - skirt;
                    for (int x = -skirt; x <= skirt; x++)
                    {
                        int wx = plan.Centre.x + x;
                        int wz = plan.Centre.z + z;
                        float distance = math.sqrt(x * x + z * z);
                        float angle = math.atan2(z, x);
                        float wobble = math.sin(angle * 3.7f) * 18f
                                     + math.sin(angle * 8.3f) * 9f
                                     + math.sin(angle * 17.1f) * 4f;
                        float edge = radius + wobble;
                        if (distance > edge + plan.CliffDrop) continue;

                        int ground = TerrainSampler.HeightAt(wx, wz, terrainSeed);
                        int target;
                        if (distance <= edge)
                        {
                            target = top;
                        }
                        else
                        {
                            float t = (distance - edge) / plan.CliffDrop;
                            float broken = math.pow(t, 1.7f)
                                         + math.sin(angle * 11f + t * 6f) * 0.10f;
                            target = (int)math.round(math.lerp(
                                top,
                                ground - 14,
                                math.saturate(broken)));
                        }

                        if (target <= ground)
                        {
                            brush.FillColumnBulk(wx, target + 1, ground + 1, wz, Mat.Empty);
                        }
                        else
                        {
                            int stoneBottom = math.max(ground, target - 2);
                            brush.FillColumnBulk(wx, ground, stoneBottom, wz, Mat.DarkStone);
                            brush.FillColumnBulk(wx, stoneBottom, target + 1, wz, Mat.Stone);
                        }

                        if (distance < edge - 12 && state.Random.NextInt(0, 100) < 92)
                            brush.FillColumnBulk(wx, target, target + 1, wz, Mat.Grass);
                    }
                }

                if (state.Cursor <= skirt * 2) return false;
                state.Phase = 1;
                state.Cursor = 0;
            }

            int reach = plan.PlateauRadius + plan.CliffDrop - 8;
            int columnEnd = math.min(reach * 2 + 1, state.Cursor + 2);
            LowerRiverGorge(ref brush, in plan, top, state.Cursor, columnEnd, reach);
            state.Cursor = columnEnd;
            return state.Cursor > reach * 2;
        }

        /// <summary>
        /// Stable bridge retained until CastleBuildPipeline owns separate legacy/planned site state.
        /// No spatial geometry or authored random choice is made here.
        /// </summary>
        internal static bool StepPlanned(
            ref VoxelBrush brush,
            in CastlePlan plan,
            uint terrainSeed,
            in CastleApproachFrame approach,
            in CastleSitePlan sitePlan,
            ref State state)
        {
            var plannedState = new CastlePlannedSiteRealizer.State
            {
                Phase = state.Phase,
                Cursor = state.Cursor,
            };
            bool complete = CastlePlannedSiteRealizer.Step(
                ref brush,
                in plan,
                terrainSeed,
                in approach,
                in sitePlan,
                ref plannedState);
            state.Phase = plannedState.Phase;
            state.Cursor = plannedState.Cursor;
            return complete;
        }

        private static void LowerRiverGorge(
            ref VoxelBrush brush,
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
                int meander = (int)math.round(
                    math.sin((x - plan.Centre.x) * 0.028f) * 8f
                  + math.sin((x - plan.Centre.x) * 0.071f) * 3f);
                int channelZ = riverZ + meander;

                for (int dz = -halfWidth; dz <= halfWidth; dz++)
                {
                    int z = channelZ + dz;
                    SculptRiverColumn(
                        ref brush,
                        x,
                        z,
                        top,
                        riverY,
                        dz,
                        halfWidth,
                        waterHalfWidth,
                        dz < 0);
                }
            }
        }

        private static void SculptRiverColumn(
            ref VoxelBrush brush,
            int x,
            int z,
            int top,
            int riverY,
            int across,
            int halfWidth,
            int waterHalfWidth,
            bool outsideBank)
        {
            int existingSurface = HighestSolid(ref brush, x, z, top + 5, riverY - 30);
            if (existingSurface < riverY - 20) return;

            float normalizedAcross = math.abs(across) / (float)halfWidth;
            float bank = math.smoothstep(0.18f, 1f, normalizedAcross);
            int authoredTerrace = outsideBank ? top - 32 : top - 1;
            int terraceTop = math.min(authoredTerrace, existingSurface);
            int surface = (int)math.round(math.lerp(riverY - 9, terraceTop, bank));

            brush.FillColumnBulk(x, surface + 1,
                                 math.max(top + 8, existingSurface + 2), z, Mat.Empty);

            int dirtDepth = normalizedAcross > 0.46f ? 5 : 2;
            brush.FillColumnBulk(x, surface - dirtDepth, surface, z,
                                 normalizedAcross > 0.38f ? Mat.Dirt : Mat.DarkStone);
            if (normalizedAcross > 0.56f)
                brush.FillColumnBulk(x, surface, surface + 1, z, Mat.Grass);

            if (math.abs(across) <= waterHalfWidth)
            {
                int bed = riverY - 10
                        + (int)math.round(math.abs(across) * 4f / waterHalfWidth);
                brush.FillColumnBulk(x, bed, riverY + 1, z, Mat.Water);
            }
        }

        private static int HighestSolid(ref VoxelBrush brush, int x, int z, int fromY, int minY)
        {
            for (int y = fromY; y >= minY; y--)
                if (brush.IsSolid(x, y, z)) return y;

            return minY;
        }
    }
}
