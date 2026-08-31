using System;
using System.Collections.Generic;
using Game.Cutscenes.Api;

namespace Game.WorldBuilder.Api
{
    public sealed partial class CampaignBuilder
    {
        private readonly string _id;
        internal readonly List<SiteSpec> Sites = new List<SiteSpec>();
        internal readonly List<NpcSpec> Npcs = new List<NpcSpec>();
        internal readonly List<SpatialConstraintSpec> SpatialConstraints = new List<SpatialConstraintSpec>();
        internal readonly List<CutsceneSpec> Cutscenes = new List<CutsceneSpec>();
        internal readonly List<StoryRuleSpec> StoryRules = new List<StoryRuleSpec>();
        internal readonly List<ObjectiveSpec> Objectives = new List<ObjectiveSpec>();
        internal readonly List<SecretPolicySpec> SecretPolicies = new List<SecretPolicySpec>();
        internal readonly List<RequiredSecretSpec> RequiredSecrets = new List<RequiredSecretSpec>();
        internal readonly List<SecretClueSpec> SecretClues = new List<SecretClueSpec>();
        internal readonly List<LootTableSpec> LootTables = new List<LootTableSpec>();

        public WorldBlueprintBuilder World { get; }
        public StoryBlueprintBuilder Story { get; }
        public LootBlueprintBuilder Loot { get; }

        internal CampaignBuilder(string id)
        {
            _id = WorldIdRules.Require(id, nameof(id));
            World = new WorldBlueprintBuilder(this);
            Story = new StoryBlueprintBuilder(this);
            Loot = new LootBlueprintBuilder(this);
        }

        public CampaignBlueprint Build() =>
            new CampaignBlueprint(
                _id,
                BuildHierarchy(),
                Sites.ToArray(),
                SiteSourceEvidence.ToArray(),
                Npcs.ToArray(),
                SpatialConstraints.ToArray(),
                Cutscenes.ToArray(),
                StoryRules.ToArray(),
                Objectives.ToArray(),
                SecretPolicies.ToArray(),
                RequiredSecrets.ToArray(),
                SecretClues.ToArray(),
                LootTables.ToArray());
    }

    public sealed partial class WorldBlueprintBuilder
    {
        private readonly CampaignBuilder _campaign;
        public SecretPolicyBlueprintBuilder Secrets { get; }

        internal WorldBlueprintBuilder(CampaignBuilder campaign)
        {
            _campaign = campaign;
            Secrets = new SecretPolicyBlueprintBuilder(campaign);
        }

        internal SiteRef RequireSite(string id, Action<SiteBuilder> configure)
        {
            var siteRef = new SiteRef(id);
            var builder = new SiteBuilder(siteRef, _campaign.SpatialConstraints);
            configure?.Invoke(builder);
            _campaign.Sites.Add(builder.Build());
            return siteRef;
        }

        internal NpcRef RequireNpc(string id, Action<NpcBuilder> configure)
        {
            var npcRef = new NpcRef(id);
            var builder = new NpcBuilder(npcRef);
            configure?.Invoke(builder);
            _campaign.Npcs.Add(builder.Build());
            return npcRef;
        }
    }

    public sealed class SiteBuilder
    {
        private readonly SiteRef _ref;
        private readonly List<SpatialConstraintSpec> _constraintSink;
        private readonly List<SiteCapabilityRequirement> _capabilities = new List<SiteCapabilityRequirement>();
        private SiteArchetype _archetype;

        internal SiteBuilder(SiteRef @ref, List<SpatialConstraintSpec> constraintSink)
        {
            _ref = @ref;
            _constraintSink = constraintSink;
            _archetype = SiteArchetype.Unspecified;
        }

        public SiteBuilder Archetype(SiteArchetype archetype)
        {
            _archetype = archetype;
            return this;
        }

        public SiteBuilder RequireCapability(SiteCapabilityRequirement capability)
        {
            _capabilities.Add(capability);
            return this;
        }

        public SiteBuilder DifferentSiteFrom(SiteRef other)
        {
            _constraintSink.Add(SpatialConstraintSpec.DifferentSite(_ref, other));
            return this;
        }

