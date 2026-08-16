using System;
using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Pure world-space projection of a validated spatial castle plan onto the still-legacy keep
    /// coordinate recipe plus authoritative interactive gate geometry. This keeps realization,
    /// presentation, and interaction on one placement transform while the keep recipe is migrated.
    /// </summary>
    public readonly struct CastleSpatialLayoutProjection
    {
        public readonly CastlePlan KeepPlan;
        public readonly CastleGateGeometry PrimaryGate;
        public readonly int3 TrapdoorCentre;
        public readonly int3 ChapelBellTowerCentre;

        internal CastleSpatialLayoutProjection(
            in CastlePlan keepPlan,
            in CastleGateGeometry primaryGate,
            int3 trapdoorCentre,
            int3 chapelBellTowerCentre)
        {
            KeepPlan = keepPlan;
            PrimaryGate = primaryGate;
            TrapdoorCentre = trapdoorCentre;
            ChapelBellTowerCentre = chapelBellTowerCentre;
        }
    }

    /// <summary>
    /// Shared projection helpers for consumers that need the concrete world coordinates implied by
    /// CastleSpatialPlan. No terrain, storage, rendering, or runtime mutation state is consulted.
    /// </summary>
    public static class CastleSpatialLayoutProjector
    {
        private const int LegacyKeepCentreZOffset = 60;

        public static CastleSpatialLayoutProjection Project(
            in CastlePlan plan,
            CastleSpatialPlan spatial)
        {
            if (spatial == null) throw new ArgumentNullException(nameof(spatial));
            if (!CastleSpatialPlanValidator.TryValidate(
                    in plan, spatial, out CastleSpatialPlanIssue issue))
            {
                throw new InvalidOperationException(
                    $"Cannot project an invalid castle spatial plan: {issue}.");
            }

            if (spatial.KeepRequiresTerrainResolution)
            {
                throw new InvalidOperationException(
                    "Castle spatial plan still requires terrain resolution for its keep.");
            }

            CastlePlan keepPlan = ProjectKeepPlan(in plan, spatial.KeepCentre);
            CastleGatePlacementSpec primaryGatePlacement = spatial.PrimaryGate;
            CastleGateGeometry primaryGate = CastleGateGeometryResolver.Resolve(
                in plan, in primaryGatePlacement);
            int3 trapdoorCentre = CastleLayout.TrapdoorCentre(in keepPlan);
            int3 chapelBellTowerCentre = CastleLayout.ChapelBellTowerCentre(in keepPlan);

            return new CastleSpatialLayoutProjection(
                in keepPlan,
                in primaryGate,
                trapdoorCentre,
                chapelBellTowerCentre);
        }

        /// <summary>
        /// Translates the semantic keep centre into the temporary CastlePlan centre expected by the
        /// extracted legacy keep/dungeon recipe. Callers should not reproduce the historical +60 Z
        /// offset themselves.
        /// </summary>
        public static CastlePlan ProjectKeepPlan(
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

        /// <summary>Returns the actual world X/Z centre represented by a projected keep plan.</summary>
        public static int2 ActualKeepCentre(in CastlePlan projectedKeepPlan) =>
            new int2(
                projectedKeepPlan.Centre.x,
                projectedKeepPlan.Centre.z + LegacyKeepCentreZOffset);
    }
}
