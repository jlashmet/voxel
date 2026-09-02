using System;
using System.Collections.Generic;
using System.Text;
using Game.Composition.WorldBuilderWorldGen;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Architecture;

namespace Game.Composition.WorldBuilderWorldGen.Runtime
{
    /// <summary>
    /// Pre-voxel campaign realization. This is the complete semantic/architecture result needed by a
    /// backend before it emits world geometry: site roles are selected, NPCs are assigned to concrete
    /// sites, hidden spaces are physically planned, and every gameplay secret owns a distinct candidate.
    /// The public game-facing plan retains only the opaque WorldBuilder town; the backend settlement
    /// remains internal to this integration assembly.
    /// </summary>
    public sealed class KentridgeCampaignGenerationPlan
    {
        public CampaignBlueprint Blueprint { get; }
        public PlanningGraph Graph { get; }
        public AuthoredTownPlan Town { get; }
        internal SettlementPlan Settlement { get; }
        public SiteResolutionResult Sites { get; }
        public IReadOnlyList<NpcSiteAssignment> NpcAssignments { get; }
        public IReadOnlyList<KentridgeHiddenSpaceGeometry> HiddenSpaces { get; }
        public IReadOnlyList<ResolvedSecretPlan> Secrets { get; }

        internal KentridgeCampaignGenerationPlan(
            CampaignBlueprint blueprint,
            PlanningGraph graph,
            AuthoredTownPlan town,
            SettlementPlan settlement,
            SiteResolutionResult sites,
            IReadOnlyList<NpcSiteAssignment> npcAssignments,
            IReadOnlyList<KentridgeHiddenSpaceGeometry> hiddenSpaces,
            IReadOnlyList<ResolvedSecretPlan> secrets)
        {
            Blueprint = blueprint ?? throw new ArgumentNullException(nameof(blueprint));
            Graph = graph ?? throw new ArgumentNullException(nameof(graph));
            Town = town ?? throw new ArgumentNullException(nameof(town));
            Settlement = settlement ?? throw new ArgumentNullException(nameof(settlement));
            Sites = sites ?? throw new ArgumentNullException(nameof(sites));
            NpcAssignments = npcAssignments ?? throw new ArgumentNullException(nameof(npcAssignments));
            HiddenSpaces = hiddenSpaces ?? throw new ArgumentNullException(nameof(hiddenSpaces));
            Secrets = secrets ?? throw new ArgumentNullException(nameof(secrets));
        }
    }

    /// <summary>
    /// Post-generation gameplay-facing realization. All positions come from the backend's exact site
    /// and hidden-space realization facts; no coordinate is reconstructed from an archetype name.
    /// </summary>
    public sealed class KentridgeCampaignWorldRealization
    {
        public KentridgeCampaignGenerationPlan Generation { get; }
        public IReadOnlyList<ResolvedNpcWorldPlacement> Npcs { get; }
        public IReadOnlyList<CutsceneStageRealization> CutsceneStages { get; }
        public IReadOnlyList<ResolvedSecretWorldGeometry> Secrets { get; }

        internal KentridgeCampaignWorldRealization(
            KentridgeCampaignGenerationPlan generation,
            IReadOnlyList<ResolvedNpcWorldPlacement> npcs,
            IReadOnlyList<CutsceneStageRealization> cutsceneStages,
            IReadOnlyList<ResolvedSecretWorldGeometry> secrets)
        {
            Generation = generation ?? throw new ArgumentNullException(nameof(generation));
            Npcs = npcs ?? throw new ArgumentNullException(nameof(npcs));
            CutsceneStages = cutsceneStages ?? throw new ArgumentNullException(nameof(cutsceneStages));
            Secrets = secrets ?? throw new ArgumentNullException(nameof(secrets));
        }
    }

