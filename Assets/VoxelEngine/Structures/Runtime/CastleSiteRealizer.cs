using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using TerrainSampler = VoxelEngine.Terrain.Api.TerrainQuery;
using Random = Unity.Mathematics.Random;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Realizes only the terrain/site portion of a planned castle. It owns the resumable site
    /// cursor and nothing about fortifications, interiors, dungeons, or later landscape dressing.
    /// </summary>
    internal static class CastleSiteRealizer
    {
        internal struct State
        {
            public int Phase;
            public int Cursor;
            public Random Random;
        }

        internal static bool Step(ref VoxelBrush brush, in CastlePlan plan, uint terrainSeed,
                                  ref State state)
        {
            int top = plan.Centre.y + plan.PlateauHeight;
            int radius = plan.PlateauRadius;
            int skirt = radius + plan.CliffDrop;

            if (state.Phase == 0)
            {
                if (state.Cursor == 0) state.Random = new Random(plan.Seed ^ 0x51E5u);
                int rowEnd = math.min(skirt * 2 + 1, state.Cursor + 4);
                for (; state.Cursor < rowEnd; state.Cursor++)
                {
                    int z = state.Cursor - skirt;
                    for (int x = -skirt; x <= skirt; x++)
                    {
                        int wx = plan.Centre.x + x;
                        int wz = plan.Centre.z + z;

                        float d = math.sqrt(x * x + z * z);

                        // Irregular edge: a perfectly circular plateau reads as a cake stand.
                        float angle = math.atan2(z, x);
                        float wobble = math.sin(angle * 3.7f) * 18f
                                     + math.sin(angle * 8.3f) * 9f
                                     + math.sin(angle * 17.1f) * 4f;

                        float edge = radius + wobble;
                        if (d > edge + plan.CliffDrop) continue;

                        int ground = TerrainSampler.HeightAt(wx, wz, terrainSeed);

                        int target;
                        if (d <= edge) target = top;
                        else
                        {
                            // Cliff face: steep, and broken up per column. The first version eased
                            // out of the plateau with pow(t, 0.55), which gives a long shallow
                            // shoulder — and a shallow slope in voxels is a staircase of contour
                            // terraces. Falling fast and unevenly is both more castle-like and cheaper.
                            float t = (d - edge) / plan.CliffDrop;
                            float broken = math.pow(t, 1.7f)
                                         + math.sin(angle * 11f + t * 6f) * 0.10f;

                            target = (int)math.round(math.lerp(
                                top, ground - 14, math.saturate(broken)));
                        }

                        if (target <= ground)
                            brush.FillColumnBulk(wx, target + 1, ground + 1, wz, Mat.Empty);
                        else
                        {
                            // Build the outcrop in bulk, leaving the visible cap as authored bands.
                            int stoneBottom = math.max(ground, target - 2);
                            brush.FillColumnBulk(wx, ground, stoneBottom, wz, Mat.DarkStone);
                            brush.FillColumnBulk(wx, stoneBottom, target + 1, wz, Mat.Stone);
                        }

                        if (d < edge - 12 && state.Random.NextInt(0, 100) < 92)
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
        /// Cuts the approach shelf into two terrain levels while keeping the upper gate road and
        /// the lower river authored as real voxel strata rather than presentation-only colour.
        /// </summary>
        private static void LowerRiverGorge(ref VoxelBrush brush, in CastlePlan plan, int top,
                                            int firstColumn, int endColumn, int reach)
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
                    int existingSurface = HighestSolid(ref brush, x, z, top + 5, riverY - 30);
                    if (existingSurface < riverY - 20) continue;

                    float across = math.abs(dz) / (float)halfWidth;
                    float bank = math.smoothstep(0.18f, 1f, across);
                    int authoredTerrace = dz < 0 ? top - 32 : top - 1;
                    int terraceTop = math.min(authoredTerrace, existingSurface);
                    int surface = (int)math.round(math.lerp(riverY - 9, terraceTop, bank));

                    brush.FillColumnBulk(x, surface + 1,
                                         math.max(top + 8, existingSurface + 2), z, Mat.Empty);

                    int dirtDepth = across > 0.46f ? 5 : 2;
                    brush.FillColumnBulk(x, surface - dirtDepth, surface, z,
                                         across > 0.38f ? Mat.Dirt : Mat.DarkStone);
                    if (across > 0.56f)
                        brush.FillColumnBulk(x, surface, surface + 1, z, Mat.Grass);

                    if (math.abs(dz) <= waterHalfWidth)
                    {
                        int bed = riverY - 10
                                + (int)math.round(math.abs(dz) * 4f / waterHalfWidth);
                        brush.FillColumnBulk(x, bed, riverY + 1, z, Mat.Water);
                    }
                }
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