        public SiteBuilder ReachableFrom(SiteRef other, TraversalProfile traversal)
        {
            _constraintSink.Add(SpatialConstraintSpec.ReachableFrom(_ref, other, traversal));
            return this;
        }

        public SiteBuilder BoundaryDistanceFrom(SiteRef other, DistanceRangeMetres distance)
        {
            _constraintSink.Add(SpatialConstraintSpec.BoundaryDistanceRange(_ref, other, distance));
            return this;
        }

        public SiteBuilder EntranceDistanceFrom(SiteRef other, DistanceRangeMetres distance)
        {
            _constraintSink.Add(SpatialConstraintSpec.PublicEntranceDistanceRange(_ref, other, distance));
            return this;
        }

        public SiteBuilder TravelDistanceFrom(
            SiteRef other,
            TraversalProfile traversal,
            DistanceRangeMetres distance)
        {
            _constraintSink.Add(SpatialConstraintSpec.TraversalDistanceRange(_ref, other, traversal, distance));
            return this;
        }

        internal SiteSpec Build() => new SiteSpec(_ref, _archetype, _capabilities.ToArray());
    }

    public sealed class NpcBuilder
    {
        private readonly NpcRef _ref;
        private SiteRef _site;
        private bool _placed;
        private bool _requiresConversation;

        internal NpcBuilder(NpcRef @ref) => _ref = @ref;

        public NpcBuilder PlaceAt(SiteRef site)
        {
            _site = site;
            _placed = true;
            return this;
        }

        public NpcBuilder RequireConversation()
        {
            _requiresConversation = true;
            return this;
        }

        internal NpcSpec Build()
        {
            if (!_placed)
                throw new InvalidOperationException($"NPC '{_ref}' must be placed at a site.");
            return new NpcSpec(_ref, _site, _requiresConversation);
        }
    }

    public sealed class StoryBlueprintBuilder
    {
        private readonly CampaignBuilder _campaign;
        internal StoryBlueprintBuilder(CampaignBuilder campaign) => _campaign = campaign;

        internal ObjectiveRef Objective(string id, Action<ObjectiveBuilder> configure)
        {
            var objectiveRef = new ObjectiveRef(id);
            var builder = new ObjectiveBuilder(objectiveRef);
            configure?.Invoke(builder);
            _campaign.Objectives.Add(builder.Build());
            return objectiveRef;
        }

        internal CutsceneRef Cutscene(CutsceneDefinition definition, Action<CutsceneBuilder> configure)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            var cutsceneRef = new CutsceneRef(definition.Id);
            var builder = new CutsceneBuilder(cutsceneRef, definition);
            configure?.Invoke(builder);
            _campaign.Cutscenes.Add(builder.Build());
            return cutsceneRef;
        }

        public StoryRuleRef Rule(string id, Action<StoryRuleBuilder> configure)
        {
            var ruleRef = new StoryRuleRef(id);
            var builder = new StoryRuleBuilder(ruleRef);
            configure?.Invoke(builder);
            _campaign.StoryRules.Add(builder.Build());
            return ruleRef;
        }
    }

    public sealed class ObjectiveBuilder
    {
        private readonly ObjectiveRef _ref;
        private SiteRef _target;
        private bool _hasTarget;
        private IObjectiveCompletionSpec _completion;

        internal ObjectiveBuilder(ObjectiveRef @ref) => _ref = @ref;

        public ObjectiveBuilder Target(SiteRef site)
        {
            _target = site;
            _hasTarget = true;
            return this;
        }

        public ObjectiveBuilder CompleteWhen(IObjectiveCompletionSpec completion)
        {
            _completion = completion ?? throw new ArgumentNullException(nameof(completion));
            return this;
        }

        internal ObjectiveSpec Build()
        {
            if (!_hasTarget)
                throw new InvalidOperationException($"Objective '{_ref}' must target a site.");
            if (_completion == null)
                throw new InvalidOperationException($"Objective '{_ref}' must define completion semantics.");
            return new ObjectiveSpec(_ref, _target, _completion);
        }
    }
}
