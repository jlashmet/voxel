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
        private const uint SiteRandomElementId = 0x53495445u; // "SITE"

        internal struct State
        {
            public int Phase;
            public int Cursor;
            public Random Random;
        }

        internal static bool Step(ref VoxelBrush brush, in CastlePlan plan, uint terrainSeed,
                                  ref State state)
        {
            CastleApproachFrame unusedApproach = default;
            CastleSitePlan unusedSite = default;
            return StepCore(
                ref brush, in plan, terrainSeed, false, in unusedApproach, in unusedSite, ref state);
        }

        internal static bool StepPlanned(
            ref VoxelBrush brush,
            in CastlePlan plan,
            uint terrainSeed,
            in CastleApproachFrame approach,
            in CastleSitePlan sitePlan,
            ref State state) =>
            StepCore(
                ref brush, in plan, terrainSeed, true, in approach, in sitePlan, ref state);

        private static bool StepCore(
            ref VoxelBrush brush,
            in CastlePlan plan,
            uint terrainSeed,
            bool hasPlannedApproach,
            in CastleApproachFrame approach,
            in CastleSitePlan sitePlan,
            ref State state)
        {
            int top = plan.Centre.y + plan.PlateauHeight;
            int radius = plan.PlateauRadius;
            int skirt = radius + plan.CliffDrop;

            if (state.Phase == 0)
            {
                if (state.Cursor == 0 && !hasPlannedApproach)
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

                        bool grassCap = hasPlannedApproach
                            ? sitePlan.ShouldGrassCap(x, z)
                            : state.Random.NextInt(0, 100) < 92;
                        if (d < edge - 12 && grassCap)
                            brush.FillColumnBulk(wx, target, target + 1, wz, Mat.Grass);
                    }
                }

                if (state.Cursor <= skirt * 2) return false;
                state.Phase = 1;
                state.Cursor = 0;
            }

            int reach = plan.PlateauRadius + plan.CliffDrop - 8;
            int columnEnd = math.min(reach * 2 + 1, state.Cursor + 2);
            if (hasPlannedApproach)
            {
                LowerRiverGorgePlanned(
                    ref brush, in plan, in approach, top, state.Cursor, columnEnd, reach);
            }
            else
            {
                LowerRiverGorgeLegacy(
                    ref brush, in plan, top, state.Cursor, columnEnd, reach);
            }
            state.Cursor = columnEnd;
            return state.Cursor > reach * 2;
        }

        /// <summary>
        /// Cuts the historical -Z approach shelf into two terrain levels. Kept for compatibility
        /// builds; spatial builds use the gate-oriented version below.
        /// </summary>
        private static void LowerRiverGorgeLegacy(
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
                int meander = (int)math.round(math.sin((x - plan.Centre.x) * 0.028f) * 8f
                                            + math.sin((x - plan.Centre.x) * 0.071f) * 3f);
                int channelZ = riverZ + meander;

                for (int dz = -halfWidth; dz <= halfWidth; dz++)
                {
                    int z = channelZ + dz;
                    SculptRiverColumn(
                        ref brush, x, z, top, riverY, dz, halfWidth, waterHalfWidth,
                        dz < 0);
                }
            }
        }

        /// <summary>
        /// Same authored gorge profile as the legacy path, expressed in the primary gate's local
        /// tangent/outward frame. The river runs parallel to the gate and its lower terrace lies on
        /// the outside of the castle regardless of perimeter orientation.
        /// </summary>
        private static void LowerRiverGorgePlanned(
            ref VoxelBrush brush,
            in CastlePlan plan,
            in CastleApproachFrame approach,
            int top,
            int firstColumn,
            int endColumn,
            int reach)
        {
            const int halfWidth = 90;
            const int waterHalfWidth = 42;
            int riverY = top - CastleLayout.LowerRiverDepth;
            float riverDistance = plan.WallThickness + 92f;

            for (int column = firstColumn; column < endColumn; column++)
            {
                float along = -reach + column;
                int meander = (int)math.round(math.sin(along * 0.028f) * 8f
                                            + math.sin(along * 0.071f) * 3f);

                for (int across = -halfWidth; across <= halfWidth; across++)
                {
                    // Positive across is farther outside because CastleApproachFrame.Outward points
                    // away from the castle. In the legacy -Z frame it equals -dz. Meander is
                    // subtracted in outward-distance space so the legacy reduction remains
                    // channelZ = riverZ + meander.
                    int2 local = approach.LocalPoint(
                        along,
                        riverDistance - meander + across);
                    int x = plan.Centre.x + local.x;
                    int z = plan.Centre.z + local.y;
                    SculptRiverColumn(
                        ref brush, x, z, top, riverY, across, halfWidth, waterHalfWidth,
                        across > 0);
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
