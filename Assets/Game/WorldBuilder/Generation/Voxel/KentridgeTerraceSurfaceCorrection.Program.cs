using System;
using System.Collections.Generic;
using Unity.Mathematics;
using VoxelEngine.Terrain.Api;

using VoxelEngine.Structures.Api;

namespace MountingForce.WorldGen.Voxel
{
    public static partial class KentridgeTerraceSurfaceCorrectionCatalogue
    {
        private const int MarketUpperTransitionBandDm = 2;
        private const int MarketUpperWestInsetDm = 220;
        private const int MarketUpperEastInsetDm = 90;
        private const int CivicUpperTransitionBandDm = 2;
        private const int CivicUpperWestInsetDm = 20;

        private static void ResolveBounds(Patch patch, uint seed, int scale,
                                          out int3 position, out int3 footprint)
        {
            int minX = patch.XDm - patch.ShoulderDm;
            int maxX = patch.XDm + patch.WidthDm + patch.ShoulderDm;
            int minZ = patch.ZDm - patch.ShoulderDm;
            int maxZ = patch.ZDm + patch.DepthDm + patch.ShoulderDm;
            int target = KentridgeVerticalProfile.SurfaceYAtDm(
                patch.AnchorXDm, patch.AnchorZDm, seed, scale);
            int minY = target, maxY = target;
            Sample(minX, minZ, seed, scale, ref minY, ref maxY);
            Sample(maxX, minZ, seed, scale, ref minY, ref maxY);
            Sample(minX, maxZ, seed, scale, ref minY, ref maxY);
            Sample(maxX, maxZ, seed, scale, ref minY, ref maxY);
            Sample((minX + maxX) / 2, (minZ + maxZ) / 2,
                   seed, scale, ref minY, ref maxY);

            int pad = VerticalPaddingDm * scale;
            int baseY = Math.Max(0, minY - pad);
            int topY = Math.Min(TerrainQuery.MaxHeight, maxY + pad);
            position = new int3(minX * scale, baseY, minZ * scale);
            footprint = new int3(
                Math.Max(1, (maxX - minX) * scale),
                Math.Max(1, topY - baseY + 1),
                Math.Max(1, (maxZ - minZ) * scale));
        }

        private static void Sample(int xDm, int zDm, uint seed, int scale,
                                   ref int minY, ref int maxY)
        {
            int y = TerrainQuery.HeightAt(xDm * scale, zDm * scale, seed);
            minY = Math.Min(minY, y);
            maxY = Math.Max(maxY, y);
        }

        private static int[] Program(Patch patch, int3 footprint,
                                     VoxelWorldGenSettings settings)
        {
            int s = settings.VoxelsPerDecimetre;
            byte stone = settings.Materials.Resolve(MaterialRole.FoundationStone);
            byte moss = settings.Materials.Resolve(MaterialRole.Moss);
            byte dirt = settings.Materials.Resolve(MaterialRole.RoadSurface);
            byte paving = settings.Materials.Resolve(MaterialRole.DarkMasonry);
            var b = new ProgramBuilder();

            if (patch.UrbanCore)
            {
                // The district terrace owns the transition shoulder shape. Keep the correction's
                // solid/paved repair constrained to the built core so it cannot stamp a rectangular
                // high-precedence surface across authored roads and natural ground.
                int coreX = patch.ShoulderDm * s;
                int coreZ = patch.ShoulderDm * s;
                int coreWidth = patch.WidthDm * s;
                int coreDepth = patch.DepthDm * s;
                b.Box(coreX, 0, coreZ, coreWidth, footprint.y, coreDepth,
                      stone, PrimitiveMode.PaintSolid);
                b.Box(coreX, 0, coreZ, coreWidth, footprint.y, coreDepth,
                      paving, PrimitiveMode.PaintSurface);

                if (patch.Id == "market-main")
                    PaintMarketToUpperTransition(
                        b, patch, footprint, s, moss, dirt);
                else if (patch.Id == "upper-shoulder")
                    PaintCivicToUpperWestTransition(
                        b, patch, footprint, s, moss, dirt);
            }
            else
            {
                // Green/mixed residential corrections still repair the full natural-ground patch.
                b.Box(0, 0, 0, footprint.x, footprint.y, footprint.z,
                      stone, PrimitiveMode.PaintSolid);
                b.Box(0, 0, 0, footprint.x, footprint.y, footprint.z,
                      moss, PrimitiveMode.PaintSurface);
            }

            return b.Finish();
        }

