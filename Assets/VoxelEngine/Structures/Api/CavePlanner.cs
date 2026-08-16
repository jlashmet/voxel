using System;
using System.Collections.Generic;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Deterministically resolves natural-cave topology from an independent seed and caller-owned
    /// scale envelope. It chooses chamber placement/connectivity only; voxel carving and decoration
    /// remain downstream realization concerns.
    /// </summary>
    public static class CavePlanner
    {
        public static CavePlan Create(uint seed, in CavePlanningConstraints constraints)
        {
            ValidateConstraints(in constraints);

            int totalChambers = constraints.SecondaryChamberCount + 1;
            var chambers = new CaveChamberPlan[totalChambers];
            var passages = new List<CavePassagePlan>(constraints.SecondaryChamberCount);

            chambers[0] = new CaveChamberPlan
            {
                Id = 0,
                Centre = constraints.Entrance + constraints.EntranceToMainOffset,
                Radii = constraints.MainRadii,
                RotationRadians = 0f,
            };

            uint randomSeed = Mix(seed ^ 0xCA6E5EEDu);
            var rng = new Random(randomSeed == 0u ? 1u : randomSeed);
            int secondaryCount = constraints.SecondaryChamberCount;
            for (int i = 1; i < totalChambers; i++)
            {
                float baseAngle = secondaryCount <= 1
                    ? 0f
                    : math.PI * 2f * (i - 1) / secondaryCount;
                float angle = baseAngle + rng.NextFloat(-0.42f, 0.42f);
                int distance = NextInclusive(
                    ref rng,
                    constraints.MinimumHorizontalSpread,
                    constraints.MaximumHorizontalSpread);
                int vertical = constraints.VerticalSpread == 0
                    ? 0
                    : NextInclusive(ref rng, -constraints.VerticalSpread, constraints.VerticalSpread);
                int3 radii = new int3(
                    NextInclusive(ref rng, constraints.SecondaryMinRadii.x,
                                  constraints.SecondaryMaxRadii.x),
                    NextInclusive(ref rng, constraints.SecondaryMinRadii.y,
                                  constraints.SecondaryMaxRadii.y),
                    NextInclusive(ref rng, constraints.SecondaryMinRadii.z,
                                  constraints.SecondaryMaxRadii.z));
                int3 centre = chambers[0].Centre + new int3(
                    (int)math.round(math.cos(angle) * distance),
                    vertical,
                    (int)math.round(math.sin(angle) * distance));

                chambers[i] = new CaveChamberPlan
                {
                    Id = i,
                    Centre = centre,
                    Radii = radii,
                    RotationRadians = rng.NextFloat(0f, math.PI * 2f),
                };

                // Attach each new chamber to an already planned chamber. This allows branching
                // while guaranteeing a connected graph without a repair pass.
                int parent = rng.NextInt(0, i);
                passages.Add(new CavePassagePlan
                {
                    FromChamberId = parent,
                    ToChamberId = i,
                    Width = constraints.PassageWidth,
                    Height = constraints.PassageHeight,
                });
            }

            var plan = new CavePlan(
                seed,
                constraints.Entrance,
                chambers,
                passages.ToArray(),
                0);
            if (!CavePlanValidator.TryValidate(plan, out CavePlanIssue issue))
                throw new InvalidOperationException($"Cave planner produced an invalid plan: {issue}.");
            return plan;
        }

        private static void ValidateConstraints(in CavePlanningConstraints constraints)
        {
            if (math.any(constraints.MainRadii <= 0))
                throw new ArgumentOutOfRangeException(nameof(constraints.MainRadii));
            if (math.any(math.abs(constraints.EntranceToMainOffset) > constraints.MainRadii))
                throw new ArgumentOutOfRangeException(nameof(constraints.EntranceToMainOffset));
            if (constraints.SecondaryChamberCount < 0 || constraints.SecondaryChamberCount > 12)
                throw new ArgumentOutOfRangeException(nameof(constraints.SecondaryChamberCount));
            if (constraints.SecondaryChamberCount > 0)
            {
                if (math.any(constraints.SecondaryMinRadii <= 0) ||
                    math.any(constraints.SecondaryMaxRadii < constraints.SecondaryMinRadii))
                    throw new ArgumentOutOfRangeException(nameof(constraints.SecondaryMinRadii));
                if (constraints.MinimumHorizontalSpread < 1 ||
                    constraints.MaximumHorizontalSpread < constraints.MinimumHorizontalSpread)
                    throw new ArgumentOutOfRangeException(nameof(constraints.MinimumHorizontalSpread));
                if (constraints.VerticalSpread < 0)
                    throw new ArgumentOutOfRangeException(nameof(constraints.VerticalSpread));
                if (constraints.PassageWidth <= 0 || constraints.PassageHeight <= 0)
                    throw new ArgumentOutOfRangeException(nameof(constraints.PassageWidth));
            }
        }

        private static int NextInclusive(ref Random rng, int minimum, int maximum) =>
            minimum == maximum ? minimum : rng.NextInt(minimum, maximum + 1);

        private static uint Mix(uint value)
        {
            unchecked
            {
                value ^= value >> 16;
                value *= 0x7FEB352Du;
                value ^= value >> 15;
                value *= 0x846CA68Bu;
                value ^= value >> 16;
                return value;
            }
        }
    }
}
