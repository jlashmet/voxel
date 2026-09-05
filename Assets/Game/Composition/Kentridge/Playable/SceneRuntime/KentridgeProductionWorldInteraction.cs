using System;
using System.Globalization;
using Game.Application.Api;
using Game.Characters.Api;
using Game.Composition.Kentridge.Api;
using Game.Composition.Kentridge.Playable;
using Game.Composition.Kentridge.Runtime;
using Game.Input.Api;
using Game.Inventory.Api;
using Game.Inventory.Runtime;
using Game.Loot.Runtime;
using Game.Quests.Api;
using Game.WorldObjects.Api;
using Game.WorldObjects.Runtime;
using UnityEngine;

namespace Game.Kentridge.PlayableSlice
{
    /// <summary>
    /// Production composition adapter for the Kentridge representative interaction route. It owns no
    /// gameplay truth: WorldObjects owns pickup state, Loot/Inventory owns transfer state, Quests owns
    /// well progression, Characters supplies the actor identity, and the Application root supplies the
    /// single production input reader.
    /// </summary>
    [DefaultExecutionOrder(-17500)]
    public sealed class KentridgeProductionWorldInteraction : MonoBehaviour, IWorldInteractionFactSink
    {
        private const ulong LocalSenderId = 24UL;
        private const float WellInteractionRangeMetres = 3.2f;
        private static readonly LocalPlayerId LocalPlayer = new LocalPlayerId(0);
        private static readonly WorldObjectId ForestLootObjectId =
            new WorldObjectId("kentridge-forest-bandit-loot");
        private static readonly ItemRef ForestLootItem =
            new ItemRef(KentridgeCampaignSessionBootstrap.ForestBanditLootItemId);

        private KentridgeProductionCompositionRoot _root;
        private KentridgePlayableSlice _slice;
        private KentridgeForestBanditEncounter _forest;
        private KentridgeWellQuestInventoryPresentation _wellPresentation;
        private KentridgeSessionRuntimeGraph _graph;
        private WorldObjectRegistry _objects;
        private InteractionClickedProcessor _interaction;
        private ItemPickupObject _forestPickup;
        private WorldInteractionFact? _lastFact;
        private WorldInteractionFailure _lastFailure;
        private bool _wellInteractionObserved;

        public bool PickupSpawned => _forestPickup != null;
        public bool PickupCollected => _forestPickup != null && !_forestPickup.Enabled;
        public bool WellInteractionObserved => _wellInteractionObserved;
        public WorldInteractionFailure LastFailure => _lastFailure;
        public WorldInteractionFact? LastFact => _lastFact;
        public int ForestLootCount =>
            _graph == null || _graph.IsDisposed
                ? 0
                : _graph.Session.Inventory.Count(_graph.Session.PlayerInventoryId, ForestLootItem);

        private void Awake()
        {
            _root = GetComponent<KentridgeProductionCompositionRoot>()
                ?? throw new InvalidOperationException(
                    "Kentridge world interaction requires the production Application root.");
            _slice = GetComponent<KentridgePlayableSlice>()
                ?? throw new InvalidOperationException(
                    "Kentridge world interaction requires the production playable slice.");
            _forest = GetComponent<KentridgeForestBanditEncounter>()
                ?? throw new InvalidOperationException(
                    "Kentridge world interaction requires the production forest encounter extension.");
            _wellPresentation = GetComponent<KentridgeWellQuestInventoryPresentation>();
        }

        private void Update()
        {
            if (!Application.isPlaying) return;

            KentridgeSessionRuntimeGraph current = _slice.SessionFactory?.Current;
            if (!ReferenceEquals(current, _graph)) ComposeForGraph(current);
            if (_graph == null || _graph.IsDisposed) return;
            if (_root.FlowSnapshot.Lifecycle != ApplicationLifecycle.InGame) return;

            if (_forest.CombatResolved && _forestPickup == null)
                SpawnForestPickup();

            IInputActionStateReader input = _root.InputActions;
            if (input == null || !input.WasPressed(LocalPlayer, StandardInputActions.Interact)) return;

            if (TryCollectForestPickup()) return;
            TryObserveWellInteraction();
        }

        public void Publish(WorldInteractionFact fact)
        {
            _lastFact = fact;
            Debug.Log(
                "SYSTEM24 world-interaction: object=" + fact.ObjectId +
                " kind=" + fact.Kind +
                " state=" + fact.StateCode +
                " revision=" + fact.ObjectRevision);
        }