        private static void PaintMarketToUpperTransition(
            ProgramBuilder b, Patch patch, int3 footprint, int scale,
            byte moss, byte dirt)
        {
            int shoulder = patch.ShoulderDm * scale;
            int bandWidth = MarketUpperTransitionBandDm * scale;
            int bandCount = patch.ShoulderDm / MarketUpperTransitionBandDm;
            if (bandCount < 2 || shoulder <= 0)
                return;

            // market-main is substantially wider than upper-shoulder. Their old rectangular
            // footprints therefore met at one large 90-degree Dirt/grass notch. First restore the
            // whole north transition strip to natural surface, then reclaim Dirt in narrow bands
            // that expand continuously from the upper terrace width to the market terrace width.
            b.Box(0, 0, 0, footprint.x, footprint.y, shoulder,
                  moss, PrimitiveMode.PaintSurface);

            int westOuterInset = MarketUpperWestInsetDm * scale;
            int eastOuterInset = MarketUpperEastInsetDm * scale;
            int denominator = bandCount - 1;
            for (int band = 0; band < bandCount; band++)
            {
                int remaining = denominator - band;
                int westInset = westOuterInset * remaining / denominator;
                int eastInset = eastOuterInset * remaining / denominator;
                int x = westInset;
                int width = footprint.x - westInset - eastInset;
                int z = band * bandWidth;
                int depth = band == bandCount - 1
                    ? shoulder - z
                    : bandWidth;
                b.Box(x, 0, z, width, footprint.y, depth,
                      dirt, PrimitiveMode.PaintSurface);
            }
        }

        private static void PaintCivicToUpperWestTransition(
            ProgramBuilder b, Patch patch, int3 footprint, int scale,
            byte moss, byte dirt)
        {
            int shoulder = patch.ShoulderDm * scale;
            int bandWidth = CivicUpperTransitionBandDm * scale;
            int bandCount = patch.ShoulderDm / CivicUpperTransitionBandDm;
            if (bandCount < 2 || shoulder <= 0)
                return;

            // civic-summit's west envelope begins 20 dm east of upper-shoulder's. Across the
            // civic south-shoulder / upper core overlap that difference used to appear as one
            // rectangular grass tongue. Restore just the upper west shoulder in that overlap, then
            // reclaim Dirt in narrow bands so the west edge moves continuously from +20 dm to 0.
            int transitionZ = shoulder;
            b.Box(0, 0, transitionZ, shoulder, footprint.y, shoulder,
                  moss, PrimitiveMode.PaintSurface);

            int westOuterInset = CivicUpperWestInsetDm * scale;
            int denominator = bandCount - 1;
            for (int band = 0; band < bandCount; band++)
            {
                int remaining = denominator - band;
                int westInset = westOuterInset * remaining / denominator;
                int z = transitionZ + band * bandWidth;
                int depth = band == bandCount - 1
                    ? shoulder - band * bandWidth
                    : bandWidth;
                b.Box(westInset, 0, z, shoulder - westInset, footprint.y, depth,
                      dirt, PrimitiveMode.PaintSurface);
            }
        }

        private sealed class ProgramBuilder
        {
            private readonly List<int> _code = new();

            public void Box(int x, int y, int z, int sx, int sy, int sz,
                            byte material, PrimitiveMode mode)
            {
                _code.Add((int)ShapeOp.EmitBox); _code.Add(0);
                _code.Add(x); _code.Add(y); _code.Add(z);
                _code.Add(sx); _code.Add(sy); _code.Add(sz);
                _code.Add(material); _code.Add(0); _code.Add(0); _code.Add((int)mode);
            }

            public int[] Finish()
            {
                _code.Add((int)ShapeOp.End); _code.Add(0);
                return _code.ToArray();
            }
        }
    }
}
