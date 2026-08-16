using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using Random = Unity.Mathematics.Random;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Realizes one occupied defensive tower from an already-planned castle.
    /// Shared tower geometry lives here so compound structures such as the curtain defenses and
    /// the keep can reuse the same physical vocabulary without owning each other's orchestration.
    /// </summary>
    internal static class CastleTowerRealizer
    {
        internal static void Build(ref VoxelBrush brush, in CastlePlan plan, int3 at,
                                   int radius, int height, bool roof)
        {
            // Base, slightly wider.
            brush.Cylinder(at.x, at.y - 30, at.z, radius + 4, 42, Mat.DarkStone);

            // Shaft, hollow so it can hold a stair.
            brush.Cylinder(at.x, at.y, at.z, radius, height, Mat.Stone, radius - 12);

            // Floors inside.
            for (int f = 1; f * plan.FloorHeight < height - 20; f++)
                brush.Disc(at.x, at.y + f * plan.FloorHeight, at.z, radius - 13, Mat.Wood);

            // Spiral stair up the shaft.
            brush.SpiralStair(at.x, at.y + 2, at.z, radius - 14, height - 24, Mat.Stone);

            // Shallow floor-height belt courses break the otherwise uninterrupted cylinder into
            // occupied storeys. They project only three voxels from the outside skin and never
            // enter the stair room.
            for (int y = at.y + plan.FloorHeight; y < at.y + height - 28;
                 y += plan.FloorHeight)
            {
                brush.Cylinder(at.x, y - 2, at.z, radius + 2, 3,
                               Mat.DarkStone, radius - 1);
            }

            // Every tower needs a real ground-floor entrance. Aim it toward the castle centre.
            CarveTowerDoor(ref brush, in plan, at, radius);

            // Arrow slits, three per floor, staggered. Keep the historical seed derivation so the
            // refactor does not alter existing castle silhouettes.
            var rng = new Random((uint)(at.x * 8191 + at.z * 131071) | 1u);
            for (int f = 0; f * plan.FloorHeight < height - 40; f++)
            {
                int y = at.y + f * plan.FloorHeight + 18;
                float phase = rng.NextFloat(0f, 6.28f);

                for (int s = 0; s < 3; s++)
                {
                    float a = phase + s * 2.09f;
                    for (int r = radius - 14; r <= radius; r++)
                    for (int h = 0; h < 22; h++)
                    {
                        int x = at.x + (int)math.round(math.cos(a) * r);
                        int z = at.z + (int)math.round(math.sin(a) * r);
                        brush.Set(x, y + h, z, Mat.Empty);
                    }
                }
            }

            // Corbel course, then parapet.
            int parapetY = at.y + height;
            brush.Cylinder(at.x, parapetY - 4, at.z, radius + 3, 5,
                           Mat.DarkStone, radius - 14);
            brush.Cylinder(at.x, parapetY, at.z, radius + 2, 6,
                           Mat.Stone, radius - 12);
            brush.CrenellateRing(at.x, parapetY + 6, at.z, radius + 2, 18, Mat.Stone);

            if (!roof) return;

            brush.Cone(at.x, parapetY + 8, at.z, radius - 4, radius * 2, Mat.Slate);
            int peakY = parapetY + 8 + radius * 2;
            brush.Box(new int3(at.x, peakY, at.z), new int3(2, 30, 2), Mat.Wood);
            brush.Box(new int3(at.x + 2, peakY + 17, at.z), new int3(22, 11, 2), Mat.Cloth);
            brush.Set(at.x, peakY + 30, at.z, Mat.Gold);
        }

        private static void CarveTowerDoor(ref VoxelBrush brush, in CastlePlan plan,
                                           int3 at, int radius)
        {
            const int width = 14;
            const int height = 30;
            int dx = plan.Centre.x - at.x;
            int dz = plan.Centre.z - at.z;

            if (math.abs(dx) > math.abs(dz))
            {
                int minX = dx >= 0 ? at.x + radius - 15 : at.x - radius - 1;
                brush.Arch(new int3(minX, at.y + 2, at.z - width / 2),
                           width, height, 16, 0, Mat.Empty);
            }
            else
            {
                int minZ = dz >= 0 ? at.z + radius - 15 : at.z - radius - 1;
                brush.Arch(new int3(at.x - width / 2, at.y + 2, minZ),
                           width, height, 16, 2, Mat.Empty);
            }
        }
    }
}
