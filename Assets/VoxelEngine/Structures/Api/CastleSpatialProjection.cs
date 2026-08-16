using System;
using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Pure compatibility projection from semantic spatial placement into the historical keep-local
    /// coordinate system plus authoritative gate geometry. Runtime and presentation consume this
    /// same projection so the temporary +60 Z keep anchor cannot drift between systems.
    /// </summary>
    public readonly struct CastleSpatialProjection
    {
        public readonly CastlePlan KeepPlan;
        public readonly CastleGateGeometry PrimaryGateGeometry;
        public readonly CastleApproachFrame Approach;
        public readonly CastleKeepAnnexPlan KeepAnnexes;

        private CastleSpatialProjection(
            in CastlePlan keepPlan,
            in CastleGateGeometry primaryGateGeometry,
            in CastleApproachFrame approach,
            in CastleKeepAnnexPlan keepAnnexes)
        {
            KeepPlan = keepPlan;
            PrimaryGateGeometry = primaryGateGeometry;
            Approach = approach;
            KeepAnnexes = keepAnnexes;
        }

        /// <summary>
        /// Projects one fully resolved spatial plan into geometry shared by runtime realization and
        /// application-side interaction/presentation. No voxel or terrain work occurs here.
        /// </summary>
        public static CastleSpatialProjection Create(
            in CastlePlan plan,
            CastleSpatialPlan spatial)
        {
            if (spatial == null) throw new ArgumentNullException(nameof(spatial));
            if (spatial.KeepRequiresTerrainResolution)
            {
                throw new InvalidOperationException(
                    "Castle spatial projection requires a terrain-resolved keep placement.");
            }

            // Projection consumes only castle placement geometry. Validate a view without the
            // attached underground graph because CastleSpatialPlanValidator itself uses this
            // projection to verify Dungeon.Entrance == TrapdoorCentre. Validating the completed
            // graph here would recurse validator -> projection -> validator indefinitely.
            CastleSpatialPlan validationView = CreateProjectionValidationView(spatial);
            if (!CastleSpatialPlanValidator.TryValidate(
                    in plan, validationView, out CastleSpatialPlanIssue issue))
            {
                throw new InvalidOperationException(
                    $"Cannot project an invalid castle spatial plan: {issue}.");
            }

            CastlePlan keepPlan = plan;
            keepPlan.Centre = new int3(
                plan.Centre.x + spatial.KeepCentre.x,
                plan.Centre.y,
                plan.Centre.z + spatial.KeepCentre.y - CastleLayout.LegacyKeepCentreZOffset);

            CastleGatePlacementSpec primaryGate = spatial.PrimaryGate;
            CastleGateGeometry gateGeometry = CastleGateGeometryResolver.Resolve(
                in plan, in primaryGate);
            CastleApproachFrame approach = CastleApproachFrame.FromGate(in primaryGate);
            CastleKeepAnnexPlan keepAnnexes = spatial.Topology.KeepAnnexes;

            return new CastleSpatialProjection(
                in keepPlan,
                in gateGeometry,
                in approach,
                in keepAnnexes);
        }

        private static CastleSpatialPlan CreateProjectionValidationView(CastleSpatialPlan spatial)
        {
            CastleTopologyPlan topology = spatial.Topology;
            CastleGatePlacementSpec primaryGate = spatial.PrimaryGate;
            CastleGatePlacementSpec posternGate = spatial.PosternGate;
            CastleGatePlacementSpec innerGate = spatial.InnerGate;

            return new CastleSpatialPlan(
                in topology,
                spatial.OuterWardVertices,
                spatial.InnerWardVertices,
                spatial.Towers,
                in primaryGate,
                spatial.HasPosternGate,
                in posternGate,
                spatial.HasInnerGate,
                in innerGate,
                spatial.HasWell,
                spatial.WellCentre,
                spatial.CourtyardBuildings,
                spatial.KeepFloors,
                null,
                null,
                spatial.KeepCentre,
                spatial.KeepRequiresTerrainResolution);
        }

        /// <summary>Actual world-space X/Z centre of the projected keep.</summary>
        public int2 KeepCentreWorld =>
            new int2(
                KeepPlan.Centre.x,
                KeepPlan.Centre.z + CastleLayout.LegacyKeepCentreZOffset);

        /// <summary>World-space secret-hatch centre for the projected keep/dungeon recipe.</summary>
        public int3 TrapdoorCentre => CastleLayout.TrapdoorCentre(in KeepPlan);

        /// <summary>World-space chapel bell-tower centre for the projected keep annex recipe.</summary>
        public int3 ChapelBellTowerCentre => CastleLayout.ChapelBellTowerCentre(in KeepPlan);
    }
}
