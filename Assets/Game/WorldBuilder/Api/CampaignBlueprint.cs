using System;
using System.Collections.Generic;
using Game.Cutscenes.Api;

namespace Game.WorldBuilder.Api
{
    public sealed class CampaignBlueprint
    {
        public string Id { get; }
        public WorldHierarchyBlueprint Hierarchy { get; }
        public IReadOnlyList<SiteSpec> Sites { get; }
        public IReadOnlyList<SiteSourceEvidenceSpec> SiteSourceEvidence { get; }
        public IReadOnlyList<NpcSpec> Npcs { get; }
        public IReadOnlyList<SpatialConstraintSpec> SpatialConstraints { get; }
        public IReadOnlyList<CutsceneSpec> Cutscenes { get; }
        public IReadOnlyList<StoryRuleSpec> StoryRules { get; }
        public IReadOnlyList<ObjectiveSpec> Objectives { get; }
        public IReadOnlyList<SecretPolicySpec> SecretPolicies { get; }
        public IReadOnlyList<RequiredSecretSpec> RequiredSecrets { get; }
        public IReadOnlyList<LootTableSpec> LootTables { get; }

        internal CampaignBlueprint(
            string id,
            WorldHierarchyBlueprint hierarchy,
            SiteSpec[] sites,
            SiteSourceEvidenceSpec[] siteSourceEvidence,
            NpcSpec[] npcs,
            SpatialConstraintSpec[] spatialConstraints,
            CutsceneSpec[] cutscenes,
            StoryRuleSpec[] storyRules,
            ObjectiveSpec[] objectives,
            SecretPolicySpec[] secretPolicies,
            RequiredSecretSpec[] requiredSecrets,
            LootTableSpec[] lootTables)
        {
            Id = WorldIdRules.Require(id, nameof(id));
            Hierarchy = hierarchy ?? throw new ArgumentNullException(nameof(hierarchy));
            Cutscenes = cutscenes ?? Array.Empty<CutsceneSpec>();
            Npcs = npcs ?? Array.Empty<NpcSpec>();
            RequiredSecrets = requiredSecrets ?? Array.Empty<RequiredSecretSpec>();
            Sites = DeriveSiteCapabilities(
                sites ?? Array.Empty<SiteSpec>(),
                Cutscenes,
                Npcs,
                RequiredSecrets);
            SiteSourceEvidence = siteSourceEvidence ?? Array.Empty<SiteSourceEvidenceSpec>();
            SpatialConstraints = spatialConstraints ?? Array.Empty<SpatialConstraintSpec>();
            StoryRules = storyRules ?? Array.Empty<StoryRuleSpec>();
            Objectives = objectives ?? Array.Empty<ObjectiveSpec>();
            SecretPolicies = secretPolicies ?? Array.Empty<SecretPolicySpec>();
            LootTables = lootTables ?? Array.Empty<LootTableSpec>();
        }

        /// <summary>
        /// Normalizes capabilities implied directly by authored content. Derived capabilities carry
        /// provenance so systems can distinguish a hard requirement from an explicit policy opt-in.
        /// Detailed generation plans remain authoritative.
        /// </summary>
        private static SiteSpec[] DeriveSiteCapabilities(
            SiteSpec[] sites,
            IReadOnlyList<CutsceneSpec> cutscenes,
            IReadOnlyList<NpcSpec> npcs,
            IReadOnlyList<RequiredSecretSpec> requiredSecrets)
        {
            var result = new SiteSpec[sites.Length];
            for (var i = 0; i < sites.Length; i++)
            {
                SiteSpec site = sites[i];
                var capabilities = new List<SiteCapabilityRequirement>(site.Capabilities.Count + 6);
                for (var j = 0; j < site.Capabilities.Count; j++)
                    capabilities.Add(site.Capabilities[j]);

                DeriveCutsceneCapabilities(site.Ref, cutscenes, capabilities);

                if (RequiresConversationSpace(site.Ref, npcs))
                    AddDerivedCapabilityIfMissing(capabilities, SiteCapability.ConversationSpace);

                if (RequiresSecretCandidateHost(site.Ref, requiredSecrets))
                    AddDerivedCapabilityIfMissing(capabilities, SiteCapability.SecretCandidateHost);

                if (capabilities.Count == site.Capabilities.Count)
                {
                    result[i] = site;
                    continue;
                }

                result[i] = new SiteSpec(site.Ref, site.Archetype, capabilities.ToArray());
            }
            return result;
        }

        private static void DeriveCutsceneCapabilities(
            SiteRef site,
            IReadOnlyList<CutsceneSpec> cutscenes,
            List<SiteCapabilityRequirement> capabilities)
        {
            bool requiresStage = false;
            bool requiresInterior = false;
            bool requiresPublicExit = false;
            bool requiresPlayerSpawn = false;

            for (var i = 0; i < cutscenes.Count; i++)
            {
                CutsceneSpec cutscene = cutscenes[i];
                if (!cutscene.Site.Equals(site)) continue;

                for (var j = 0; j < cutscene.Definition.StageRequirements.Count; j++)
                {
                    CutsceneStagePointRequirement requirement = cutscene.Definition.StageRequirements[j];
                    requiresStage = true;

                    switch (requirement.Region)
                    {
                        case CutsceneStageRegion.SiteInterior:
                        case CutsceneStageRegion.InteriorGatheringArea:
                            requiresInterior = true;
                            break;
                        case CutsceneStageRegion.PublicEntrance:
                        case CutsceneStageRegion.EntranceApproach:
                            requiresPublicExit = true;
                            break;
                        case CutsceneStageRegion.PlayerSpawnArea:
                            requiresPlayerSpawn = true;
                            break;
                    }
                }
            }

            if (requiresStage)
                AddDerivedCapabilityIfMissing(capabilities, SiteCapability.CutsceneStage);
            if (requiresInterior)
                AddDerivedCapabilityIfMissing(capabilities, SiteCapability.Interior);
            if (requiresPublicExit)
                AddDerivedCapabilityIfMissing(capabilities, SiteCapability.PublicExit);
            if (requiresPlayerSpawn)
                AddDerivedCapabilityIfMissing(capabilities, SiteCapability.PlayerSpawn(1));
        }

        private static bool RequiresConversationSpace(
            SiteRef site,
            IReadOnlyList<NpcSpec> npcs)
        {
            for (var i = 0; i < npcs.Count; i++)
                if (npcs[i].Site.Equals(site) && npcs[i].RequiresConversation)
                    return true;
            return false;
        }

        private static bool RequiresSecretCandidateHost(
            SiteRef site,
            IReadOnlyList<RequiredSecretSpec> requiredSecrets)
        {
            for (var i = 0; i < requiredSecrets.Count; i++)
                if (requiredSecrets[i].Site.Equals(site))
                    return true;
            return false;
        }

        private static void AddDerivedCapabilityIfMissing(
            List<SiteCapabilityRequirement> capabilities,
            SiteCapabilityRequirement capability)
        {
            for (var i = 0; i < capabilities.Count; i++)
                if (capabilities[i].Kind == capability.Kind)
                    return;
            capabilities.Add(capability.AsDerived());
        }
    }

    public static class Campaign
    {
        public static CampaignBuilder Create(string id) => new CampaignBuilder(id);
    }
}
