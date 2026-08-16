using System;
using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Pure compatibility projection for authored castle geometry that still consumes the
    /// historical CastlePlan keep anchor. Spatial planning remains authoritative; this value only
    /// translates that finished plan into shared coordinates for realization and interaction.
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

    public static class CastleSpatialLayoutProjector
    {
        /// <summary>
        /// Historical keep recipes treat CastlePlan.Centre.z + 60 as the actual keep centre.
        /// Keeping that legacy detail here prevents Runtime, presentation, and interaction code
        /// from each carrying their own copy of the migration offset.
        /// </summary>
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

            CastleGatePlacementSpec primaryPlacement = spatial.PrimaryGate;
            CastleGateGeometry primaryGate = CastleGateGeometryResolver.Resolve(
                in plan, in primaryPlacement);
            int3 trapdoor = CastleLayout.TrapdoorCentre(in keepPlan);
            int3 bellTower = CastleLayout.ChapelBellTowerCentre(in keepPlan);

            return new CastleSpatialLayoutProjection(
                in keepPlan, in primaryGate, trapdoor, bellTower);
        }
    }
}
