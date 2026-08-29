using System;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Reusable route-shaping policy for a long authored cave. Positions are normalized to the
    /// configured primary-route length so callers can change segment count/length without copying
    /// showcase-specific segment indices. Bend geometry is expressed relative to each resolved
    /// primary segment endpoint. Naturalization settings widen the rectangular guaranteed-clearance
    /// core into an overlapping irregular void while leaving dogleg windows under dogleg ownership.
    /// </summary>
    public struct UndergroundCavernTraversalProfile
    {
        public int[] BendPositionsPermille;
        public int[] RouteLightPositionsPermille;
        public int[] BendForwardOffsets;
        public int[] BendSideOffsets;
        public int BendSideReach;
        public int BendRadius;
        public int NaturalizationSpacing;
        public int NaturalizationRadius;
        public int NaturalizationRadiusVariation;
        public int NaturalizationHeightVariation;
        public int NaturalizationLateralJitter;

        public bool IsWellFormed =>
            BendPositionsPermille != null && BendPositionsPermille.Length >= 2 &&
            RouteLightPositionsPermille != null && RouteLightPositionsPermille.Length >= 2 &&
            BendForwardOffsets != null && BendSideOffsets != null &&
            BendForwardOffsets.Length >= 5 && BendForwardOffsets.Length == BendSideOffsets.Length &&
            BendSideReach >= 8 && BendRadius >= 6 &&
            NaturalizationSpacing >= 8 && NaturalizationSpacing <= 32 &&
            NaturalizationRadius >= 10 && NaturalizationRadius <= 32 &&
            NaturalizationRadiusVariation >= 0 && NaturalizationRadiusVariation <= 10 &&
            NaturalizationHeightVariation >= 0 && NaturalizationHeightVariation <= 24 &&
            NaturalizationLateralJitter >= 0 && NaturalizationLateralJitter <= 12 &&
            NaturalizationRadius - NaturalizationRadiusVariation >= 8 &&
            StrictlyIncreasingPermille(BendPositionsPermille) &&
            StrictlyIncreasingPermille(RouteLightPositionsPermille);

        public static UndergroundCavernTraversalProfile LongDescent =>
            new UndergroundCavernTraversalProfile
            {
                BendPositionsPermille = new[] { 293, 534, 741 },
                RouteLightPositionsPermille = new[] { 138, 293, 448, 603, 759, 897 },
                BendForwardOffsets = new[] { -30, -20, -10, 2, 14, 26, 32 },
                BendSideOffsets = new[] { 0, 10, 22, 32, 30, 16, 2 },
                BendSideReach = 32,
                BendRadius = 16,
                NaturalizationSpacing = 13,
                NaturalizationRadius = 19,
                NaturalizationRadiusVariation = 4,
                NaturalizationHeightVariation = 12,
                NaturalizationLateralJitter = 6,
            };

        public int[] ResolveBendSegments(in CaveConfig cave) =>
            ResolveSegments(BendPositionsPermille, in cave);

        public int[] ResolveRouteLightSegments(in CaveConfig cave) =>
            ResolveSegments(RouteLightPositionsPermille, in cave);

        private static int[] ResolveSegments(int[] positionsPermille, in CaveConfig cave)
        {
            if (positionsPermille == null) return Array.Empty<int>();
            var segments = new int[positionsPermille.Length];
            int previous = 0;
            int max = math.max(1, cave.MainSegmentCount - 2);
            for (int i = 0; i < positionsPermille.Length; i++)
            {
                int resolved = (positionsPermille[i] * cave.MainSegmentCount + 500) / 1000;
                resolved = math.clamp(resolved, 1, max);
                if (i > 0) resolved = math.max(previous + 1, resolved);
                resolved = math.min(max, resolved);
                segments[i] = resolved;
                previous = resolved;
            }
            return segments;
        }

        private static bool StrictlyIncreasingPermille(int[] values)
        {
            if (values == null || values.Length == 0) return false;
            int previous = 0;
            for (int i = 0; i < values.Length; i++)
            {
                int value = values[i];
                if (value <= previous || value >= 1000) return false;
                previous = value;
            }
            return true;
        }
    }
}