using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using TerrainSampler = VoxelEngine.Terrain.Api.TerrainQuery;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Realizes the frozen spatial castle site recipe. All authored site shape, surface variation,
    /// and river cross-section policy arrive through CastleSitePlan; this component owns only
    /// deterministic terrain queries and voxel mutation.
    /// </summary>
    internal static class CastlePlannedSiteRealizer
    {
        internal struct State
        {
            public int Phase;
            public int Cursor;
        }

        internal static bool Step(
            ref VoxelBrush brush,
            in CastlePlan plan,
            uint terrainSeed,
            in CastleApproachFrame approach,
            in CastleSitePlan sitePlan,
            ref State state)
        {
            int top = plan.Centre.y + plan.PlateauHeight;
            int radius = plan.PlateauRadius;
            int skirt = radius + plan.CliffDrop;
            CastleSiteGeometryPlan geometry = sitePlan.Geometry;

            if (state.Phase == 0)
            {
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
                        float wobble =
                            math.sin(angle * geometry.EdgeFrequencyA) * geometry.EdgeAmplitudeA
                          + math.sin(angle * geometry.EdgeFrequencyB) * geometry.EdgeAmplitudeB
                          + math.sin(angle * geometry.EdgeFrequencyC) * geometry.EdgeAmplitudeC;
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
                            float broken = math.pow(t, geometry.CliffFalloffExponent)
                                         + math.sin(
                                             angle * geometry.CliffNoiseAngularFrequency
                                             + t * geometry.CliffNoiseProgressFrequency)
                                           * geometry.CliffNoiseAmplitude;
                            target = (int)math.round(math.lerp(
                                top,
                                ground - geometry.CliffGroundInset,
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

                        if (distance < edge - geometry.GrassEdgeInset
                            && sitePlan.ShouldGrassCap(x, z))
                        {
                            brush.FillColumnBulk(wx, target, target + 1, wz, Mat.Grass);
                        }
                    }
                }

                if (state.Cursor <= skirt * 2) return false;
                state.Phase = 1;
                state.Cursor = 0;
            }

            int reach = plan.PlateauRadius + plan.CliffDrop - geometry.ApproachReachInset;
            int columnEnd = math.min(reach * 2 + 1, state.Cursor + 2);
            LowerRiverGorge(
                ref brush,
                in plan,
                in approach,
                in geometry,
                top,
                state.Cursor,
                columnEnd,
                reach);
            state.Cursor = columnEnd;
            return state.Cursor > reach * 2;
        }

        private static void LowerRiverGorge(
            ref VoxelBrush brush,
            in CastlePlan plan,
            in CastleApproachFrame approach,
            in CastleSiteGeometryPlan geometry,
            int top,
            int firstColumn,
            int endColumn,
            int reach)
        {
            int halfWidth = geometry.RiverHalfWidth;
            int waterHalfWidth = geometry.WaterHalfWidth;
            int riverY = top - geometry.RiverDepth;
            float riverDistance = plan.WallThickness + geometry.RiverOffset;
            CastleRiverCrossSectionPlan crossSection = geometry.RiverCrossSection;

            for (int column = firstColumn; column < endColumn; column++)
            {
                float along = -reach + column;
                int meander = (int)math.round(
                    math.sin(along * geometry.MeanderFrequencyA) * geometry.MeanderAmplitudeA
                  + math.sin(along * geometry.MeanderFrequencyB) * geometry.MeanderAmplitudeB);

                for (int across = -halfWidth; across <= halfWidth; across++)
                {
                    int2 local = approach.LocalPoint(
                        along,
                        riverDistance - meander + across);
                    int x = plan.Centre.x + local.x;
                    int z = plan.Centre.z + local.y;
                    SculptRiverColumn(
                        ref brush,
                        x,
                        z,
                        top,
                        riverY,
                        across,
                        halfWidth,
                        waterHalfWidth,
                        across > 0,
                        in crossSection);
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
            bool outsideBank,
            in CastleRiverCrossSectionPlan crossSection)
        {
            int existingSurface = HighestSolid(ref brush, x, z, top + 5, riverY - 30);
            if (existingSurface < riverY - crossSection.ExistingSurfaceRejectDepth) return;

            float normalizedAcross = math.abs(across) / (float)halfWidth;
            float bank = math.smoothstep(
                crossSection.BankBlendStart,
                crossSection.BankBlendEnd,
                normalizedAcross);
            int authoredTerrace = outsideBank
                ? top - crossSection.OutsideTerraceDrop
                : top - crossSection.InsideTerraceDrop;
            int terraceTop = math.min(authoredTerrace, existingSurface);
            int surface = (int)math.round(math.lerp(riverY - 9, terraceTop, bank));

            brush.FillColumnBulk(
                x,
                surface + 1,
                math.max(top + crossSection.SurfaceClearance, existingSurface + 2),
                z,
                Mat.Empty);

            int dirtDepth = normalizedAcross > crossSection.DeepSoilThreshold
                ? crossSection.DeepSoilDepth
                : crossSection.ShallowSoilDepth;
            brush.FillColumnBulk(
                x,
                surface - dirtDepth,
                surface,
                z,
                normalizedAcross > crossSection.LooseBankThreshold
                    ? Mat.Dirt
                    : Mat.DarkStone);
            if (normalizedAcross > crossSection.GrassThreshold)
                brush.FillColumnBulk(x, surface, surface + 1, z, Mat.Grass);

            if (math.abs(across) <= waterHalfWidth)
            {
                int bed = riverY - crossSection.BedDepth
                        + (int)math.round(
                            math.abs(across) * crossSection.BedRise / (float)waterHalfWidth);
                brush.FillColumnBulk(x, bed, riverY + 1, z, Mat.Water);
            }
        }

        private static int HighestSolid(ref VoxelBrush brush, int x, int z, int fromY, int minY)
        {
            for (int y = fromY; y >= minY; y--)
            {
                if (brush.IsSolid(x, y, z)) return y;
            }
            return minY;
        }
    }
}
