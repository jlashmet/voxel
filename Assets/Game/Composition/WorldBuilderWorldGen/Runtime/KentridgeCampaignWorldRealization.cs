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
    /// HiddenSpaces is intentionally exposed so the Voxel backend can include those rooms in its
    /// catalogue without taking any dependency on WorldBuilder.Runtime.
    /// </summary>
    public sealed class KentridgeCampaignGenerationPlan
    {
        public CampaignBlueprint Blueprint { get; }
        public PlanningGraph Graph { get; }
        public SettlementPlan Settlement { get; }
        public SiteResolutionResult Sites { get; }
        public IReadOnlyList<NpcSiteAssignment> NpcAssignments { get; }
        public IReadOnlyList<KentridgeHiddenSpaceGeometry> HiddenSpaces { get; }
        public IReadOnlyList<ResolvedSecretPlan> Secrets { get; }

        internal KentridgeCampaignGenerationPlan(
            CampaignBlueprint blueprint,
            PlanningGraph graph,
            SettlementPlan settlement,
            SiteResolutionResult sites,
            IReadOnlyList<NpcSiteAssignment> npcAssignments,
            IReadOnlyList<KentridgeHiddenSpaceGeometry> hiddenSpaces,
            IReadOnlyList<ResolvedSecretPlan> secrets)
        {
            Blueprint = blueprint ?? throw new ArgumentNullException(nameof(blueprint));
            Graph = graph ?? throw new ArgumentNullException(nameof(graph));
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
    /// Composition-owned pre-generation pipeline for a Kentridge settlement. WorldBuilder owns
    /// compilation/constraint solving; WorldGen owns the settlement, traversal, architecture, and hidden
    /// geometry; Composition is the only layer allowed to invoke both runtimes and join their results.
    /// </summary>
    public static class KentridgeCampaignWorldPlanner
    {
        public static KentridgeCampaignGenerationPlan Plan(
            CampaignBlueprint blueprint,
            SettlementPlan settlement,
            RegionRef region,
            SettlementRef settlementRef)
        {
            if (blueprint == null) throw new ArgumentNullException(nameof(blueprint));
            if (settlement == null) throw new ArgumentNullException(nameof(settlement));

            PlanningGraph graph = BlueprintCompiler.Compile(blueprint);
            var projections = new KentridgeArchitectureSiteProjectionProvider(settlement);
            var traversal = new SettlementStreetTraversalFacts(settlement, projections);
            var facts = new SettlementPlanWorldBuilderFacts(
                settlement,
                region,
                settlementRef,
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
                secretCandidates,
                settlement.Seed);

            return new KentridgeCampaignGenerationPlan(
                blueprint,
                graph,
                settlement,
                sites,
                npcAssignments,
                hiddenSpaces,
                secrets);
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
