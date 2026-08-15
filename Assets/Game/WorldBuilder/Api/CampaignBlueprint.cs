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
            Sites = DeriveSiteCapabilities(sites ?? Array.Empty<SiteSpec>(), Cutscenes);
            Npcs = npcs ?? Array.Empty<NpcSpec>();
            SpatialConstraints = spatialConstraints ?? Array.Empty<SpatialConstraintSpec>();
            StoryRules = storyRules ?? Array.Empty<StoryRuleSpec>();
            Objectives = objectives ?? Array.Empty<ObjectiveSpec>();
            SecretPolicies = secretPolicies ?? Array.Empty<SecretPolicySpec>();
            RequiredSecrets = requiredSecrets ?? Array.Empty<RequiredSecretSpec>();
            LootTables = lootTables ?? Array.Empty<LootTableSpec>();
        }

        /// <summary>
        /// Normalizes capabilities implied by authored content. A cutscene with physical stage points
        /// is itself proof that its host must support cutscene staging; authors should not repeat the
        /// same fact with RequireCapability(CutsceneStage). The detailed CutsceneStagePlan remains the
        /// actual geometry requirement consumed by generation.
        /// </summary>
        private static SiteSpec[] DeriveSiteCapabilities(
            SiteSpec[] sites,
            IReadOnlyList<CutsceneSpec> cutscenes)
        {
            var result = new SiteSpec[sites.Length];
            for (var i = 0; i < sites.Length; i++)
            {
                SiteSpec site = sites[i];
                bool requiresCutsceneStage = false;
                for (var j = 0; j < cutscenes.Count; j++)
                {
                    CutsceneSpec cutscene = cutscenes[j];
                    if (cutscene.Site.Equals(site.Ref)
                        && cutscene.Definition.StageRequirements.Count > 0)
                    {
                        requiresCutsceneStage = true;
                        break;
                    }
                }

                if (!requiresCutsceneStage || HasCapability(site, SiteCapabilityKind.CutsceneStage))
                {
                    result[i] = site;
                    continue;
                }

                var capabilities = new SiteCapabilityRequirement[site.Capabilities.Count + 1];
                for (var j = 0; j < site.Capabilities.Count; j++)
                    capabilities[j] = site.Capabilities[j];
                capabilities[capabilities.Length - 1] = SiteCapability.CutsceneStage;
                result[i] = new SiteSpec(site.Ref, site.Archetype, capabilities);
            }
            return result;
        }

        private static bool HasCapability(SiteSpec site, SiteCapabilityKind kind)
        {
            for (var i = 0; i < site.Capabilities.Count; i++)
                if (site.Capabilities[i].Kind == kind)
                    return true;
            return false;
        }
    }

    public static class Campaign
    {
        public static CampaignBuilder Create(string id) => new CampaignBuilder(id);
    }
}