        private void ComposeForGraph(KentridgeSessionRuntimeGraph graph)
        {
            _graph = graph;
            _objects = null;
            _interaction = null;
            _forestPickup = null;
            _lastFact = null;
            _lastFailure = WorldInteractionFailure.None;
            _wellInteractionObserved = false;
            if (graph == null || graph.IsDisposed) return;

            KentridgeCampaignSession session = graph.Session;
            KentridgeCharacterHost host = _slice.CharacterHost
                ?? throw new InvalidOperationException(
                    "Kentridge world interaction requires the production character host.");

            CharacterRegistryFailure binding = host.Characters.Bind(
                host.PlayerCharacterId,
                new CharacterBinding("steam", LocalSenderId.ToString(CultureInfo.InvariantCulture)));
            if (binding != CharacterRegistryFailure.None && binding != CharacterRegistryFailure.DuplicateBinding)
                throw new InvalidOperationException(
                    "Kentridge world interaction could not bind the local platform identity: " + binding + ".");

            var inventoryTransactions = new InventoryTransactionsAdapter(
                session.InventoryAuthority,
                session.Inventory,
                session.InventoryState);
            var inventoryBindings = new CharacterInventoryBindings();
            if (!inventoryBindings.TryBind(host.PlayerCharacterId, session.PlayerInventoryId))
                throw new InvalidOperationException(
                    "Kentridge world interaction could not bind the player inventory.");

            _objects = new WorldObjectRegistry();
            _interaction = new InteractionClickedProcessor(host.Characters, _objects, this);
            _pickupTransfer = new WorldObjectLootAdapter(inventoryTransactions, inventoryBindings);
        }

        private IWorldItemPickupTransfer _pickupTransfer;

        private void SpawnForestPickup()
        {
            KentridgeCharacterHost host = _slice.CharacterHost;
            if (host == null || _objects == null || _pickupTransfer == null) return;
            if (!host.Characters.TryGet(host.PlayerCharacterId, out CharacterSnapshot player))
                throw new InvalidOperationException(
                    "Kentridge forest loot could not resolve the authoritative player character.");

            _forestPickup = new ItemPickupObject(
                ForestLootObjectId,
                player.Kinematics.Position,
                new WorldItemPayload(KentridgeCampaignSessionBootstrap.ForestBanditLootItemId, 1),
                _pickupTransfer);
            if (!_objects.TryRegister(_forestPickup))
                throw new InvalidOperationException(
                    "Kentridge forest loot WorldObject registration was rejected.");

            Debug.Log(
                "SYSTEM24 loot-spawned: object=" + ForestLootObjectId +
                " item=" + KentridgeCampaignSessionBootstrap.ForestBanditLootItemId);
        }

        private bool TryCollectForestPickup()
        {
            if (_forestPickup == null || !_forestPickup.Enabled || _interaction == null) return false;

            WorldInteractionResult result = _interaction.Process(LocalSenderId);
            _lastFailure = result.Failure;
            if (!result.Succeeded)
            {
                if (result.Failure != WorldInteractionFailure.NoTarget &&
                    result.Failure != WorldInteractionFailure.OutOfRange)
                    Debug.LogWarning("SYSTEM24 loot interaction rejected: " + result.Failure);
                return false;
            }

            Debug.Log(
                "SYSTEM24 loot: object=" + ForestLootObjectId +
                " inventoryCount=" + ForestLootCount);
            return true;
        }

        private bool TryObserveWellInteraction()
        {
            if (_wellPresentation == null || !_wellPresentation.IsBound || _graph == null) return false;
            KentridgeCharacterHost host = _slice.CharacterHost;
            if (host == null) return false;

            Vector3 delta = host.Position - _wellPresentation.WellWorldPosition;
            delta.y = 0f;
            if (delta.sqrMagnitude > WellInteractionRangeMetres * WellInteractionRangeMetres) return false;

            QuestSnapshot before = _graph.Session.Runtime.GetQuestSnapshot(KentridgeWellQuestDefinition.Ref);
            if (before.Status != QuestStatus.Active) return false;

            var events = _graph.ObserveQuest(
                QuestObservation.Interacted(KentridgeWellQuestDefinition.WellTargetId));
            if (events.Count == 0) return false;

            _wellInteractionObserved = true;
            _wellPresentation.SetStatusMessage("The boy is safe. Return to Madeline.");
            Debug.Log(
                "SYSTEM24 well-interaction: events=" + events.Count +
                " quest=" + KentridgeWellQuestDefinition.QuestId);
            return true;
        }
    }
}
