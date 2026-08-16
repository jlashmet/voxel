using System;
using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Runtime/presentation-facing projection of a validated spatial castle plan. The semantic
    /// spatial plan remains the source of truth; this view exists only to bridge legacy keep-local
    /// geometry and the shared primary-gate basis while those consumers are migrated.
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
    /// Pure projection helpers shared by Runtime and application presentation. This is the single
    /// owner of the historical +60 Z keep-authoring offset; callers should never reproduce it.
    /// </summary>
    public static class CastleSpatialLayoutProjector
    {
        public const int LegacyKeepCentreZOffset = 60;

        public static CastleSpatialLayoutProjection Project(
            in CastlePlan plan,
            CastleSpatialPlan spatial)
        {
            if (spatial == null) throw new ArgumentNullException(nameof(spatial));
            if (spatial.KeepRequiresTerrainResolution)
            {
                throw new InvalidOperationException(
                    "Castle spatial layout must resolve terrain-dependent keep placement before projection.");
            }

            CastlePlan keepPlan = PlaceLegacyKeepRecipe(in plan, spatial.KeepCentre);
            CastleGatePlacementSpec primaryGate = spatial.PrimaryGate;
            CastleGateGeometry gateGeometry = CastleGateGeometryResolver.Resolve(
                in plan, in primaryGate);
            return new CastleSpatialLayoutProjection(in keepPlan, in gateGeometry);
        }

        /// <summary>
        /// Converts the semantic actual keep centre into the CastlePlan anchor expected by the
        /// extracted legacy keep/dungeon recipe. The returned plan is compatibility data only.
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

        /// <summary>Returns the actual world-space X/Z centre represented by a projected keep plan.</summary>
        public static int2 ActualKeepCentre(in CastlePlan projectedKeepPlan) =>
            new int2(
                projectedKeepPlan.Centre.x,
                projectedKeepPlan.Centre.z + LegacyKeepCentreZOffset);
    }
}
