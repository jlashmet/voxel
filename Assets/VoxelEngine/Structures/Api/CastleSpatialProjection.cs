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
        /// Projects one semantic local keep centre into the temporary CastlePlan anchor expected by
        /// the legacy keep/dungeon authoring recipe. The offset itself remains owned by CastleLayout.
        /// </summary>
        public static CastlePlan ProjectKeepPlan(
            in CastlePlan plan,
            int2 localKeepCentre)
        {
            CastlePlan keepPlan = plan;
            keepPlan.Centre = new int3(
                plan.Centre.x + localKeepCentre.x,
                plan.Centre.y,
                plan.Centre.z + localKeepCentre.y - CastleLayout.LegacyKeepCentreZOffset);
            return keepPlan;
        }

        /// <summary>Returns the actual world-space X/Z keep centre represented by a projected plan.</summary>
        public static int2 ActualKeepCentre(in CastlePlan projectedKeepPlan) =>
            new int2(
                projectedKeepPlan.Centre.x,
                projectedKeepPlan.Centre.z + CastleLayout.LegacyKeepCentreZOffset);

        /// <summary>
        /// Returns the world-space minimum corner of the authored keep volume represented by a
        /// projected plan. Runtime components should consume this instead of reconstructing the
        /// temporary legacy keep anchor themselves.
        /// </summary>
        public static int3 KeepMinimum(in CastlePlan projectedKeepPlan)
        {
            int2 centre = ActualKeepCentre(in projectedKeepPlan);
            return new int3(
                centre.x - projectedKeepPlan.KeepHalfX,
                projectedKeepPlan.Centre.y + projectedKeepPlan.PlateauHeight,
                centre.y - projectedKeepPlan.KeepHalfZ);
        }

        /// <summary>
        /// Returns the authored keep volume size represented by a projected plan. Runtime components
        /// should consume this instead of rebuilding the same dimensions from keep half-extents.
        /// </summary>
        public static int3 KeepSize(in CastlePlan projectedKeepPlan) =>
            new int3(
                projectedKeepPlan.KeepHalfX * 2,
                projectedKeepPlan.KeepHeight,
                projectedKeepPlan.KeepHalfZ * 2);

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

            CastlePlan keepPlan = ProjectKeepPlan(in plan, spatial.KeepCentre);
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
                spatial.KeepRequiresTerrainResolution,
                spatial.InnerTowers);
        }

        /// <summary>Actual world-space X/Z centre of the projected keep.</summary>
        public int2 KeepCentreWorld => ActualKeepCentre(in KeepPlan);

        /// <summary>World-space minimum corner of the projected keep volume.</summary>
        public int3 KeepMinimumWorld => KeepMinimum(in KeepPlan);

        /// <summary>World-space size of the projected keep volume.</summary>
        public int3 KeepSizeWorld => KeepSize(in KeepPlan);

        /// <summary>World-space secret-hatch centre for the projected keep/dungeon recipe.</summary>
        public int3 TrapdoorCentre => CastleLayout.TrapdoorCentre(in KeepPlan);

        /// <summary>World-space chapel bell-tower centre for the projected keep annex recipe.</summary>
        public int3 ChapelBellTowerCentre => CastleLayout.ChapelBellTowerCentre(in KeepPlan);
    }
}
