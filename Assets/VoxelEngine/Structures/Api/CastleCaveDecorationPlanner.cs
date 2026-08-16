using System;
using System.Collections.Generic;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Freezes castle-specific natural-cave dressing before Runtime. The formulas and RNG sequence
    /// intentionally match the current planned-cave decorator so moving these decisions upstream
    /// does not perturb existing appearance.
    /// </summary>
    public static class CastleCaveDecorationPlanner
    {
        private const uint DecorSalt = 0x4445434Fu; // "DECO"

        public static CastleCaveDecorationPlan Create(CavePlan cave)
        {
            if (!CavePlanValidator.TryValidate(cave, out CavePlanIssue issue))
                throw new InvalidOperationException($"Cannot plan decoration for invalid cave: {issue}.");

            CaveChamberPlan[] chambers = cave.Chambers;
            var elements = new List<CastleCaveDecorationSpec>(chambers.Length * 8 + 2);
            AddEntryPool(elements, in chambers[cave.EntryChamberId]);

            uint randomSeed = cave.Seed ^ DecorSalt;
            if (randomSeed == 0u) randomSeed = 1u;
            var rng = new Random(randomSeed);

            for (int i = 0; i < chambers.Length; i++)
            {
                CaveChamberPlan chamber = chambers[i];
                AddCrystalCluster(elements, in chamber, i);
                AddFormations(elements, in chamber, i == cave.EntryChamberId, ref rng);
                AddLightMarker(elements, in chamber, i);
            }

            CastleCaveDecorationSpec[] planned = elements.ToArray();
            for (int i = 0; i < planned.Length; i++)
            {
                CastleCaveDecorationSpec spec = planned[i];
                spec.Id = i;
                planned[i] = spec;
            }

            return new CastleCaveDecorationPlan(cave.Seed, planned);
        }

        private static void AddEntryPool(
            List<CastleCaveDecorationSpec> elements,
            in CaveChamberPlan chamber)
        {
            int radius = math.max(8, math.min(chamber.Radii.x, chamber.Radii.z) / 2);
            int floor = chamber.Centre.y - chamber.Radii.y + 1;
            int depth = math.clamp(chamber.Radii.y / 4, 4, 10);
            elements.Add(new CastleCaveDecorationSpec
            {
                ChamberId = chamber.Id,
                Kind = CastleCaveDecorationKind.EntryPool,
                Position = new int3(chamber.Centre.x, floor, chamber.Centre.z),
                Radius = radius,
                Height = depth,
            });

            int halfPath = math.max(2, radius / 10);
            elements.Add(new CastleCaveDecorationSpec
            {
                ChamberId = chamber.Id,
                Kind = CastleCaveDecorationKind.DryCauseway,
                Position = new int3(
                    chamber.Centre.x - halfPath,
                    floor,
                    chamber.Centre.z - radius),
                Size = new int3(halfPath * 2 + 1, 2, radius * 2 + 1),
            });
        }

        private static void AddCrystalCluster(
            List<CastleCaveDecorationSpec> elements,
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

            AddSpire(elements, chamber.Id, CastleCaveDecorationKind.CrystalSpire,
                new int3(x, floor, z), 3, height);
            AddSpire(elements, chamber.Id, CastleCaveDecorationKind.MossSpire,
                new int3(x - side * 5, floor, z + 3), 2, math.max(5, height - 5));
            AddSpire(elements, chamber.Id, CastleCaveDecorationKind.CrystalSpire,
                new int3(x + side * 4, floor, z + 4), 2, math.max(6, height - 3));
        }

        private static void AddFormations(
            List<CastleCaveDecorationSpec> elements,
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
                bool hanging = (i & 1) != 0;
                AddSpire(
                    elements,
                    chamber.Id,
                    hanging ? CastleCaveDecorationKind.Stalactite
                            : CastleCaveDecorationKind.Stalagmite,
                    new int3(x, hanging ? roof : floor, z),
                    radius,
                    height);
            }
        }

        private static void AddLightMarker(
            List<CastleCaveDecorationSpec> elements,
            in CaveChamberPlan chamber,
            int chamberIndex)
        {
            int side = (chamberIndex & 1) == 0 ? -1 : 1;
            int x = chamber.Centre.x + side * math.max(8, chamber.Radii.x / 4);
            int z = chamber.Centre.z + math.max(6, chamber.Radii.z / 6);
            int y = chamber.Centre.y - math.max(1, chamber.Radii.y / 4);
            elements.Add(new CastleCaveDecorationSpec
            {
                ChamberId = chamber.Id,
                Kind = CastleCaveDecorationKind.LightMarker,
                Position = new int3(x, y, z),
            });
        }

        private static void AddSpire(
            List<CastleCaveDecorationSpec> elements,
            int chamberId,
            CastleCaveDecorationKind kind,
            int3 position,
            int radius,
            int height)
        {
            elements.Add(new CastleCaveDecorationSpec
            {
                ChamberId = chamberId,
                Kind = kind,
                Position = position,
                Radius = radius,
                Height = height,
            });
        }
    }
}
