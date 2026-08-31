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
                throw new InvalidOperationException($"Objective '{_ref}' requires a target site.");
            if (_completion == null)
                throw new InvalidOperationException($"Objective '{_ref}' requires a completion rule.");
            return new ObjectiveSpec(_ref, _target, _completion);
        }
    }

    public sealed class CutsceneBuilder
    {
        private readonly CutsceneRef _ref;
        private readonly CutsceneDefinition _definition;
        private readonly List<CutsceneActorBindingSpec> _actorBindings = new List<CutsceneActorBindingSpec>();
        private SiteRef _site;
        private bool _hasSite;

        public CutsceneRef Ref => _ref;
        public CutsceneDefinition Definition => _definition;

        internal CutsceneBuilder(CutsceneRef @ref, CutsceneDefinition definition)
        {
            _ref = @ref;
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        }

        public CutsceneBuilder At(SiteRef site)
        {
            _site = site;
            _hasSite = true;
            return this;
        }

        public CutsceneBuilder Bind(CutsceneActorId actor, CutsceneActorTargetSpec target)
        {
            _actorBindings.Add(new CutsceneActorBindingSpec(actor, target));
            return this;
        }

        internal CutsceneSpec Build()
        {
            if (!_hasSite)
                throw new InvalidOperationException($"Cutscene '{_ref}' requires a site.");
            return new CutsceneSpec(_ref, _definition, _site, _actorBindings.ToArray());
        }
    }

    public sealed class StoryRuleBuilder
    {
        private readonly StoryRuleRef _ref;
        private IStoryTriggerSpec _trigger;
        private readonly List<IStoryConditionSpec> _conditions = new List<IStoryConditionSpec>();
        private readonly List<IStoryEffectSpec> _effects = new List<IStoryEffectSpec>();

        internal StoryRuleBuilder(StoryRuleRef @ref) => _ref = @ref;

        public StoryRuleBuilder When(IStoryTriggerSpec trigger)
        {
            _trigger = trigger ?? throw new ArgumentNullException(nameof(trigger));
            return this;
        }

        public StoryRuleBuilder If(IStoryConditionSpec condition)
        {
            _conditions.Add(condition ?? throw new ArgumentNullException(nameof(condition)));
            return this;
        }

        public StoryRuleBuilder Then(IStoryEffectSpec effect)
        {
            _effects.Add(effect ?? throw new ArgumentNullException(nameof(effect)));
            return this;
        }

        internal StoryRuleSpec Build()
        {
            if (_trigger == null)
                throw new InvalidOperationException($"Story rule '{_ref}' requires a trigger.");
            if (_effects.Count == 0)
                throw new InvalidOperationException($"Story rule '{_ref}' requires at least one effect.");
            return new StoryRuleSpec(_ref, _trigger, _conditions.ToArray(), _effects.ToArray());
        }
    }

    public sealed class SecretPolicyBlueprintBuilder
    {
        private readonly CampaignBuilder _campaign;
        internal SecretPolicyBlueprintBuilder(CampaignBuilder campaign) => _campaign = campaign;

        public SecretPolicyRef Policy(string id, Action<SecretPolicyBuilder> configure)
        {
            var policyRef = new SecretPolicyRef(id);
            var builder = new SecretPolicyBuilder(policyRef);
            configure?.Invoke(builder);
            _campaign.SecretPolicies.Add(builder.Build());
            return policyRef;
        }
    }

    public sealed class SecretPolicyBuilder
    {
        private readonly SecretPolicyRef _ref;
        private SecretScope _scope = SecretScope.ExplorableSites;
        private readonly List<SecretEntranceType> _entranceTypes = new List<SecretEntranceType>();
        private SecretDistribution _distribution;
        private bool _hasDistribution;
        private bool _requiresHiddenSpace;
        private ContainerArchetype _container = ContainerArchetype.TreasureChest;
        private LootTableRef _reward;
        private bool _hasReward;

        internal SecretPolicyBuilder(SecretPolicyRef @ref) => _ref = @ref;

        public SecretPolicyBuilder Scope(SecretScope scope)
        {
            _scope = scope;
            return this;
        }

        public SecretPolicyBuilder Entrance(SecretEntranceType entrance)
        {
            _entranceTypes.Add(entrance);
            return this;
        }

        public SecretPolicyBuilder Distribution(SecretDistribution distribution)
        {
            _distribution = distribution;
            _hasDistribution = true;
            return this;
        }

        public SecretPolicyBuilder RequireHiddenSpace()
        {
            _requiresHiddenSpace = true;
            return this;
        }

        public SecretPolicyBuilder Container(ContainerArchetype container)
        {
            _container = container;
            return this;
        }

        public SecretPolicyBuilder RewardWith(LootTableRef reward)
        {
            _reward = reward;
            _hasReward = true;
            return this;
        }

        internal SecretPolicySpec Build()
        {
            if (_entranceTypes.Count == 0)
                throw new InvalidOperationException($"Secret policy '{_ref}' requires at least one entrance type.");
            if (!_hasDistribution)
                throw new InvalidOperationException($"Secret policy '{_ref}' requires a distribution.");
            if (!_hasReward)
                throw new InvalidOperationException($"Secret policy '{_ref}' requires a reward table.");

            return new SecretPolicySpec(
                _ref,
                _scope,
                _entranceTypes.ToArray(),
                _distribution,
                _requiresHiddenSpace,
                _container,
                _reward);
        }
    }

    public sealed class LootBlueprintBuilder
    {
        private readonly CampaignBuilder _campaign;
        internal LootBlueprintBuilder(CampaignBuilder campaign) => _campaign = campaign;

        public LootTableRef Table(string id, Action<LootTableBuilder> configure)
        {
            var tableRef = new LootTableRef(id);
            var builder = new LootTableBuilder(tableRef);
            configure?.Invoke(builder);
            _campaign.LootTables.Add(builder.Build());
            return tableRef;
        }
    }

    public sealed class LootTableBuilder
    {
        private readonly LootTableRef _ref;
        private int _minimumRolls;
        private int _maximumRolls;
        private bool _hasRollCount;
        private readonly List<LootCategory> _guaranteed = new List<LootCategory>();
        private readonly List<GuaranteedLootItem> _guaranteedItems = new List<GuaranteedLootItem>();
        private readonly List<WeightedLootCategory> _weighted = new List<WeightedLootCategory>();
        private readonly List<WeightedLootItem> _weightedItems = new List<WeightedLootItem>();

        internal LootTableBuilder(LootTableRef @ref) => _ref = @ref;

        public LootTableBuilder RollCount(int minimum, int maximum)
        {
            if (minimum < 0) throw new ArgumentOutOfRangeException(nameof(minimum));
            if (maximum < minimum) throw new ArgumentOutOfRangeException(nameof(maximum));
            _minimumRolls = minimum;
            _maximumRolls = maximum;
            _hasRollCount = true;
            return this;
        }

        public LootTableBuilder Guaranteed(LootCategory category)
        {
            _guaranteed.Add(category);
            return this;
        }

        public LootTableBuilder Guaranteed(LootItemId item, int quantity)
            => Guaranteed(item, LootQuantityRange.Exactly(quantity));

        public LootTableBuilder Guaranteed(LootItemId item, int minimumQuantity, int maximumQuantity)
            => Guaranteed(item, new LootQuantityRange(minimumQuantity, maximumQuantity));

        public LootTableBuilder Guaranteed(LootItemId item, LootQuantityRange quantity)
        {
            _guaranteedItems.Add(new GuaranteedLootItem(item, quantity));
            return this;
        }

        public LootTableBuilder Weighted(LootCategory category, int weight)
        {
            _weighted.Add(new WeightedLootCategory(category, weight));
            return this;
        }

        public LootTableBuilder Weighted(LootItemId item, int weight, int quantity = 1)
            => Weighted(item, weight, LootQuantityRange.Exactly(quantity));

        public LootTableBuilder Weighted(
            LootItemId item,
            int weight,
            int minimumQuantity,
            int maximumQuantity)
            => Weighted(item, weight, new LootQuantityRange(minimumQuantity, maximumQuantity));

        public LootTableBuilder Weighted(LootItemId item, int weight, LootQuantityRange quantity)
        {
            _weightedItems.Add(new WeightedLootItem(item, weight, quantity));
            return this;
        }

        internal LootTableSpec Build()
        {
            if (!_hasRollCount)
                throw new InvalidOperationException($"Loot table '{_ref}' requires a roll count.");
            return new LootTableSpec(
                _ref,
                _minimumRolls,
                _maximumRolls,
                _guaranteed.ToArray(),
                _guaranteedItems.ToArray(),
                _weighted.ToArray(),
                _weightedItems.ToArray());
        }
    }
}
