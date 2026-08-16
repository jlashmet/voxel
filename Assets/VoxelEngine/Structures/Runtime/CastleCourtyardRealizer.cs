using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using Random = Unity.Mathematics.Random;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>Realizes the occupied bailey space between the defensive shell and the keep.</summary>
    internal static class CastleCourtyardRealizer
    {
        internal static void Build(ref VoxelBrush brush, in CastlePlan plan)
        {
            int baseY = plan.Centre.y + plan.PlateauHeight;
            var rng = new Random(plan.Seed ^ 0xC0DEu);

            // Paving in the middle, worn to dirt at the edges.
            for (int z = -plan.BaileyHalfZ + 40; z < plan.BaileyHalfZ - 40; z++)
            for (int x = -plan.BaileyHalfX + 40; x < plan.BaileyHalfX - 40; x++)
            {
                byte material = rng.NextInt(0, 100) < 82 ? Mat.Stone : Mat.Dirt;
                brush.FillColumnBulk(plan.Centre.x + x, baseY, baseY + 1,
                                     plan.Centre.z + z, material);
            }

            // A well.
            int wx = plan.Centre.x - plan.BaileyHalfX / 2;
            int wz = plan.Centre.z + plan.BaileyHalfZ / 3;
            BuildWell(ref brush, wx, wz, baseY);

            // Lean-to outbuildings against the inside of the wall.
            for (int i = 0; i < 3; i++)
            {
                int bx = plan.Centre.x - plan.BaileyHalfX + 60 + i * 150;
                int bz = plan.Centre.z + plan.BaileyHalfZ - 130;
                int w = rng.NextInt(70, 100);
                int d = rng.NextInt(60, 84);
                int h = rng.NextInt(56, 76);

                brush.HollowBox(new int3(bx, baseY, bz), new int3(w, h, d),
                                5, Mat.Stone, false, false);
                brush.Box(new int3(bx + w / 2 - 9, baseY, bz),
                          new int3(18, 30, 5), Mat.Empty);
                brush.Gable(new int3(bx - 4, baseY + h, bz - 4),
                            new int3(w + 8, 30, d + 8), true, Mat.Tile);
            }
        }

        /// <summary>
        /// Realizes only geometry that is meaningful for an arbitrary planned perimeter. Paving is
        /// clipped to the polygon and the well is placed beside the keep while the old axis-aligned
        /// rear-wall sheds remain on the legacy path until semantic building placement exists.
        /// </summary>
        internal static void BuildPlanned(
            ref VoxelBrush brush,
            in CastlePlan plan,
            int2[] localPerimeter,
            in CastleGatePlacementSpec primaryGate,
            int2 localKeepCentre)
        {
            if (localPerimeter == null || localPerimeter.Length < 3)
                return;

            int minX = localPerimeter[0].x;
            int maxX = minX;
            int minZ = localPerimeter[0].y;
            int maxZ = minZ;
            for (int i = 1; i < localPerimeter.Length; i++)
            {
                minX = math.min(minX, localPerimeter[i].x);
                maxX = math.max(maxX, localPerimeter[i].x);
                minZ = math.min(minZ, localPerimeter[i].y);
                maxZ = math.max(maxZ, localPerimeter[i].y);
            }

            int baseY = plan.Centre.y + plan.PlateauHeight;
            var rng = new Random(CastleSeedPartition.Derive(
                plan.Seed, CastleSeedDomain.Decor, 0xC047u));
            for (int z = minZ; z <= maxZ; z++)
            for (int x = minX; x <= maxX; x++)
            {
                var local = new int2(x, z);
                if (!CastlePolygonGeometry.ContainsPoint(local, localPerimeter))
                    continue;

                byte material = rng.NextInt(0, 100) < 82 ? Mat.Stone : Mat.Dirt;
                brush.FillColumnBulk(plan.Centre.x + x, baseY, baseY + 1,
                                     plan.Centre.z + z, material);
            }

            if (TryChooseWell(
                    in plan, localPerimeter, in primaryGate, localKeepCentre,
                    out int2 localWell))
            {
                BuildWell(
                    ref brush,
                    plan.Centre.x + localWell.x,
                    plan.Centre.z + localWell.y,
                    baseY);
            }
        }

        private static bool TryChooseWell(
            in CastlePlan plan,
            int2[] perimeter,
            in CastleGatePlacementSpec gate,
            int2 keepCentre,
            out int2 well)
        {
            float2 approach = new float2(gate.Centre.x - keepCentre.x,
                                         gate.Centre.y - keepCentre.y);
            float length = math.length(approach);
            float2 direction = length > 0.001f ? approach / length : new float2(0f, -1f);
            float2 tangent = new float2(-direction.y, direction.x);
            int sideDistance = math.max(plan.KeepHalfX, plan.KeepHalfZ) + 58;
            int sideSign = (CastleSeedPartition.Derive(
                plan.Seed, CastleSeedDomain.Decor, 0xC048u) & 1u) == 0u ? -1 : 1;

            int2 first = Round(new float2(keepCentre.x, keepCentre.y)
                               + tangent * (sideSign * sideDistance));
            if (WellFits(in plan, perimeter, keepCentre, first))
            {
                well = first;
                return true;
            }

            int2 second = Round(new float2(keepCentre.x, keepCentre.y)
                                - tangent * (sideSign * sideDistance));
            if (WellFits(in plan, perimeter, keepCentre, second))
            {
                well = second;
                return true;
            }

            well = default;
            return false;
        }

        private static bool WellFits(
            in CastlePlan plan,
            int2[] perimeter,
            int2 keepCentre,
            int2 candidate)
        {
            const int clearanceRadius = 20;
            int2[] probes =
            {
                candidate,
                candidate + new int2(clearanceRadius, 0),
                candidate + new int2(-clearanceRadius, 0),
                candidate + new int2(0, clearanceRadius),
                candidate + new int2(0, -clearanceRadius),
            };
            for (int i = 0; i < probes.Length; i++)
            {
                if (!CastlePolygonGeometry.ContainsPoint(probes[i], perimeter))
                    return false;
            }

            return math.abs(candidate.x - keepCentre.x) > plan.KeepHalfX + clearanceRadius
                || math.abs(candidate.y - keepCentre.y) > plan.KeepHalfZ + clearanceRadius;
        }

        private static void BuildWell(ref VoxelBrush brush, int wx, int wz, int baseY)
        {
            brush.Cylinder(wx, baseY + 1, wz, 16, 12, Mat.DarkStone, 11);
            brush.Cylinder(wx, baseY - 60, wz, 11, 60, Mat.Empty);
            brush.Cylinder(wx, baseY - 60, wz, 10, 14, Mat.Water);
        }

        private static int2 Round(float2 value) =>
            new int2((int)math.round(value.x), (int)math.round(value.y));
    }
}