    /// <summary>
    /// Composition-owned pre-generation pipeline for one WorldBuilder-authored Kentridge settlement.
    /// WorldBuilder owns authoring/compilation; the legacy settlement and architecture implementation
    /// remain private realization details inside this integration assembly.
    /// </summary>
    public static class KentridgeCampaignWorldPlanner
    {
        public static KentridgeCampaignGenerationPlan Plan(
            CampaignBlueprint blueprint,
            AuthoredTownPlan town)
        {
            if (blueprint == null) throw new ArgumentNullException(nameof(blueprint));
            if (town == null) throw new ArgumentNullException(nameof(town));
            if (!string.Equals(town.SettlementId, WorldBuilderTownIds.Kentridge, StringComparison.Ordinal))
                throw new ArgumentOutOfRangeException(
                    nameof(town),
                    town.SettlementId,
                    "Kentridge campaign planning requires the WorldBuilder Kentridge town plan.");
            if (!(town.BackendPlan is SettlementPlan settlement))
                throw new InvalidOperationException(
                    "The authored Kentridge town does not carry the expected settlement realization.");
            if (settlement.Seed != town.Seed)
                throw new InvalidOperationException(
                    "The authored town seed and backend settlement seed do not match.");

            PlanningGraph graph = BlueprintCompiler.Compile(blueprint);
            WorldSettlementPlan semanticSettlement = ResolveSemanticSettlement(graph.HierarchyPlan);
            ValidateSupportedHierarchy(graph.HierarchyPlan, semanticSettlement);

            var projections = new KentridgeArchitectureSiteProjectionProvider(settlement);
            var traversal = new SettlementStreetTraversalFacts(settlement, projections);
            var facts = new SettlementPlanWorldBuilderFacts(
                settlement,
                semanticSettlement.Region,
                semanticSettlement.Settlement,
                projections,
                traversal,
                projections);

            SiteResolutionResult sites = SiteRoleResolver.Resolve(graph, facts);
            if (!sites.IsResolved)
                throw new InvalidOperationException(FormatSiteResolutionFailure(sites));

            IReadOnlyList<NpcSiteAssignment> npcAssignments =
                NpcPlacementResolver.ResolveSites(graph, sites);
            IReadOnlyList<KentridgeHiddenSpaceGeometry> hiddenSpaces =
                KentridgeHiddenSpaceRequestComposer.ResolveArchitecture(
                    graph,
                    sites,
                    settlement);
            var secretCandidates = new KentridgeHiddenSpaceSecretCandidateProvider(
                settlement,
                sites,
                hiddenSpaces);
            IReadOnlyList<ResolvedSecretPlan> secrets = SecretPlanner.ResolveCampaign(
                blueprint,
                graph,
                sites,
                secretCandidates,
                settlement.Seed);

            return new KentridgeCampaignGenerationPlan(
                blueprint,
                graph,
                town,
                settlement,
                sites,
                npcAssignments,
                hiddenSpaces,
                secrets);
        }

        private static WorldSettlementPlan ResolveSemanticSettlement(WorldHierarchyPlan hierarchy)
        {
            if (hierarchy == null)
                throw new ArgumentNullException(nameof(hierarchy));
            if (hierarchy.Settlements.Count != 1)
                throw new InvalidOperationException(
                    "Kentridge single-settlement planning requires exactly one authored settlement " +
                    "in the compiled campaign hierarchy, but found " + hierarchy.Settlements.Count + ".");
            return hierarchy.Settlements[0];
        }

