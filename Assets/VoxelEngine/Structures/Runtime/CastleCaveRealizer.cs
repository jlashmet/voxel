using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using Random = Unity.Mathematics.Random;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Realizes the natural cavern reached from the castle dungeon.
    /// Kept separate from designed dungeon rooms so this geometry can evolve toward a reusable
    /// cave planner/realizer without dragging castle circulation policy with it.
    /// </summary>
    internal static class CastleCaveRealizer
    {
        private const uint CaveRandomElementId = 0x43415645u; // "CAVE"

        internal static void Build(ref VoxelBrush brush, in CastlePlan plan, int3 at)
        {
            uint caveSeed = CastleSeedPartition.Derive(
                plan.Seed, CastleSeedDomain.Dungeon, CaveRandomElementId);
            var rng = new Random(caveSeed);

            CarveCavernEllipsoid(ref brush, at + new int3(0, 27, 0),
                                 new int3(82, 36, 104), 0.17f);
            CarveCavernEllipsoid(ref brush, at + new int3(-58, 23, -18),
                                 new int3(56, 30, 72), 1.43f);
            CarveCavernEllipsoid(ref brush, at + new int3(62, 25, 30),
                                 new int3(60, 33, 74), 2.71f);
            CarveCavernEllipsoid(ref brush, at + new int3(12, 31, -72),
                                 new int3(66, 37, 62), 4.19f);

            int sideCaveX = at.x + 145;
            int sideCaveZ = at.z + 25;
            brush.Box(new int3(at.x - 5, at.y + 2, sideCaveZ - 10),
                      new int3(159, 30, 20), Mat.Empty);
            brush.Box(new int3(at.x - 5, at.y - 1, sideCaveZ - 10),
                      new int3(159, 3, 20), Mat.DarkStone);

            CarveCavernEllipsoid(ref brush,
                                 new int3(sideCaveX - 10, at.y + 17, sideCaveZ - 5),
                                 new int3(40, 31, 47), 0.91f);
            CarveCavernEllipsoid(ref brush,
                                 new int3(sideCaveX + 24, at.y + 15, sideCaveZ + 16),
                                 new int3(35, 27, 38), 2.23f);
            CarveCavernEllipsoid(ref brush,
                                 new int3(sideCaveX + 3, at.y + 23, sideCaveZ - 28),
                                 new int3(31, 33, 34), 3.77f);
            brush.Box(new int3(at.x - 5, at.y - 1, sideCaveZ - 10),
                      new int3(159, 3, 20), Mat.DarkStone);
            brush.Disc(sideCaveX, at.y - 1, sideCaveZ, 28, Mat.DarkStone);

            for (int z = -44; z <= 44; z++)
            for (int x = -44; x <= 44; x++)
            {
                if (x * x + z * z > 44 * 44) continue;
                brush.FillColumnBulk(at.x + x, at.y - 12, at.y - 2,
                                     at.z + z, Mat.Water);
            }

            for (int i = 0; i < 26; i++)
            {
                int sx = at.x + rng.NextInt(-95, 95);
                int sz = at.z + rng.NextInt(-95, 95);
                if (math.abs(sx - at.x) < 24 && sz < at.z + 55 && sz > at.z - 92)
                    sx += sx < at.x ? -32 : 32;
                int h = rng.NextInt(10, 34);
                brush.Cone(sx, at.y - 2, sz, rng.NextInt(3, 7), h, Mat.DarkStone);
            }

            for (int i = 0; i < 18; i++)
            {
                int sx = at.x + rng.NextInt(-78, 78);
                int sz = at.z + rng.NextInt(-78, 78);
                brush.HangingCone(sx, at.y + rng.NextInt(48, 61), sz,
                                  rng.NextInt(3, 8), rng.NextInt(12, 31), Mat.DarkStone);
            }

            brush.Box(new int3(at.x - 5, at.y - 2, at.z - 52),
                      new int3(10, 3, 104), Mat.DarkStone);
            int2[] causewayRemains =
            {
                new(-8, -42), new(5, -13), new(-8, 25), new(5, 45),
            };
            for (int i = 0; i < causewayRemains.Length; i++)
            {
                brush.Box(new int3(at.x + causewayRemains[i].x, at.y + 1,
                                   at.z + causewayRemains[i].y),
                          new int3(3, 4 + (i & 1) * 3, 3),
                          i == 2 ? Mat.Moss : Mat.Stone);
            }

            int fallZ = at.z - 76;
            brush.Box(new int3(at.x + 15, at.y - 3, fallZ - 8),
                      new int3(24, 31, 13), Mat.Empty);
            for (int x = -8; x <= 8; x++)
            for (int z = -1; z <= 0; z++)
            {
                int topY = at.y + 27 - math.abs(x) / 3
                         - math.abs((x * 5 + z * 3) % 3);
                brush.FillColumnBulk(at.x + 27 + x, at.y - 2, topY,
                                     fallZ + z, Mat.Cascade);
            }
            brush.Disc(at.x + 27, at.y - 2, fallZ + 8, 28, Mat.Water);

            for (int side = -1; side <= 1; side += 2)
            {
                int columnX = at.x + 27 + side * 20;
                brush.Cylinder(columnX, at.y - 2, fallZ + 4, 6,
                               side < 0 ? 30 : 22, Mat.Stone);
                brush.Cylinder(columnX, at.y + (side < 0 ? 24 : 16), fallZ + 4,
                               8, 4, Mat.DarkStone);
                brush.Cone(columnX + side * 4, at.y - 1, fallZ + 10,
                           5, 8, Mat.Moss);
            }

            int3[] crystalCentres =
            {
                new(at.x - 58, at.y - 2, at.z - 34),
                new(at.x + 61, at.y - 2, at.z + 28),
                new(at.x + 48, at.y - 2, at.z - 51),
            };
            foreach (int3 crystal in crystalCentres)
            {
                brush.Cone(crystal.x, crystal.y, crystal.z, 3, 13, Mat.Crystal);
                brush.Cone(crystal.x - 5, crystal.y, crystal.z + 3, 2, 8, Mat.Moss);
                brush.Cone(crystal.x + 4, crystal.y, crystal.z + 4, 2, 10, Mat.Crystal);
            }

            brush.Disc(sideCaveX, at.y + 1, sideCaveZ, 15, Mat.Water);
            brush.Box(new int3(sideCaveX - 20, at.y + 2, sideCaveZ - 3),
                      new int3(40, 2, 6), Mat.DarkStone);

            int archX = sideCaveX + 28;
            for (int side = -1; side <= 1; side += 2)
            {
                int pillarZ = sideCaveZ + side * 16;
                brush.Cylinder(archX, at.y + 2, pillarZ, 6, 29, Mat.Stone);
                brush.Cylinder(archX, at.y + 27, pillarZ, 8, 4, Mat.DarkStone);
            }
            brush.Box(new int3(archX - 4, at.y + 28, sideCaveZ - 22),
                      new int3(8, 6, 44), Mat.DarkStone);
            brush.Box(new int3(archX + 1, at.y + 11, sideCaveZ - 3),
                      new int3(5, 14, 6), Mat.Crystal);
            brush.Box(new int3(archX - 2, at.y + 8, sideCaveZ - 6),
                      new int3(11, 4, 12), Mat.Stone);

            for (int i = 0; i < 9; i++)
            {
                float angle = i * (math.PI * 2f / 9f) + 0.23f;
                float radius = 27f + (i % 3) * 5f;
                int cx = sideCaveX + (int)math.round(math.cos(angle) * radius);
                int cz = sideCaveZ + (int)math.round(math.sin(angle) * radius);
                int crystalHeight = 7 + (i * 5 % 8);
                brush.Cone(cx, at.y + 2, cz, 2, crystalHeight,
                           i == 2 || i == 7 ? Mat.Moss : Mat.Crystal);
                if ((i & 1) == 0)
                {
                    brush.Cone(cx + 5, at.y + 2, cz - 3, 2,
                               math.max(7, crystalHeight - 6), Mat.Crystal);
                }
            }
            brush.HangingCone(sideCaveX - 25, at.y + 48, sideCaveZ - 20,
                              6, 21, Mat.DarkStone);
            brush.HangingCone(sideCaveX + 3, at.y + 51, sideCaveZ + 20,
                              7, 25, Mat.DarkStone);
            brush.HangingCone(sideCaveX + 30, at.y + 46, sideCaveZ - 15,
                              5, 18, Mat.DarkStone);

            // Reassert deterministic circulation after random cave dressing.
            brush.Box(new int3(at.x - 5, at.y + 2, sideCaveZ - 8),
                      new int3(151, 22, 16), Mat.Empty);
            brush.Box(new int3(at.x - 5, at.y - 1, sideCaveZ - 8),
                      new int3(151, 3, 16), Mat.DarkStone);

            int3[] caveLights =
            {
                new(at.x - 48, at.y + 12, at.z - 28),
                new(at.x + 44, at.y + 10, at.z - 18),
                new(at.x - 38, at.y + 14, at.z + 38),
                new(at.x + 50, at.y + 11, at.z + 32),
            };
            foreach (var light in caveLights)
            {
                brush.Box(light, new int3(1, 3, 1), Mat.Glass);
                brush.Box(light - new int3(1, 1, 1), new int3(3, 1, 3), Mat.Gold);
            }
        }

        private static void CarveCavernEllipsoid(ref VoxelBrush brush, int3 centre,
                                                 int3 radii, float phase)
        {
            float inverseX = 1f / (radii.x * radii.x);
            float inverseZ = 1f / (radii.z * radii.z);
            for (int z = -radii.z; z <= radii.z; z++)
            for (int x = -radii.x; x <= radii.x; x++)
            {
                float boundary = 1f
                    + math.sin(x * 0.091f + z * 0.037f + phase) * 0.085f
                    + math.sin(x * 0.031f - z * 0.073f + phase * 1.7f) * 0.065f
                    + math.sin((x + z) * 0.151f - phase * 0.8f) * 0.025f;
                float radial = (x * x * inverseX + z * z * inverseZ)
                             / math.max(0.76f, boundary);
                if (radial > 1f) continue;
                float profile = math.sqrt(1f - radial);
                int halfHeight = (int)math.floor(radii.y * profile);
                int floorWarp = (int)math.round(
                    math.sin(x * 0.117f + z * 0.053f + phase) * 2.2f
                  + math.sin(z * 0.181f - phase) * 1.1f);
                int roofWarp = (int)math.round(
                    math.sin(x * 0.067f - z * 0.101f + phase * 2.0f) * 3.5f
                  + math.sin((x - z) * 0.139f + phase) * 1.6f);
                brush.FillColumnBulk(centre.x + x, centre.y - halfHeight + floorWarp,
                                     centre.y + halfHeight + roofWarp + 1,
                                     centre.z + z, Mat.Empty);
            }
        }
    }
}
