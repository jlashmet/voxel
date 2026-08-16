using System;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using Random = Unity.Mathematics.Random;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Castle-specific dressing for an already planned natural cave. CavePlan owns all chamber and
    /// passage topology; this component only maps supplied decoration semantics to voxel materials.
    /// </summary>
    public static class CastlePlannedCaveDecorator
    {
        private const uint DecorSalt = 0x4445434Fu; // "DECO"

        /// <summary>
        /// Compatibility entry point retained while callers migrate. New spatial builds must pass
        /// a precomputed CastleCaveDecorationPlan to the overload below.
        /// </summary>
        public static void Build(ref VoxelBrush brush, CavePlan plan)
        {
            if (!CavePlanValidator.TryValidate(plan, out CavePlanIssue issue))
                throw new InvalidOperationException($"Cannot decorate invalid cave plan: {issue}.");

            CaveChamberPlan[] chambers = plan.Chambers;
            if (chambers.Length == 0) return;

            BuildEntryPool(ref brush, in chambers[plan.EntryChamberId]);

            uint randomSeed = plan.Seed ^ DecorSalt;
            if (randomSeed == 0u) randomSeed = 1u;
            var rng = new Random(randomSeed);

            for (int i = 0; i < chambers.Length; i++)
            {
                CaveChamberPlan chamber = chambers[i];
                BuildCrystalCluster(ref brush, in chamber, i);
                BuildFormations(ref brush, in chamber, i == plan.EntryChamberId, ref rng);
                BuildLightMarker(ref brush, in chamber, i);
            }
        }

        /// <summary>
        /// Realizes already-planned castle cave dressing. This path contains no random choices or
        /// chamber-index placement policy; it only interprets stable decoration specs.
        /// </summary>
        public static void Build(
            ref VoxelBrush brush,
            CavePlan cave,
            CastleCaveDecorationPlan decoration)
        {
            if (!CastleCaveDecorationPlanValidator.TryValidate(
                    cave, decoration, out CastleCaveDecorationPlanIssue issue))
            {
                throw new InvalidOperationException(
                    $"Cannot realize invalid castle cave decoration plan: {issue}.");
            }

            CastleCaveDecorationSpec[] elements = decoration.Elements;
            for (int i = 0; i < elements.Length; i++)
            {
                CastleCaveDecorationSpec spec = elements[i];
                BuildPlannedElement(ref brush, in spec);
            }
        }

        private static void BuildPlannedElement(
            ref VoxelBrush brush,
            in CastleCaveDecorationSpec spec)
        {
            switch (spec.Kind)
            {
                case CastleCaveDecorationKind.EntryPool:
                {
                    int radiusSq = spec.Radius * spec.Radius;
                    for (int dz = -spec.Radius; dz <= spec.Radius; dz++)
                    for (int dx = -spec.Radius; dx <= spec.Radius; dx++)
                    {
                        if (dx * dx + dz * dz > radiusSq) continue;
                        brush.FillColumnBulk(
                            spec.Position.x + dx,
                            spec.Position.y,
                            spec.Position.y + spec.Height,
                            spec.Position.z + dz,
                            Mat.Water);
                    }
                    break;
                }

                case CastleCaveDecorationKind.DryCauseway:
                    brush.Box(spec.Position, spec.Size, Mat.DarkStone);
                    break;

                case CastleCaveDecorationKind.CrystalSpire:
                    brush.Cone(
                        spec.Position.x, spec.Position.y, spec.Position.z,
                        spec.Radius, spec.Height, Mat.Crystal);
                    break;

                case CastleCaveDecorationKind.MossSpire:
                    brush.Cone(
                        spec.Position.x, spec.Position.y, spec.Position.z,
                        spec.Radius, spec.Height, Mat.Moss);
                    break;

                case CastleCaveDecorationKind.Stalagmite:
                    brush.Cone(
                        spec.Position.x, spec.Position.y, spec.Position.z,
                        spec.Radius, spec.Height, Mat.DarkStone);
                    break;

                case CastleCaveDecorationKind.Stalactite:
                    brush.HangingCone(
                        spec.Position.x, spec.Position.y, spec.Position.z,
                        spec.Radius, spec.Height, Mat.DarkStone);
                    break;

                case CastleCaveDecorationKind.LightMarker:
                    brush.Box(spec.Position, new int3(1, 3, 1), Mat.Glass);
                    brush.Box(
                        spec.Position - new int3(1, 1, 1),
                        new int3(3, 1, 3),
                        Mat.Gold);
                    break;

                default:
                    throw new InvalidOperationException(
                        $"Unsupported castle cave decoration kind: {spec.Kind}.");
            }
        }

        private static void BuildEntryPool(ref VoxelBrush brush, in CaveChamberPlan chamber)
        {
            int radius = math.max(8, math.min(chamber.Radii.x, chamber.Radii.z) / 2);
            int floor = chamber.Centre.y - chamber.Radii.y + 1;
            int depth = math.clamp(chamber.Radii.y / 4, 4, 10);
            int radiusSq = radius * radius;

            for (int dz = -radius; dz <= radius; dz++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (dx * dx + dz * dz > radiusSq) continue;
                brush.FillColumnBulk(
                    chamber.Centre.x + dx,
                    floor,
                    floor + depth,
                    chamber.Centre.z + dz,
                    Mat.Water);
            }

            int halfPath = math.max(2, radius / 10);
            brush.Box(
                new int3(
                    chamber.Centre.x - halfPath,
                    floor,
                    chamber.Centre.z - radius),
                new int3(halfPath * 2 + 1, 2, radius * 2 + 1),
                Mat.DarkStone);
        }

        private static void BuildCrystalCluster(
            ref VoxelBrush brush,
            in CaveChamberPlan chamber,
            int chamberIndex)
        {
            int side = (chamberIndex & 1) == 0 ? 1 : -1;
            int offsetX = side * math.max(7, chamber.Radii.x / 3);
            int offsetZ = ((chamberIndex % 3) - 1) * math.max(5, chamber.Radii.z / 5);
            int floor = chamber.Centre.y - chamber.Radii.y + 2;
            int x = chamber.Centre.x + offsetX;
            int z = chamber.Centre.z + offsetZ;
            int height = math.clamp(chamber.Radii.y / 3, 7, 16);

            brush.Cone(x, floor, z, 3, height, Mat.Crystal);
            brush.Cone(x - side * 5, floor, z + 3, 2, math.max(5, height - 5), Mat.Moss);
            brush.Cone(x + side * 4, floor, z + 4, 2, math.max(6, height - 3), Mat.Crystal);
        }

        private static void BuildFormations(
            ref VoxelBrush brush,
            in CaveChamberPlan chamber,
            bool entryChamber,
            ref Random rng)
        {
            int count = entryChamber ? 5 : 3;
            float usableX = math.max(8f, chamber.Radii.x * 0.62f);
            float usableZ = math.max(8f, chamber.Radii.z * 0.62f);
            int floor = chamber.Centre.y - chamber.Radii.y + 2;
            int roof = chamber.Centre.y + chamber.Radii.y - 2;

            for (int i = 0; i < count; i++)
            {
                float angle = rng.NextFloat(0f, math.PI * 2f);
                float radial = rng.NextFloat(0.58f, 0.82f);
                int x = chamber.Centre.x
                      + (int)math.round(math.cos(angle) * usableX * radial);
                int z = chamber.Centre.z
                      + (int)math.round(math.sin(angle) * usableZ * radial);
                int height = rng.NextInt(7, math.max(8, math.min(28, chamber.Radii.y)));
                int radius = rng.NextInt(2, 6);

                if ((i & 1) == 0)
                    brush.Cone(x, floor, z, radius, height, Mat.DarkStone);
                else
                    brush.HangingCone(x, roof, z, radius, height, Mat.DarkStone);
            }
        }

        private static void BuildLightMarker(
            ref VoxelBrush brush,
            in CaveChamberPlan chamber,
            int chamberIndex)
        {
            int side = (chamberIndex & 1) == 0 ? -1 : 1;
            int x = chamber.Centre.x + side * math.max(8, chamber.Radii.x / 4);
            int z = chamber.Centre.z + math.max(6, chamber.Radii.z / 6);
            int y = chamber.Centre.y - math.max(1, chamber.Radii.y / 4);

            brush.Box(new int3(x, y, z), new int3(1, 3, 1), Mat.Glass);
            brush.Box(new int3(x - 1, y - 1, z - 1), new int3(3, 1, 3), Mat.Gold);
        }
    }
}
