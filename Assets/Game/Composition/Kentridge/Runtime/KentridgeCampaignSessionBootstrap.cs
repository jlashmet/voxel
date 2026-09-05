using System;
using System.Collections.Generic;
using Game.Characters.Api;
using Game.Composition.Campaign.Runtime;
using Game.Composition.Kentridge.Api;
using Game.Composition.WorldBuilderWorldGen;
using Game.Composition.WorldBuilderWorldGen.Runtime;
using Game.Cutscenes.Api;
using Game.Inventory.Api;
using Game.Inventory.Runtime;
using Game.Quests.Api;
using Game.WorldBuilder.Api;

namespace Game.Composition.Kentridge.Runtime
{
    public sealed class KentridgeWellQuestRewardRuntime
    {
        private static readonly ItemRef RewardRef =
            new ItemRef(KentridgeWellQuestDefinition.RewardItemId);
        private static readonly InventoryTransactionId RewardTransactionId =
            new InventoryTransactionId("kentridge.well-quest.reward");

        private readonly IInventoryQuery _inventory;
        private readonly IInventoryAuthority _authority;
        private readonly InventoryId _inventoryId;

        public KentridgeWellQuestRewardRuntime(
            IInventoryQuery inventory,
            IInventoryAuthority authority,
            InventoryId inventoryId)
        {
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _authority = authority ?? throw new ArgumentNullException(nameof(authority));
            if (!inventoryId.IsValid) throw new ArgumentException("Inventory id is required.", nameof(inventoryId));
            _inventoryId = inventoryId;
        }

        public bool Synchronize(bool questCompleted)
        {
            if (!questCompleted || _inventory.Count(_inventoryId, RewardRef) > 0) return false;
            InventoryTransactionResult result = _authority.Add(new InventoryAddRequest(
                RewardTransactionId,
                _inventoryId,
                RewardRef,
                1));
            return result.Succeeded && result.Changes.Count > 0;
        }
    }

    public sealed class KentridgeCampaignSession
    {
        private readonly KentridgeWellQuestRewardRuntime _wellQuestRewards;

        public CampaignBlueprint Blueprint { get; }
        public KentridgeCampaignGenerationPlan Generation { get; }
        public KentridgeCampaignWorldRealization World { get; }
        public CampaignRuntime Runtime { get; }
        public IInventoryQuery Inventory { get; }
        public IInventoryAuthority InventoryAuthority { get; }
        public IInventoryStatePort InventoryState { get; }
        public InventoryId PlayerInventoryId { get; }

        internal KentridgeCampaignSession(
            CampaignBlueprint blueprint,
            KentridgeCampaignGenerationPlan generation,
            KentridgeCampaignWorldRealization world,
            CampaignRuntime runtime,
            IInventoryQuery inventory,
            IInventoryAuthority inventoryAuthority,
            IInventoryStatePort inventoryState,
            InventoryId playerInventoryId,
            KentridgeWellQuestRewardRuntime wellQuestRewards)
        {
            Blueprint = blueprint ?? throw new ArgumentNullException(nameof(blueprint));
            Generation = generation ?? throw new ArgumentNullException(nameof(generation));
            World = world ?? throw new ArgumentNullException(nameof(world));
            Runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            Inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            InventoryAuthority = inventoryAuthority ?? throw new ArgumentNullException(nameof(inventoryAuthority));
            InventoryState = inventoryState ?? throw new ArgumentNullException(nameof(inventoryState));
            if (!playerInventoryId.IsValid) throw new ArgumentException("Player inventory id is required.", nameof(playerInventoryId));
            PlayerInventoryId = playerInventoryId;
            _wellQuestRewards = wellQuestRewards ?? throw new ArgumentNullException(nameof(wellQuestRewards));
        }

        public int StartNewGame()
        {
            int matched = Runtime.StartNewGame();
            SynchronizeRewards();
            return matched;
        }

        public IReadOnlyList<QuestEvent> ObserveQuest(QuestObservation observation)
        {
            IReadOnlyList<QuestEvent> events = Runtime.ObserveQuest(observation);
            SynchronizeRewards();
            return events;
        }

