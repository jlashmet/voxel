using System;
using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>Conservative world-voxel envelope of a validated natural CavePlan.</summary>
    public readonly struct CaveBuildBounds
    {
        public readonly int3 Min;
        public readonly int3 MaxExclusive;

        internal CaveBuildBounds(int3 min, int3 maxExclusive)
        {
            Min = min;
            MaxExclusive = maxExclusive;
        }

        public bool Contains(int3 voxel) =>
            math.all(voxel >= Min) && math.all(voxel < MaxExclusive);
    }

    /// <summary>
    /// Pure bounds resolver for generic CaveRealizer geometry. The bounds mirror the chamber and
    /// passage loops used by Runtime so streaming dependencies can be established before mutation.
    /// </summary>
    public static class CaveBuildBoundsResolver
    {
        public static CaveBuildBounds Resolve(CavePlan plan)
        {
            if (!CavePlanValidator.TryValidate(plan, out CavePlanIssue issue))
                throw new InvalidOperationException($"Cave bounds require a valid plan: {issue}.");

            bool hasBounds = false;
            int3 min = default;
            int3 maxExclusive = default;

            CaveChamberPlan[] chambers = plan.Chambers;
            for (int i = 0; i < chambers.Length; i++)
            {
                CaveChamberPlan chamber = chambers[i];
                int rx = chamber.Radii.x;
                int rz = chamber.Radii.z;
                float cos = math.cos(chamber.RotationRadians);
                float sin = math.sin(chamber.RotationRadians);

                // Match CaveRealizer's rotated ellipse projection exactly. Using the unrotated
                // rx/rz box clips dependency bounds whenever a non-circular chamber is rotated.
                int extentX = (int)math.ceil(math.sqrt(
                    rx * (float)rx * cos * cos + rz * (float)rz * sin * sin));
                int extentZ = (int)math.ceil(math.sqrt(
                    rx * (float)rx * sin * sin + rz * (float)rz * cos * cos));
                var extent = new int3(extentX, chamber.Radii.y, extentZ);
                IncludeBox(
                    chamber.Centre - extent,
                    chamber.Centre + extent + 1,
                    ref hasBounds,
                    ref min,
                    ref maxExclusive);
            }

            CavePassagePlan[] passages = plan.Passages;
            if (passages != null)
            {
                for (int i = 0; i < passages.Length; i++)
                {
                    CavePassagePlan passage = passages[i];
                    CaveChamberPlan from = chambers[passage.FromChamberId];
                    CaveChamberPlan to = chambers[passage.ToChamberId];
                    int radius = math.max(1, passage.Width / 2);
                    int halfHeight = math.max(1, passage.Height / 2);
                    int3 passagePadding = new int3(radius, halfHeight, radius);
                    int3 passageMin = math.min(from.Centre, to.Centre) - passagePadding;
                    int3 passageMaxExclusive = math.max(from.Centre, to.Centre)
                                              + passagePadding + 1;
                    IncludeBox(
                        passageMin,
                        passageMaxExclusive,
                        ref hasBounds,
                        ref min,
                        ref maxExclusive);
                }
            }

            return new CaveBuildBounds(min, maxExclusive);
        }

        private static void IncludeBox(
            int3 boxMin,
            int3 boxMaxExclusive,
            ref bool hasBounds,
            ref int3 min,
            ref int3 maxExclusive)
        {
            if (!hasBounds)
            {
                min = boxMin;
                maxExclusive = boxMaxExclusive;
                hasBounds = true;
                return;
            }

            min = math.min(min, boxMin);
            maxExclusive = math.max(maxExclusive, boxMaxExclusive);
        }
    }
}
