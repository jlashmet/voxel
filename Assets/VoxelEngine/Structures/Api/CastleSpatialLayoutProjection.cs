using System;
using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Read-only compatibility projection of a resolved spatial castle plan. Semantic placement
    /// remains owned by CastleSpatialPlan; this value only maps that placement onto legacy keep
    /// authoring coordinates and the shared world-space primary-gate geometry.
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
    /// Pure API projection shared by runtime realization and application interaction/presentation.
    /// It is the only public castle-planning component that knows the historical keep recipe uses
    /// a +60 Z centre offset internally.
    /// </summary>
    public static class CastleSpatialLayoutProjector
    {
        private const int LegacyKeepCentreZOffset = 60;

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

            if (!CastleSpatialPlanValidator.TryValidate(
                    in plan, spatial, out CastleSpatialPlanIssue issue))
            {
                throw new InvalidOperationException(
                    $"Cannot project invalid castle spatial layout: {issue}.");
            }

            CastlePlan keepPlan = plan;
            keepPlan.Centre = new int3(
                plan.Centre.x + spatial.KeepCentre.x,
                plan.Centre.y,
                plan.Centre.z + spatial.KeepCentre.y - LegacyKeepCentreZOffset);

            CastleGatePlacementSpec primaryGatePlacement = spatial.PrimaryGate;
            CastleGateGeometry primaryGate = CastleGateGeometryResolver.Resolve(
                in plan, in primaryGatePlacement);
            return new CastleSpatialLayoutProjection(in keepPlan, in primaryGate);
        }
    }
}