        public bool SynchronizeRewards()
        {
            return _wellQuestRewards.Synchronize(
                Runtime.IsQuestCompleted(KentridgeWellQuestDefinition.Ref));
        }
    }

    public static class KentridgeCampaignSessionBootstrap
    {
        public const string ForestBanditLootItemId = "kentridge-forest-bandit-keepsake";

        public static KentridgeCampaignGenerationPlan Plan(
            CampaignBlueprint blueprint,
            AuthoredTownPlan town)
        {
            if (blueprint == null) throw new ArgumentNullException(nameof(blueprint));
            if (town == null) throw new ArgumentNullException(nameof(town));

            return KentridgeCampaignWorldPlanner.Plan(blueprint, town);
        }

        public static KentridgeCampaignSession CreateSession(
            CampaignBlueprint blueprint,
            KentridgeCampaignGenerationPlan generation,
            KentridgeCampaignRealizationFacts realizationFacts,
            IKentridgeCampaignActorHost actors,
            ICutscenePresentation presentation,
            IKentridgeCampaignSecretHost secretHost = null)
        {
            if (blueprint == null) throw new ArgumentNullException(nameof(blueprint));
            if (generation == null) throw new ArgumentNullException(nameof(generation));
            if (realizationFacts == null) throw new ArgumentNullException(nameof(realizationFacts));
            if (actors == null) throw new ArgumentNullException(nameof(actors));
            if (presentation == null) throw new ArgumentNullException(nameof(presentation));
            if (!ReferenceEquals(blueprint, generation.Blueprint))
                throw new InvalidOperationException(
                    "Kentridge session blueprint does not own the supplied campaign generation plan.");

            KentridgeCampaignWorldRealization world =
                KentridgeCampaignWorldRealizationBoundary.Realize(
                    generation,
                    realizationFacts);

            if (world.Secrets.Count > 0 && secretHost == null)
                throw new ArgumentNullException(
                    nameof(secretHost),
                    "Campaign selected physical secrets but no gameplay secret host was supplied.");
            ValidatePlayerBindings(blueprint, actors);
            ValidateNpcPlacements(blueprint, world.Npcs);

            if (world.Secrets.Count > 0)
                secretHost.PrepareSecrets(world.Secrets);

            actors.PrepareNpcs(world.Npcs);
            ValidatePreparedNpcs(world.Npcs, actors);
            ValidateAllCutsceneBindings(blueprint, actors);

            var runtime = new CampaignRuntime(
                blueprint,
                world.CutsceneStages,
                actors,
                presentation,
                KentridgeWellQuestDefinition.CreateDefinitions());

            ItemRef reward = new ItemRef(KentridgeWellQuestDefinition.RewardItemId);
            ItemRef forestLoot = new ItemRef(ForestBanditLootItemId);
            CharacterId primaryCharacter = CharacterId.FromStableKey("player-slot", "0");
            var playerInventoryId = new InventoryId("kentridge.inventory.player-slot-0");
            var inventory = new InventoryRuntime(
                new[]
                {
                    new ItemDefinition(reward, "Well Rescue Token", "W"),
                    new ItemDefinition(forestLoot, "Bandit Keepsake", "B")
                },
                new[]
                {
                    new InventoryDescriptor(
                        playerInventoryId,
                        new InventoryBindingMetadata("character", primaryCharacter.Value))
                });
            var rewards = new KentridgeWellQuestRewardRuntime(
                inventory,
                inventory,
                playerInventoryId);

            return new KentridgeCampaignSession(
                blueprint,
                generation,
                world,
                runtime,
                inventory,
                inventory,
                inventory,
                playerInventoryId,
                rewards);
        }

