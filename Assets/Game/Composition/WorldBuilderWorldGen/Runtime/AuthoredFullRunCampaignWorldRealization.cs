using System;
using System.Collections.Generic;
using Game.Cutscenes.Api;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using MountingForce.WorldGen;

namespace Game.Composition.WorldBuilderWorldGen.Runtime
{
    /// <summary>
    /// Production realization boundary for the authored full campaign. Semantic roles remain owned by
    /// WorldBuilder; this adapter consumes the already-resolved hierarchy-backed physical candidates and
    /// exposes concrete NPC and cutscene-stage placements to the Kentridge gameplay composition.
    /// </summary>
    public sealed class AuthoredFullRunCampaignWorldRealization
    {
        private readonly ResolvedNpcWorldPlacement[] _npcs;
        private readonly CutsceneStageRealization[] _cutsceneStages;

        public AuthoredFullRunCampaignGenerationPlan Generation { get; }
        public IReadOnlyList<ResolvedNpcWorldPlacement> Npcs => _npcs;
        public IReadOnlyList<CutsceneStageRealization> CutsceneStages => _cutsceneStages;

        internal AuthoredFullRunCampaignWorldRealization(
            AuthoredFullRunCampaignGenerationPlan generation,
            IReadOnlyList<ResolvedNpcWorldPlacement> npcs,
            IReadOnlyList<CutsceneStageRealization> cutsceneStages)
        {
            Generation = generation ?? throw new ArgumentNullException(nameof(generation));
            if (npcs == null) throw new ArgumentNullException(nameof(npcs));
            if (cutsceneStages == null) throw new ArgumentNullException(nameof(cutsceneStages));

            _npcs = new ResolvedNpcWorldPlacement[npcs.Count];
            for (var i = 0; i < npcs.Count; i++)
                _npcs[i] = npcs[i] ?? throw new InvalidOperationException(
                    "Authored full-run realization contains a null NPC placement at index " + i + ".");

            _cutsceneStages = new CutsceneStageRealization[cutsceneStages.Count];
            for (var i = 0; i < cutsceneStages.Count; i++)
                _cutsceneStages[i] = cutsceneStages[i] ?? throw new InvalidOperationException(
                    "Authored full-run realization contains a null cutscene stage at index " + i + ".");
        }
    }

    public static class AuthoredFullRunCampaignWorldRealizer
    {
        public static AuthoredFullRunCampaignWorldRealization Realize(
            AuthoredFullRunCampaignGenerationPlan generation)
        {
            if (generation == null) throw new ArgumentNullException(nameof(generation));
            if (!generation.Sites.IsResolved)
                throw new InvalidOperationException(
                    "Authored full-run world realization requires a successful semantic site resolution.");

            // Re-project the exact same deterministic facts used by AuthoredFullRunCampaignGenerator.
            // This intentionally consumes the recovered hierarchy + TopDownWorldPhysicalPlan rather than
            // manufacturing a second coordinate authority in gameplay composition.
            var facts = new AuthoredFullRunPhysicalSiteFacts(generation.World);
            IReadOnlyList<ResolvedNpcWorldPlacement> npcs = ResolveNpcs(
                generation.NpcAssignments,
                facts);
            IReadOnlyList<CutsceneStageRealization> stages = CutsceneStageRealizer.Realize(
                generation.World.Graph,
                new PhysicalCutsceneSiteGeometryProvider(generation.Sites, facts));

            return new AuthoredFullRunCampaignWorldRealization(generation, npcs, stages);
        }

        private static IReadOnlyList<ResolvedNpcWorldPlacement> ResolveNpcs(
            IReadOnlyList<NpcSiteAssignment> assignments,
            AuthoredFullRunPhysicalSiteFacts facts)
        {
            var result = new List<ResolvedNpcWorldPlacement>(assignments.Count);
            var seen = new HashSet<NpcRef>();
            for (var i = 0; i < assignments.Count; i++)
            {
                NpcSiteAssignment assignment = assignments[i]
                    ?? throw new InvalidOperationException(
                        "Authored full-run NPC assignments contain null at index " + i + ".");
                if (!seen.Add(assignment.Npc))
                    throw new InvalidOperationException(
                        "Authored full-run NPC '" + assignment.Npc + "' has more than one physical assignment.");
                if (!facts.TryGetCentre(assignment.Site, out Int2 centreDm))
                    throw new InvalidOperationException(
                        "Authored full-run NPC '" + assignment.Npc + "' targets site '" + assignment.Site +
                        "' without a hierarchy-backed physical anchor.");

                // TopDownWorldPhysicalPlan is decimetre-native in X/Z. Y remains terrain-owned; zero is
                // the production top-down ground datum used by the recovered macro-world plan, not an
                // authored campaign coordinate. Terrain/character presentation may ground visually at runtime.
                var position = new RealizedWorldPoint(
                    new Int3(centreDm.X, 0, centreDm.Y),
                    unitsPerDecimetre: 1);
                result.Add(new ResolvedNpcWorldPlacement(
                    assignment.Npc,
                    assignment.SiteRole,
                    assignment.Site,
                    assignment.RequiresConversation,
                    position));
            }
            return result;
        }

        private sealed class PhysicalCutsceneSiteGeometryProvider : ICutsceneSiteGeometryProvider
        {
            private readonly Dictionary<SiteRef, ResolvedSiteId> _sites;
            private readonly AuthoredFullRunPhysicalSiteFacts _facts;

            public PhysicalCutsceneSiteGeometryProvider(
                SiteResolutionResult resolution,
                AuthoredFullRunPhysicalSiteFacts facts)
            {
                if (resolution == null) throw new ArgumentNullException(nameof(resolution));
                if (!resolution.IsResolved)
                    throw new ArgumentException(
                        "Full-run cutscene geometry requires successful site resolution.",
                        nameof(resolution));
                _facts = facts ?? throw new ArgumentNullException(nameof(facts));
                _sites = new Dictionary<SiteRef, ResolvedSiteId>();
                for (var i = 0; i < resolution.Bindings.Count; i++)
                {
                    SiteRoleBinding binding = resolution.Bindings[i];
                    _sites[binding.Role] = binding.Site;
                }
            }

            public bool TryResolve(SiteRef site, out CutsceneSiteGeometry geometry)
            {
                if (!_sites.TryGetValue(site, out ResolvedSiteId resolved)
                    || !_facts.TryGetCentre(resolved, out Int2 centreDm)
                    || !_facts.TryGetCutsceneStageEnvelope(resolved, out CutsceneStageEnvelope envelope))
                {
                    geometry = default;
                    return false;
                }

                geometry = new CutsceneSiteGeometry(
                    new CutsceneInt3(centreDm.X, 0, centreDm.Y),
                    new CutsceneInt3(0, 0, 1),
                    new CutsceneInt3(1, 0, 0),
                    envelope.InteriorHalfWidthDecimetres,
                    envelope.InteriorDepthDecimetres);
                return true;
            }
        }
    }
}
