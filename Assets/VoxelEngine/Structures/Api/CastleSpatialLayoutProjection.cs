using System;
using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Pure projection from a validated spatial castle plan into the world-space compatibility
    /// geometry still consumed by the extracted keep/dungeon recipe and showcase interaction.
    /// Planning remains authoritative; this type only translates coordinates.
    /// </summary>
    public readonly struct CastleSpatialLayoutProjection
    {
        private const int LegacyKeepCentreZOffset = 60;

        public CastlePlan KeepPlan { get; }
        public CastleGateGeometry PrimaryGate { get; }
        public int2 KeepCentreWorld { get; }
        public int3 TrapdoorCentre { get; }
        public int3 ChapelBellTowerCentre { get; }

        private CastleSpatialLayoutProjection(
            in CastlePlan keepPlan,
            in CastleGateGeometry primaryGate,
            int2 keepCentreWorld,
            int3 trapdoorCentre,
            int3 chapelBellTowerCentre)
        {
            KeepPlan = keepPlan;
            PrimaryGate = primaryGate;
            KeepCentreWorld = keepCentreWorld;
            TrapdoorCentre = trapdoorCentre;
            ChapelBellTowerCentre = chapelBellTowerCentre;
        }

        /// <summary>
        /// Canonical projection entry point used by Runtime and presentation. Resolve remains as a
        /// compatibility alias so callers compiled against the earlier planning API do not need to
        /// duplicate or rediscover the keep/gate coordinate translation.
        /// </summary>
        public static CastleSpatialLayoutProjection Create(
            in CastlePlan plan,
            CastleSpatialPlan spatial) =>
            Resolve(in plan, spatial);

        public static CastleSpatialLayoutProjection Resolve(
            in CastlePlan plan,
            CastleSpatialPlan spatial)
        {
            if (spatial == null) throw new ArgumentNullException(nameof(spatial));
            if (!CastleSpatialPlanValidator.TryValidate(
                    in plan, spatial, out CastleSpatialPlanIssue issue))
            {
                throw new InvalidOperationException(
                    $"Castle spatial plan is structurally invalid: {issue}.");
            }

            if (spatial.KeepRequiresTerrainResolution)
            {
                throw new InvalidOperationException(
                    "Castle spatial layout projection requires a resolved keep placement.");
            }

            int2 keepCentreWorld = new int2(
                plan.Centre.x + spatial.KeepCentre.x,
                plan.Centre.z + spatial.KeepCentre.y);

            // The current extracted keep recipe still authors itself around a historical +60 Z
            // offset from CastlePlan.Centre. Keep that migration detail here so Runtime, showcase
            // interaction, and presentation never duplicate or disagree about the translation.
            CastlePlan keepPlan = plan;
            keepPlan.Centre = new int3(
                keepCentreWorld.x,
                plan.Centre.y,
                keepCentreWorld.y - LegacyKeepCentreZOffset);

            CastleGatePlacementSpec primaryGatePlacement = spatial.PrimaryGate;
            CastleGateGeometry primaryGate = CastleGateGeometryResolver.Resolve(
                in plan, in primaryGatePlacement);
            int3 trapdoorCentre = CastleLayout.TrapdoorCentre(in keepPlan);
            int3 bellTowerCentre = CastleLayout.ChapelBellTowerCentre(in keepPlan);

            return new CastleSpatialLayoutProjection(
                in keepPlan,
                in primaryGate,
                keepCentreWorld,
                trapdoorCentre,
                bellTowerCentre);
        }
    }
}
