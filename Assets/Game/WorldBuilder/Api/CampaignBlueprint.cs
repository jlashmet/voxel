using System;
using System.Collections.Generic;

namespace Game.WorldBuilder.Api
{
    public sealed class CampaignBlueprint
    {
        public string Id { get; }
        public IReadOnlyList<SiteSpec> Sites { get; }
        public IReadOnlyList<NpcSpec> Npcs { get; }
        public IReadOnlyList<SpatialConstraintSpec> SpatialConstraints { get; }
        public IReadOnlyList<CutsceneSpec> Cutscenes { get; }
        public IReadOnlyList<ObjectiveSpec> Objectives { get; }
        public IReadOnlyList<SecretPolicySpec> SecretPolicies { get; }
        public IReadOnlyList<LootTableSpec> LootTables { get; }

        internal CampaignBlueprint(
            string id,
            SiteSpec[] sites,
            NpcSpec[] npcs,
            SpatialConstraintSpec[] spatialConstraints,
            CutsceneSpec[] cutscenes,
            ObjectiveSpec[] objectives,
            SecretPolicySpec[] secretPolicies,
            LootTableSpec[] lootTables)
        {
            Id = WorldIdRules.Require(id, nameof(id));
            Sites = sites ?? Array.Empty<SiteSpec>();
            Npcs = npcs ?? Array.Empty<NpcSpec>();
            SpatialConstraints = spatialConstraints ?? Array.Empty<SpatialConstraintSpec>();
            Cutscenes = cutscenes ?? Array.Empty<CutsceneSpec>();
            Objectives = objectives ?? Array.Empty<ObjectiveSpec>();
            SecretPolicies = secretPolicies ?? Array.Empty<SecretPolicySpec>();
            LootTables = lootTables ?? Array.Empty<LootTableSpec>();
        }
    }

    public static class Campaign
    {
        public static CampaignBuilder Create(string id) => new CampaignBuilder(id);
    }
}
