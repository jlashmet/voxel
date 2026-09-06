using System;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Voxel;

namespace Game.Composition.WorldBuilderWorldGen.Runtime
{
    /// <summary>
    /// Production composition handoff for the authored multi-region campaign. The semantic hierarchy
    /// remains WorldBuilder-owned while the recovered macro planner owns physical roads, regions and
    /// settlement envelopes. This is deliberately separate from KentridgeCampaignWorldPlanner, whose
    /// one-region/one-settlement guard remains the correct contract for the opening-only backend.
    /// </summary>
    public sealed class AuthoredFullRunPhysicalWorldPlan
    {
        public CampaignBlueprint Blueprint { get; }
        public PlanningGraph Graph { get; }
        public TopDownWorldLayout Layout { get; }
        public TopDownWorldPhysicalPlan Physical { get; }

        internal AuthoredFullRunPhysicalWorldPlan(
            CampaignBlueprint blueprint,
            PlanningGraph graph,
            TopDownWorldLayout layout,
            TopDownWorldPhysicalPlan physical)
        {
            Blueprint = blueprint ?? throw new ArgumentNullException(nameof(blueprint));
            Graph = graph ?? throw new ArgumentNullException(nameof(graph));
            Layout = layout ?? throw new ArgumentNullException(nameof(layout));
            Physical = physical ?? throw new ArgumentNullException(nameof(physical));
        }

        public bool TryGetPhysicalSettlement(
            SettlementRef semanticSettlement,
            out TopDownWorldSettlementPlan physicalSettlement) =>
            Physical.TryGetSettlement(semanticSettlement.Id, out physicalSettlement);
    }

    /// <summary>
    /// Compiles the real authored hierarchy and consumes the recovered source-backed Mounting Force
    /// macro world plus its physical intent. No continuation coordinate is invented here: settlement
    /// positions, route tiles and geography all come from the authoritative layout/physical planners.
    /// Later site/NPC/stage realization consumes this plan rather than weakening the opening planner.
    /// </summary>
    public static class AuthoredFullRunPhysicalWorldPlanner
    {
        public static AuthoredFullRunPhysicalWorldPlan Plan(
            CampaignBlueprint blueprint,
            uint seed,
            Int2 kentridgeCentreDm,
            int voxelsPerDecimetre)
        {
            if (blueprint == null) throw new ArgumentNullException(nameof(blueprint));
            if (voxelsPerDecimetre < 1) throw new ArgumentOutOfRangeException(nameof(voxelsPerDecimetre));

            PlanningGraph graph = BlueprintCompiler.Compile(blueprint);
            if (graph.HierarchyPlan.Settlements.Count < 2)
                throw new InvalidOperationException(
                    "Authored full-run physical planning requires a multi-settlement compiled hierarchy. " +
                    "Use KentridgeCampaignWorldPlanner for the opening-only single-settlement composition.");

            TopDownWorldLayout layout = MountingForceTopDownWorldDefinition.Build(seed);
            TopDownWorldPhysicalPlan physical = TopDownWorldPhysicalPlanner.Plan(
                layout,
                KentridgeTopDownWorldPhysicalIntent.Build(),
                kentridgeCentreDm,
                MountingForceTopDownWorldDefinition.CellSizeDm,
                voxelsPerDecimetre);

            ValidateSettlementCoverage(graph.HierarchyPlan, physical);
            return new AuthoredFullRunPhysicalWorldPlan(blueprint, graph, layout, physical);
        }

        private static void ValidateSettlementCoverage(
            WorldHierarchyPlan hierarchy,
            TopDownWorldPhysicalPlan physical)
        {
            for (var i = 0; i < hierarchy.Settlements.Count; i++)
            {
                WorldSettlementPlan semantic = hierarchy.Settlements[i];
                if (physical.TryGetSettlement(semantic.Settlement.Id, out _)) continue;
                throw new InvalidOperationException(
                    "The authored full-run settlement '" + semantic.Settlement +
                    "' has no source-backed physical settlement in the recovered macro world. " +
                    "Do not substitute a fake region or silently drop the semantic settlement.");
            }

            for (var i = 0; i < hierarchy.SitePlacements.Count; i++)
            {
                WorldSitePlacementPlan placement = hierarchy.SitePlacements[i];
                if (placement.Kind != SitePlacementKind.Settlement) continue;
                if (physical.TryGetSettlement(placement.Settlement.Id, out _)) continue;
                throw new InvalidOperationException(
                    "Authored site '" + placement.Site + "' belongs to settlement '" +
                    placement.Settlement + "', but that settlement has no physical realization.");
            }
        }
    }
}
