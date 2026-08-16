using System;
using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// API-owned projection from semantic castle placement into the historical keep-local authoring
    /// frame plus authoritative gate geometry. Runtime and presentation clients consume this same
    /// projection so interaction, lighting, dungeon landmarks, and voxel realization cannot drift.
    /// </summary>
    public readonly struct CastleSpatialLayoutProjection
    {
        public const int LegacyKeepCentreZOffset = 60;

        public readonly CastlePlan KeepPlan;
        public readonly CastleGateGeometry PrimaryGate;

        private CastleSpatialLayoutProjection(
            CastlePlan keepPlan,
            CastleGateGeometry primaryGate)
        {
            KeepPlan = keepPlan;
            PrimaryGate = primaryGate;
        }

        public static CastleSpatialLayoutProjection Create(
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
                    "Cannot project castle layout before HighestGround keep placement is resolved.");
            }

            CastlePlan keepPlan = PlaceKeepPlan(in plan, spatial.KeepCentre);
            CastleGatePlacementSpec primary = spatial.PrimaryGate;
            CastleGateGeometry gate = CastleGateGeometryResolver.Resolve(in plan, in primary);
            return new CastleSpatialLayoutProjection(keepPlan, gate);
        }

        /// <summary>
        /// Projects the semantic keep centre into the compatibility CastlePlan expected by the
        /// extracted keep, annex, dungeon, and cave recipes. KeepCentre remains the real centre;
        /// only this compatibility view carries the legacy +60 Z authoring offset.
        /// </summary>
        public static CastlePlan PlaceKeepPlan(in CastlePlan plan, int2 localKeepCentre)
        {
            CastlePlan placed = plan;
            placed.Centre = new int3(
                plan.Centre.x + localKeepCentre.x,
                plan.Centre.y,
                plan.Centre.z + localKeepCentre.y - LegacyKeepCentreZOffset);
            return placed;
        }

        public static int2 ActualKeepCentre(in CastlePlan placedPlan) =>
            new int2(
                placedPlan.Centre.x,
                placedPlan.Centre.z + LegacyKeepCentreZOffset);
    }
}
