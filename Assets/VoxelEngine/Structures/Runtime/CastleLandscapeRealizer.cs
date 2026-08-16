using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using Random = Unity.Mathematics.Random;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Realizes castle-local landscape dressing after all structure and underground work is done.
    /// Terrain/site sculpting remains a separate earlier stage; this pass owns waterfall framing,
    /// approach planting, wall-foot overgrowth, and cleanup of loose terrain above authored water.
    /// </summary>
    internal static class CastleLandscapeRealizer
    {
        internal static void Build(ref VoxelBrush brush, in CastlePlan plan, uint terrainSeed)
        {
            int top = plan.Centre.y + plan.PlateauHeight;
            RavineWaterfall(ref brush, in plan, terrainSeed, top);
            TreeBelt(ref brush, in plan, top);
            ApproachPlanting(ref brush, in plan, top);
            WallFootingOvergrowth(ref brush, in plan, top);
            RemoveFloatingRiverTerrain(ref brush, in plan, top);
        }

        private static void WallFootingOvergrowth(ref VoxelBrush brush, in CastlePlan plan, int top)
        {
            int gateZ = plan.Centre.z - plan.BaileyHalfZ;
            var rng = new Random(plan.Seed ^ 0xB07A11u);

            for (int side = -1; side <= 1; side += 2)
            for (int bay = 0; bay < 7; bay++)
            {
                int x = plan.Centre.x + side * (96 + bay * 24);
                if (math.abs(x - plan.Centre.x) >= plan.BaileyHalfX - plan.TowerRadius) continue;
                int z = gateZ - 17 - rng.NextInt(0, 10);
                int surface = HighestSolid(ref brush, x, z, top + 12, top - 80);
                int shrubRadius = rng.NextInt(4, 8);
                brush.Cone(x, surface + 1, z, shrubRadius, rng.NextInt(5, 11),
                           (bay & 1) == 0 ? Mat.Moss : Mat.Grass);

                int rubbleX = x + side * rng.NextInt(8, 15);
                int rubbleZ = z - rng.NextInt(2, 10);
                int rubbleY = HighestSolid(ref brush, rubbleX, rubbleZ, top + 12, top - 80);
                brush.Box(new int3(rubbleX, rubbleY + 1, rubbleZ),
                          new int3(rng.NextInt(4, 9), rng.NextInt(3, 7), rng.NextInt(4, 8)),
                          bay % 3 == 0 ? Mat.Stone : Mat.DarkStone);
            }

            int[] ivyOffsets = { -214, -162, -108, 116, 171, 218 };
            for (int i = 0; i < ivyOffsets.Length; i++)
            {
                int rootX = plan.Centre.x + ivyOffsets[i];
                if (math.abs(ivyOffsets[i]) >= plan.BaileyHalfX - plan.TowerRadius) continue;
                int ivyHeight = 24 + (i * 13 % 31);
                for (int y = 0; y < ivyHeight; y += 6)
                {
                    int width = math.max(2, 9 - y / 7);
                    int drift = ((i & 1) == 0 ? 1 : -1) * (y / 10);
                    brush.Box(new int3(rootX + drift, top + 2 + y, gateZ - 2),
                              new int3(width, math.min(7, ivyHeight - y), 2), Mat.Moss);
                }
            }

            int2[] copseOffsets =
            {
                new(-260, -82), new(-282, -48), new(266, -62), new(292, -30),
            };
            for (int i = 0; i < copseOffsets.Length; i++)
            {
                int x = plan.Centre.x + copseOffsets[i].x;
                int z = gateZ + copseOffsets[i].y;
                int surface = HighestSolid(ref brush, x, z, top + 18, top - 120);
                Pine(ref brush, x, surface + 1, z, 44 + i * 5, 13 + (i & 1) * 3,
                     i == 1 ? Mat.Moss : Mat.Grass);
            }
        }

        private static void RavineWaterfall(ref VoxelBrush brush, in CastlePlan plan,
                                            uint terrainSeed, int top)
        {
            int streamX = CastleLayout.WaterfallStreamX(in plan);
            int lipZ = CastleLayout.WaterfallLipZ(in plan);
            int riverZ = CastleLayout.LowerRiverZAt(in plan, streamX);
            int streamStartZ = plan.Centre.z + plan.BaileyHalfZ + plan.TowerRadius + 18;
            int streamLength = math.max(1, streamStartZ - lipZ);

            for (int z = streamStartZ; z >= lipZ; z--)
            {
                float t = (streamStartZ - z) / (float)streamLength;
                int centreX = streamX
                            + (int)math.round(math.sin(t * math.PI * 3.2f) * 7f);
                int halfWidth = 10 + (int)math.round(t * 5f);
                int channelY = top - 6 - (int)math.round(t * 11f);
                for (int dx = -halfWidth; dx <= halfWidth; dx++)
                {
                    float across = math.abs(dx) / (float)halfWidth;
                    int bottom = channelY + (int)math.round(across * across * 8f);
                    brush.FillColumnBulk(centreX + dx, bottom, top + 8, z, Mat.Empty);
                    if (math.abs(dx) <= halfWidth - 3)
                        brush.FillColumnBulk(centreX + dx, bottom, bottom + 3, z, Mat.Water);
                }
            }

            int poolX = streamX;
            int poolZ = riverZ + 27;
            int poolY = top - 80;
            const int poolRadiusX = 68;
            const int poolRadiusZ = 43;
            for (int dz = -poolRadiusZ; dz <= poolRadiusZ; dz++)
            for (int dx = -poolRadiusX; dx <= poolRadiusX; dx++)
            {
                float ellipse = dx * dx / (float)(poolRadiusX * poolRadiusX)
                              + dz * dz / (float)(poolRadiusZ * poolRadiusZ);
                if (ellipse > 1f) continue;
                float rim = math.saturate((ellipse - 0.66f) / 0.34f);
                int bottom = ellipse < 0.66f
                    ? poolY - 9
                    : (int)math.round(math.lerp(poolY - 9, poolY + 17,
                                                math.pow(rim, 0.72f)));
                brush.FillColumnBulk(poolX + dx, bottom, top + 7, poolZ + dz, Mat.Empty);
                if (ellipse < 0.68f)
                    brush.FillColumnBulk(poolX + dx, bottom, poolY + 1,
                                         poolZ + dz, Mat.Water);
            }

            for (int dz = -7; dz <= 7; dz++)
            for (int dx = -30; dx <= 30; dx++)
            {
                brush.FillColumnBulk(streamX + dx, poolY + 1, top - 16,
                                     lipZ + dz, Mat.Empty);
                if (dz <= 0 && dz >= -5 && math.abs(dx) <= 23)
                {
                    int edge = math.abs(dx);
                    int raggedTop = top - 16 - edge / 7
                                  - math.abs((dx * 13 + dz * 7) % 3);
                    int raggedBottom = poolY + 1 + math.max(0, edge - 18) / 2;
                    brush.FillColumnBulk(streamX + dx, raggedBottom, raggedTop,
                                         lipZ + dz, Mat.Cascade);
                }
            }

            int outletLength = math.max(1, poolZ - riverZ);
            for (int z = poolZ; z >= riverZ; z--)
            {
                float t = (poolZ - z) / (float)outletLength;
                int waterY = (int)math.round(math.lerp(poolY,
                    top - CastleLayout.LowerRiverDepth, t));
                int halfWidth = 18 + (int)math.round(t * 8f);
                for (int dx = -halfWidth; dx <= halfWidth; dx++)
                {
                    float across = math.abs(dx) / (float)halfWidth;
                    int bed = waterY - 7 + (int)math.round(across * across * 6f);
                    brush.FillColumnBulk(streamX + dx, bed, top + 5, z, Mat.Empty);
                    if (math.abs(dx) <= halfWidth - 3)
                        brush.FillColumnBulk(streamX + dx, bed, waterY + 1, z, Mat.Water);
                }
            }

            var rockRng = new Random(plan.Seed ^ 0xA11CEu);
            for (int side = -1; side <= 1; side += 2)
            for (int i = 0; i < 4; i++)
            {
                int rx = streamX + side * (34 + i * 8);
                int rz = lipZ + 5 + i * 7;
                int surface = HighestSolid(ref brush, rx, rz, top + 12, poolY - 16);
                brush.Cone(rx, surface + 1, rz, rockRng.NextInt(4, 7),
                           rockRng.NextInt(7, 14), Mat.DarkStone);
            }

            int2[] treeOffsets =
            {
                new(-88, 58), new(92, 72), new(-105, -28), new(108, -18),
            };
            for (int i = 0; i < treeOffsets.Length; i++)
            {
                int tx = poolX + treeOffsets[i].x;
                int tz = poolZ + treeOffsets[i].y;
                int surface = HighestSolid(ref brush, tx, tz, top + 24, top - 180);
                if ((i & 1) == 0)
                    Tree(ref brush, tx, surface + 1, tz, 40 + i * 3, 15, Mat.Moss);
                else
                    Pine(ref brush, tx, surface + 1, tz, 45 + i * 3, 14, Mat.Grass);
            }
        }

        private static void RemoveFloatingRiverTerrain(ref VoxelBrush brush,
                                                       in CastlePlan plan, int top)
        {
            int streamX = CastleLayout.WaterfallStreamX(in plan);
            int lipZ = CastleLayout.WaterfallLipZ(in plan);
            int riverZ = CastleLayout.LowerRiverZAt(in plan, streamX);
            const int poolRadiusX = 68;

            for (int x = streamX - poolRadiusX - 10; x <= streamX + poolRadiusX + 10; x++)
            for (int z = riverZ - 10; z <= lipZ + 30; z++)
            {
                bool waterBelow = false;
                bool structurallyAnchored = false;
                for (int y = top - CastleLayout.LowerRiverDepth - 12; y <= top + 8; y++)
                {
                    byte material = brush.Get(x, y, z);
                    if (material == Mat.Water || material == Mat.Cascade)
                    {
                        waterBelow = true;
                        structurallyAnchored = false;
                        continue;
                    }
                    if (material == Mat.Empty || !waterBelow) continue;

                    bool looseTerrain = material == Mat.Grass || material == Mat.Dirt
                                     || material == Mat.Moss || material == Mat.Sand;
                    if (looseTerrain && !structurallyAnchored)
                        brush.Set(x, y, z, Mat.Empty);
                    else
                        structurallyAnchored = true;
                }
            }
        }

        private static int HighestSolid(ref VoxelBrush brush, int x, int z, int fromY, int minY)
        {
            for (int y = fromY; y >= minY; y--)
                if (brush.IsSolid(x, y, z)) return y;
            return minY;
        }

        private static void TreeBelt(ref VoxelBrush brush, in CastlePlan plan, int top)
        {
            var rng = new Random(plan.Seed ^ 0x7EE5u);
            int built = 0;

            for (int attempt = 0; attempt < 96 && built < 22; attempt++)
            {
                float angle = rng.NextFloat(0f, math.PI * 2f);
                float radius = rng.NextFloat(plan.PlateauRadius * 0.74f,
                                             plan.PlateauRadius - 26f);
                int ox = (int)math.round(math.cos(angle) * radius);
                int oz = (int)math.round(math.sin(angle) * radius);

                bool outsideWalls = math.abs(ox) > plan.BaileyHalfX + plan.TowerRadius + 16
                                 || math.abs(oz) > plan.BaileyHalfZ + plan.TowerRadius + 16;
                bool blocksGate = oz < -plan.BaileyHalfZ && math.abs(ox) < 105;
                int waterfallOffsetX = CastleLayout.WaterfallStreamX(in plan) - plan.Centre.x;
                int waterfallOffsetZ = CastleLayout.WaterfallLipZ(in plan) - plan.Centre.z;
                bool nearWaterfall = math.abs(ox - waterfallOffsetX) < 125
                                  && math.abs(oz - waterfallOffsetZ) < 165;
                if (!outsideWalls || blocksGate || nearWaterfall) continue;

                int height = rng.NextInt(34, 58);
                int canopyRadius = rng.NextInt(12, 19);
                Tree(ref brush, plan.Centre.x + ox, top + 1, plan.Centre.z + oz,
                     height, canopyRadius, built % 3 == 0 ? Mat.Grass : Mat.Moss);
                built++;
            }
        }

        private static void ApproachPlanting(ref VoxelBrush brush, in CastlePlan plan, int top)
        {
            int gateZ = plan.Centre.z - plan.BaileyHalfZ;
            int2[] offsets =
            {
                new(-178, -92), new(168, -78), new(-235, -105), new(235, -110),
                new(-154, 42), new(184, 62),
            };

            for (int i = 0; i < offsets.Length; i++)
            {
                int x = plan.Centre.x + offsets[i].x;
                int z = gateZ + offsets[i].y;
                int surface = HighestSolid(ref brush, x, z, top + 20, top - 170);
                if ((i & 1) == 0)
                    Pine(ref brush, x, surface + 1, z, 58 + (i % 3) * 8,
                         18 + (i & 1) * 3, i % 3 == 0 ? Mat.Grass : Mat.Moss);
                else
                    Tree(ref brush, x, surface + 1, z, 44 + (i % 3) * 6,
                         15 + (i % 2) * 3, i % 3 == 0 ? Mat.Grass : Mat.Moss);

                int side = (i & 1) == 0 ? -1 : 1;
                int rockX = x + side * (13 + i % 3 * 3);
                int rockZ = z + 8 - i * 3;
                int rockY = HighestSolid(ref brush, rockX, rockZ, top + 20, top - 170);
                brush.Cone(rockX, rockY + 1, rockZ, 4 + i % 3, 6 + i % 4,
                           i % 2 == 0 ? Mat.DarkStone : Mat.Stone);
            }
        }

        private static void Pine(ref VoxelBrush brush, int x, int y, int z,
                                 int height, int radius, byte foliage)
        {
            // Trees are semantic vegetation now. ShowcaseTreePopulation owns TreeInstances.
        }

        private static void Tree(ref VoxelBrush brush, int x, int y, int z,
                                 int height, int canopyRadius, byte foliage)
        {
            // Trees are semantic vegetation now. ShowcaseTreePopulation owns TreeInstances.
        }
    }
}