        private static void ValidatePlayerBindings(
            CampaignBlueprint blueprint,
            IKentridgeCampaignActorHost actors)
        {
            for (var i = 0; i < blueprint.Cutscenes.Count; i++)
            {
                CutsceneSpec cutscene = blueprint.Cutscenes[i];
                for (var j = 0; j < cutscene.ActorBindings.Count; j++)
                {
                    CutsceneActorBindingSpec binding = cutscene.ActorBindings[j];
                    if (binding.Target.Kind != CutsceneActorTargetKind.PlayerSlot) continue;

                    ICutsceneActorRuntime player;
                    if (!actors.TryResolvePlayer(binding.Target.PlayerSlot, out player) || player == null)
                        throw new InvalidOperationException(
                            "Campaign requires player slot " + binding.Target.PlayerSlot +
                            " for cutscene '" + cutscene.Ref +
                            "', but the actor host has no authoritative player runtime.");
                }
            }
        }

        private static void ValidateNpcPlacements(
            CampaignBlueprint blueprint,
            IReadOnlyList<ResolvedNpcWorldPlacement> placements)
        {
            var realized = new HashSet<NpcRef>();
            for (var i = 0; i < placements.Count; i++)
            {
                ResolvedNpcWorldPlacement placement = placements[i]
                    ?? throw new InvalidOperationException(
                        "Kentridge world realization contains a null NPC placement at index " + i + ".");
                if (!realized.Add(placement.Npc))
                    throw new InvalidOperationException(
                        "Kentridge world realization contains more than one placement for NPC '" +
                        placement.Npc + "'.");
            }

            for (var i = 0; i < blueprint.Cutscenes.Count; i++)
            {
                CutsceneSpec cutscene = blueprint.Cutscenes[i];
                for (var j = 0; j < cutscene.ActorBindings.Count; j++)
                {
                    CutsceneActorBindingSpec binding = cutscene.ActorBindings[j];
                    if (binding.Target.Kind != CutsceneActorTargetKind.Npc) continue;
                    if (!realized.Contains(binding.Target.Npc))
                        throw new InvalidOperationException(
                            "Cutscene '" + cutscene.Ref + "' requires NPC '" + binding.Target.Npc +
                            "', but the generated world contains no physical placement for it.");
                }
            }
        }

        private static void ValidatePreparedNpcs(
            IReadOnlyList<ResolvedNpcWorldPlacement> placements,
            IKentridgeCampaignActorHost actors)
        {
            for (var i = 0; i < placements.Count; i++)
            {
                ResolvedNpcWorldPlacement placement = placements[i];
                ICutsceneActorRuntime prepared;
                if (!actors.TryResolveNpc(placement.Npc, out prepared) || prepared == null)
                    throw new InvalidOperationException(
                        "Campaign actor host prepared NPC batch but did not expose NPC '" +
                        placement.Npc + "' through TryResolveNpc.");
            }
        }

        private static void ValidateAllCutsceneBindings(
            CampaignBlueprint blueprint,
            IKentridgeCampaignActorHost actors)
        {
            for (var i = 0; i < blueprint.Cutscenes.Count; i++)
            {
                CutsceneSpec cutscene = blueprint.Cutscenes[i];
                for (var j = 0; j < cutscene.ActorBindings.Count; j++)
                {
                    CutsceneActorBindingSpec binding = cutscene.ActorBindings[j];
                    ICutsceneActorRuntime actor;
                    bool resolved;
                    switch (binding.Target.Kind)
                    {
                        case CutsceneActorTargetKind.Npc:
                            resolved = actors.TryResolveNpc(binding.Target.Npc, out actor);
                            break;
                        case CutsceneActorTargetKind.PlayerSlot:
                            resolved = actors.TryResolvePlayer(binding.Target.PlayerSlot, out actor);
                            break;
                        default:
                            throw new InvalidOperationException(
                                "Cutscene '" + cutscene.Ref +
                                "' contains unsupported actor target kind '" + binding.Target.Kind + "'.");
                    }

                    if (!resolved || actor == null)
                        throw new InvalidOperationException(
                            "Campaign actor host cannot resolve cutscene actor '" + binding.Actor +
                            "' for cutscene '" + cutscene.Ref + "'.");
                }
            }
        }
    }
}