        private static void ValidateSupportedHierarchy(
            WorldHierarchyPlan hierarchy,
            WorldSettlementPlan settlement)
        {
            if (hierarchy.Regions.Count != 1)
                throw new InvalidOperationException(
                    "Kentridge single-region planning requires exactly one authored region, but found " +
                    hierarchy.Regions.Count + ". A multi-region generator must consume WorldHierarchyPlan directly.");

            WorldRegionPlan region = null;
            for (var i = 0; i < hierarchy.Regions.Count; i++)
            {
                if (hierarchy.Regions[i].Region.Equals(settlement.Region))
                {
                    region = hierarchy.Regions[i];
                    break;
                }
            }

            if (region == null)
                throw new InvalidOperationException(
                    "Kentridge authored settlement '" + settlement.Settlement +
                    "' has no compiled owning region.");

            if (region.Biome != BiomeFamily.Unspecified)
                throw new InvalidOperationException(
                    "Kentridge WorldGen does not yet expose biome realization facts, so authored biome '" +
                    region.Biome + "' cannot be proven satisfied. Leave the biome unspecified or use a hierarchy-aware world generator.");

            if (hierarchy.Routes.Count > 0)
                throw new InvalidOperationException(
                    "Kentridge WorldGen currently plans settlement streets but not outer WorldBuilder routes. " +
                    "The campaign requires " + hierarchy.Routes.Count +
                    " route(s); use a hierarchy-aware world generator instead of silently ignoring them.");

            if (settlement.Archetype != SettlementArchetype.Unspecified
                && settlement.Archetype != SettlementArchetype.Town)
                throw new InvalidOperationException(
                    "Kentridge WorldGen realizes a town, but campaign settlement '" + settlement.Settlement +
                    "' requires archetype '" + settlement.Archetype + "'.");

            if (settlement.HasPopulationRange)
                throw new InvalidOperationException(
                    "Kentridge WorldGen does not yet expose population realization facts, so campaign settlement '" +
                    settlement.Settlement + "' population requirement " + settlement.Population.Minimum + ".." +
                    settlement.Population.Maximum + " cannot be proven satisfied.");

            if (settlement.RouteAccess.Count > 0)
                throw new InvalidOperationException(
                    "Kentridge WorldGen does not yet realize WorldBuilder settlement-to-route connectors; " +
                    "campaign settlement '" + settlement.Settlement + "' requires " +
                    settlement.RouteAccess.Count + " connector(s).");
        }

        private static string FormatSiteResolutionFailure(SiteResolutionResult sites)
        {
            var text = new StringBuilder(
                "Kentridge campaign site roles cannot be realized by the generated settlement.");
            for (var i = 0; i < sites.Diagnostics.Count; i++)
                text.Append("\n").Append(sites.Diagnostics[i]);
            return text.ToString();
        }
    }

    /// <summary>
    /// Second phase run after the backend has fixed terrain-relative world placement. It converts the
    /// generation plan into concrete gameplay positions/stages while preserving exact backend scale.
    /// </summary>
    public static class KentridgeCampaignWorldRealizer
    {
        public static KentridgeCampaignWorldRealization Realize(
            KentridgeCampaignGenerationPlan generation,
            ISettlementSiteRealizationFacts siteFacts,
            IHiddenSpaceRealizationFacts hiddenSpaceFacts = null)
        {
            if (generation == null) throw new ArgumentNullException(nameof(generation));
            if (siteFacts == null) throw new ArgumentNullException(nameof(siteFacts));
            if (generation.Secrets.Count > 0 && hiddenSpaceFacts == null)
                throw new ArgumentNullException(
                    nameof(hiddenSpaceFacts),
                    "Physical hidden-space facts are required when the campaign selected secrets.");

            IReadOnlyList<ResolvedNpcWorldPlacement> npcs =
                KentridgeNpcWorldPlacementResolver.Resolve(
                    generation.NpcAssignments,
                    generation.Settlement,
                    siteFacts);

            var projections = new KentridgeArchitectureSiteProjectionProvider(
                generation.Settlement);
            var cutsceneGeometry = new SettlementCutsceneSiteGeometryProvider(
                generation.Settlement,
                generation.Sites,
                projections,
                siteFacts);
            IReadOnlyList<CutsceneStageRealization> stages =
                CutsceneStageRealizer.Realize(generation.Graph, cutsceneGeometry);

            var secrets = new List<ResolvedSecretWorldGeometry>(generation.Secrets.Count);
            for (var i = 0; i < generation.Secrets.Count; i++)
                secrets.Add(SecretWorldGeometryResolver.Resolve(
                    generation.Secrets[i],
                    hiddenSpaceFacts));

            return new KentridgeCampaignWorldRealization(
                generation,
                npcs,
                stages,
                secrets.ToArray());
        }
    }
}
