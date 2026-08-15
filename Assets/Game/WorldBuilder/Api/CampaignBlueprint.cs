using System;
using System.Collections.Generic;

namespace Game.WorldBuilder.Api
{
    public sealed class CampaignBlueprint
    {
        public string Id { get; }
        public WorldHierarchyBlueprint Hierarchy { get; }
        public IReadOnlyList<SiteSpec> Sites { get; }
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
            Sites = DeriveSiteCapabilities(
                sites ?? Array.Empty<SiteSpec>(),
                Cutscenes,
                Npcs);
            SpatialConstraints = spatialConstraints ?? Array.Empty<SpatialConstraintSpec>();
            StoryRules = storyRules ?? Array.Empty<StoryRuleSpec>();
            Objectives = objectives ?? Array.Empty<ObjectiveSpec>();
            SecretPolicies = secretPolicies ?? Array.Empty<SecretPolicySpec>();
            RequiredSecrets = requiredSecrets ?? Array.Empty<RequiredSecretSpec>();
            LootTables = lootTables ?? Array.Empty<LootTableSpec>();
        }

        /// <summary>
        /// Normalizes capabilities implied directly by authored content. The detailed generation
        /// plans remain authoritative; these capabilities let generic site selection/generation see
        /// the same requirements without forcing authors to restate them manually.
        /// </summary>
        private static SiteSpec[] DeriveSiteCapabilities(
            SiteSpec[] sites,
            IReadOnlyList<CutsceneSpec> cutscenes,
            IReadOnlyList<NpcSpec> npcs)
        {
            var result = new SiteSpec[sites.Length];
            for (var i = 0; i < sites.Length; i++)
            {
                SiteSpec site = sites[i];
                var capabilities = new List<SiteCapabilityRequirement>(site.Capabilities.Count + 2);
                for (var j = 0; j < site.Capabilities.Count; j++)
                    capabilities.Add(site.Capabilities[j]);

                if (RequiresCutsceneStage(site.Ref, cutscenes))
                    AddCapabilityIfMissing(capabilities, SiteCapability.CutsceneStage);

                if (RequiresConversationSpace(site.Ref, npcs))
                    AddCapabilityIfMissing(capabilities, SiteCapability.ConversationSpace);

                if (capabilities.Count == site.Capabilities.Count)
                {
                    result[i] = site;
                    continue;
                }

                result[i] = new SiteSpec(site.Ref, site.Archetype, capabilities.ToArray());
            }
            return result;
        }

        private static bool RequiresCutsceneStage(
            SiteRef site,
            IReadOnlyList<CutsceneSpec> cutscenes)
        {
            for (var i = 0; i < cutscenes.Count; i++)
            {
                CutsceneSpec cutscene = cutscenes[i];
                if (cutscene.Site.Equals(site)
                    && cutscene.Definition.StageRequirements.Count > 0)
                    return true;
            }
            return false;
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

        private static void AddCapabilityIfMissing(
            List<SiteCapabilityRequirement> capabilities,
            SiteCapabilityRequirement capability)
        {
            for (var i = 0; i < capabilities.Count; i++)
                if (capabilities[i].Kind == capability.Kind)
                    return;
            capabilities.Add(capability);
        }
    }

    public static class Campaign
    {
        public static CampaignBuilder Create(string id) => new CampaignBuilder(id);
    }
}
