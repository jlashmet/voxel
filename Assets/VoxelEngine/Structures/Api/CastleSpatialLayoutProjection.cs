using System;
using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Runtime-ready projection of a validated spatial castle plan. KeepPlan preserves the
    /// historical keep recipe's internal anchor convention while PrimaryGate remains the
    /// authoritative world-space gate geometry used by realization and interaction.
    /// </summary>
    public readonly struct CastleSpatialLayoutProjection
    {
        public readonly CastlePlan KeepPlan;
        public readonly CastleGateGeometry PrimaryGate;

        internal CastleSpatialLayoutProjection(
            in CastlePlan keepPlan,
            in CastleGateGeometry primaryGate)
        {
            KeepPlan = keepPlan;
            PrimaryGate = primaryGate;
        }
    }

    /// <summary>
    /// Pure coordinate projection shared by Runtime and Composition. This is migration glue only:
    /// semantic placement stays in CastleSpatialPlan while legacy keep-local recipes consume the
    /// projected CastlePlan until those recipes are rewritten around explicit local frames.
    /// </summary>
    public static class CastleSpatialLayoutProjector
    {
        private const int LegacyKeepCentreZOffset = 60;

        public static CastleSpatialLayoutProjection Resolve(
            in CastlePlan plan,
            CastleSpatialPlan spatial)
        {
            if (spatial == null) throw new ArgumentNullException(nameof(spatial));
            if (!CastleSpatialPlanValidator.TryValidate(
                    in plan, spatial, out CastleSpatialPlanIssue issue))
            {
                throw new InvalidOperationException(
                    $"Cannot project invalid castle spatial plan: {issue}.");
            }

            if (spatial.KeepRequiresTerrainResolution)
            {
                throw new InvalidOperationException(
                    "Cannot project a castle whose keep still requires terrain resolution.");
            }

            CastlePlan keepPlan = PlaceLegacyKeepRecipe(in plan, spatial.KeepCentre);
            CastleGatePlacementSpec primaryGate = spatial.PrimaryGate;
            CastleGateGeometry gateGeometry = CastleGateGeometryResolver.Resolve(
                in plan, in primaryGate);
            return new CastleSpatialLayoutProjection(in keepPlan, in gateGeometry);
        }

        /// <summary>
        /// Projects an actual local keep centre into the anchor expected by the extracted legacy
        /// keep/dungeon recipe. Callers should prefer Resolve when a complete spatial plan exists.
        /// </summary>
        public static CastlePlan PlaceLegacyKeepRecipe(
            in CastlePlan plan,
            int2 localKeepCentre)
        {
            CastlePlan placed = plan;
            placed.Centre = new int3(
                plan.Centre.x + localKeepCentre.x,
                plan.Centre.y,
                plan.Centre.z + localKeepCentre.y - LegacyKeepCentreZOffset);
            return placed;
        }

        /// <summary>Returns the actual world X/Z keep centre represented by a projected plan.</summary>
        public static int2 ActualKeepCentre(in CastlePlan projectedKeepPlan) =>
            new int2(
                projectedKeepPlan.Centre.x,
                projectedKeepPlan.Centre.z + LegacyKeepCentreZOffset);
    }
}
