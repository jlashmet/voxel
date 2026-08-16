using System;
using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Shared projection from a validated semantic/spatial castle plan into the remaining
    /// compatibility geometry used by keep-local authored content and player interaction.
    /// Runtime and Composition consume this projection so the historical keep anchor and gate
    /// basis have exactly one owner during migration.
    /// </summary>
    public readonly struct CastleSpatialLayoutProjection
    {
        public const int LegacyKeepCentreZOffset = 60;

        public readonly CastlePlan KeepPlan;
        public readonly CastleGateGeometry PrimaryGate;

        private CastleSpatialLayoutProjection(
            in CastlePlan keepPlan,
            in CastleGateGeometry primaryGate)
        {
            KeepPlan = keepPlan;
            PrimaryGate = primaryGate;
        }

        public int3 TrapdoorCentre
        {
            get
            {
                CastlePlan keepPlan = KeepPlan;
                return CastleLayout.TrapdoorCentre(in keepPlan);
            }
        }

        public int3 ChapelBellTowerCentre
        {
            get
            {
                CastlePlan keepPlan = KeepPlan;
                return CastleLayout.ChapelBellTowerCentre(in keepPlan);
            }
        }

        public static CastleSpatialLayoutProjection Resolve(
            in CastlePlan plan,
            CastleSpatialPlan spatial)
        {
            if (spatial == null) throw new ArgumentNullException(nameof(spatial));
            if (spatial.KeepRequiresTerrainResolution)
            {
                throw new InvalidOperationException(
                    "Castle spatial projection requires a resolved keep placement.");
            }

            if (!CastleSpatialPlanValidator.TryValidate(
                    in plan, spatial, out CastleSpatialPlanIssue issue))
            {
                throw new InvalidOperationException(
                    $"Castle spatial projection requires a valid plan: {issue}.");
            }

            CastlePlan keepPlan = plan;
            keepPlan.Centre = new int3(
                plan.Centre.x + spatial.KeepCentre.x,
                plan.Centre.y,
                plan.Centre.z + spatial.KeepCentre.y - LegacyKeepCentreZOffset);

            CastleGatePlacementSpec primaryGate = spatial.PrimaryGate;
            CastleGateGeometry gateGeometry = CastleGateGeometryResolver.Resolve(
                in plan, in primaryGate);
            return new CastleSpatialLayoutProjection(in keepPlan, in gateGeometry);
        }
    }
}
