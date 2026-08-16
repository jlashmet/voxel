using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using Random = Unity.Mathematics.Random;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Realizes one occupied defensive tower. Compatibility callers retain the historical
    /// world-position RNG for slit rotation; planned callers supply frozen per-floor phases.
    /// </summary>
    internal static class CastleTowerRealizer
    {
        internal static void Build(
            ref VoxelBrush brush,
            in CastlePlan plan,
            int3 at,
            int radius,
            int height,
            bool roof) =>
            BuildCore(ref brush, in plan, at, radius, height, roof, null);

        internal static void BuildPlanned(
            ref VoxelBrush brush,
            in CastlePlan plan,
            int3 at,
            int radius,
            int height,
            bool roof,
            CastleTowerSlitPlan slitPlan)
        {
            CastleTowerSlitPlanValidator.RequireValid(
                slitPlan, height, plan.FloorHeight);
            BuildCore(ref brush, in plan, at, radius, height, roof, slitPlan);
        }

        private static void BuildCore(
            ref VoxelBrush brush,
            in CastlePlan plan,
            int3 at,
            int radius,
            int height,
            bool roof,
            CastleTowerSlitPlan slitPlan)
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

            if (slitPlan == null)
                CarveArrowSlitsLegacy(ref brush, in plan, at, radius, height);
            else
                CarveArrowSlitsPlanned(ref brush, in plan, at, radius, slitPlan);

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

        private static void CarveArrowSlitsLegacy(
            ref VoxelBrush brush,
            in CastlePlan plan,
            int3 at,
            int radius,
            int height)
        {
            var rng = new Random((uint)(at.x * 8191 + at.z * 131071) | 1u);
            for (int floor = 0; floor * plan.FloorHeight < height - 40; floor++)
            {
                float phase = rng.NextFloat(0f, 6.28f);
                CarveArrowSlitFloor(ref brush, in plan, at, radius, floor, phase);
            }
        }

        private static void CarveArrowSlitsPlanned(
            ref VoxelBrush brush,
            in CastlePlan plan,
            int3 at,
            int radius,
            CastleTowerSlitPlan slitPlan)
        {
            for (int floor = 0; floor < slitPlan.FloorCount; floor++)
            {
                CarveArrowSlitFloor(
                    ref brush,
                    in plan,
                    at,
                    radius,
                    floor,
                    slitPlan.PhaseRadiansAt(floor));
            }
        }

        private static void CarveArrowSlitFloor(
            ref VoxelBrush brush,
            in CastlePlan plan,
            int3 at,
            int radius,
            int floor,
            float phase)
        {
            int y = at.y + floor * plan.FloorHeight + 18;
            for (int slit = 0; slit < 3; slit++)
            {
                float angle = phase + slit * 2.09f;
                for (int r = radius - 14; r <= radius; r++)
                for (int h = 0; h < 22; h++)
                {
                    int x = at.x + (int)math.round(math.cos(angle) * r);
                    int z = at.z + (int)math.round(math.sin(angle) * r);
                    brush.Set(x, y + h, z, Mat.Empty);
                }
            }
        }

        private static void CarveTowerDoor(
            ref VoxelBrush brush,
            in CastlePlan plan,
            int3 at,
            int radius)
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
