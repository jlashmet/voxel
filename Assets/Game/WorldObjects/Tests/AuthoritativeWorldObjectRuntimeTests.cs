using System;
using System.Collections.Generic;
using Game.Characters.Api;
using Game.Inventory.Api;
using Game.Loot.Runtime;
using Game.Progression.Api;
using Game.Progression.Runtime;
using Game.WorldObjects.Api;
using Game.WorldObjects.Runtime;
using NUnit.Framework;

namespace Game.WorldObjects.Tests
{
    public sealed class AuthoritativeWorldObjectRuntimeTests
    {
        private static readonly CharacterId ActorId = new CharacterId("player:one");
        private static readonly CharacterVector3 Origin = new CharacterVector3(1f, 2f, 3f);

        [Test]
        public void ProcessorRejectsUnknownRequesterAndNoTarget()
        {
            var characters = new FakeCharacters(ActorId, Origin, 42UL);
            var registry = new WorldObjectRegistry();
            var processor = new InteractionClickedProcessor(characters, registry);
            Assert.That(processor.Process(999UL).Failure, Is.EqualTo(WorldInteractionFailure.UnknownActor));
            Assert.That(processor.Process(42UL).Failure, Is.EqualTo(WorldInteractionFailure.NoTarget));
        }

        [Test]
        public void ProcessorSelectsLowestCoLocatedIdAndAcceptsAtMostOne()
        {
            var characters = new FakeCharacters(ActorId, Origin, 42UL);
            var registry = new WorldObjectRegistry();
            var facts = new RecordingWorldFacts();
            var later = new DoorToggleObject(new WorldObjectId("door-b"), Origin);
            var first = new DoorToggleObject(new WorldObjectId("door-a"), Origin);
            Assert.That(registry.TryRegister(later), Is.True);
            Assert.That(registry.TryRegister(first), Is.True);
            var result = new InteractionClickedProcessor(characters, registry, facts).Process(42UL);
            Assert.That(result.Succeeded, Is.True);
            Assert.That(first.IsOpen, Is.True);
            Assert.That(later.IsOpen, Is.False);
            Assert.That(facts.Facts.Count, Is.EqualTo(1));
            Assert.That(facts.Facts[0].ObjectId, Is.EqualTo(first.Id));
        }

        [Test]
        public void ValidatorRejectsOutOfRangeAndUnknownObject()
        {
            var characters = new FakeCharacters(ActorId, Origin, 42UL);
            var registry = new WorldObjectRegistry();
            var processor = new InteractionClickedProcessor(characters, registry);
            var remote = new DoorToggleObject(new WorldObjectId("remote"), new CharacterVector3(9f, 9f, 9f));
            registry.TryRegister(remote);
            Assert.That(processor.Validate(ActorId, remote.Id).Failure, Is.EqualTo(WorldInteractionFailure.OutOfRange));
            Assert.That(processor.Validate(ActorId, new WorldObjectId("missing")).Failure, Is.EqualTo(WorldInteractionFailure.UnknownObject));
        }

        [Test]
        public void PickupRequiresPayloadAndOnlyDisablesAfterSuccessfulTransfer()
        {
            var transfer = new StubPickupTransfer(WorldInteractionResult.Reject(WorldInteractionFailure.InventoryRejected));
            var pickup = new ItemPickupObject(new WorldObjectId("pickup"), Origin, new WorldItemPayload("ore", 2), transfer);
            var initial = pickup.CaptureState();
            Assert.That(pickup.Interact(new WorldInteractionContext(ActorId)).Failure, Is.EqualTo(WorldInteractionFailure.InventoryRejected));
            Assert.That(pickup.Enabled, Is.True);
            transfer.Result = WorldInteractionResult.Success();
            Assert.That(pickup.Interact(new WorldInteractionContext(ActorId)).Succeeded, Is.True);
            Assert.That(pickup.Enabled, Is.False);
            Assert.That(pickup.Interact(new WorldInteractionContext(ActorId)).Failure, Is.EqualTo(WorldInteractionFailure.InvalidState));
            Assert.That(pickup.RestoreState(initial).Succeeded, Is.True);
            Assert.That(pickup.Enabled, Is.True);
            var empty = new ItemPickupObject(new WorldObjectId("empty"), Origin, new WorldItemPayload("", 0), transfer);
            Assert.That(empty.Interact(new WorldInteractionContext(ActorId)).Failure, Is.EqualTo(WorldInteractionFailure.InvalidPayload));
        }

        [Test]
        public void LootAdapterReportsMissingRejectedAndSuccessfulInventory()
        {
            var inventory = new FakeInventoryTransactions();
            var bindings = new CharacterInventoryBindings();
            var adapter = new WorldObjectLootAdapter(inventory, bindings);
            var payload = new WorldItemPayload("ore", 2);
            Assert.That(adapter.TryTransfer(ActorId, new WorldObjectId("p"), payload).Failure,
                Is.EqualTo(WorldInteractionFailure.MissingInventory));
            var inventoryId = new InventoryId("inventory-1");
            Assert.That(bindings.TryBind(ActorId, inventoryId), Is.True);
            inventory.Failure = InventoryFailureReason.DestinationRejected;
            Assert.That(adapter.TryTransfer(ActorId, new WorldObjectId("p"), payload).Failure,
                Is.EqualTo(WorldInteractionFailure.InventoryRejected));
            inventory.Failure = InventoryFailureReason.UnknownInventory;
            Assert.That(adapter.TryTransfer(ActorId, new WorldObjectId("p"), payload).Failure,
                Is.EqualTo(WorldInteractionFailure.MissingInventory));
            inventory.Failure = InventoryFailureReason.None;
            Assert.That(adapter.TryTransfer(ActorId, new WorldObjectId("p"), payload).Succeeded, Is.True);
            Assert.That(inventory.AddCalls, Is.EqualTo(3));
            Assert.That(inventory.LastInventoryId, Is.EqualTo(inventoryId));
        }

        [Test]
        public void DoorToggleIsDeterministicAcrossRepeatedTransitionsAndRestore()
        {
            var door = new DoorToggleObject(new WorldObjectId("door"), Origin);
            var closed = door.CaptureState();
            Assert.That(door.Interact(new WorldInteractionContext(ActorId)).Succeeded, Is.True);
            Assert.That(door.IsOpen, Is.True);
            Assert.That(door.Interact(new WorldInteractionContext(ActorId)).Succeeded, Is.True);
            Assert.That(door.IsOpen, Is.False);
            Assert.That(door.RestoreState(new WorldObjectStateSnapshot(door.Id, door.Kind, true, 1, 17)).Succeeded, Is.True);
            Assert.That(door.IsOpen, Is.True);
            Assert.That(door.RestoreState(closed).Succeeded, Is.True);
            Assert.That(door.IsOpen, Is.False);
        }

        [Test]
        public void NestedSubsceneToggleRejectsInvalidStateAndRoundTrips()
        {
            var nested = new NestedSubsceneToggleObject(new WorldObjectId("nested"), Origin, "scene-a");
            var inactive = nested.CaptureState();
            Assert.That(nested.Interact(new WorldInteractionContext(ActorId)).Succeeded, Is.True);
            Assert.That(nested.ActiveState, Is.EqualTo(NestedSubsceneActiveState.Active));
            Assert.That(nested.Interact(new WorldInteractionContext(ActorId)).Succeeded, Is.True);
            Assert.That(nested.ActiveState, Is.EqualTo(NestedSubsceneActiveState.Inactive));
            Assert.That(nested.RestoreState(inactive).Succeeded, Is.True);
            var invalid = new NestedSubsceneToggleObject(new WorldObjectId("invalid-nested"), Origin, "scene-b", (NestedSubsceneActiveState)99);
            Assert.That(invalid.Interact(new WorldInteractionContext(ActorId)).Failure, Is.EqualTo(WorldInteractionFailure.InvalidState));
            Assert.That(nested.RestoreState(new WorldObjectStateSnapshot(nested.Id, nested.Kind, true, 99, 2)).Failure,
                Is.EqualTo(WorldInteractionFailure.InvalidState));
        }

        [Test]
        public void RegistryCaptureRestoreIsOrderedAndReuseSupportsThreeOfEachBehavior()
        {
            var registry = new WorldObjectRegistry();
            var transfer = new StubPickupTransfer(WorldInteractionResult.Success());
            for (var i = 2; i >= 0; i--)
            {
                Assert.That(registry.TryRegister(new ItemPickupObject(new WorldObjectId("pickup-" + i), Origin,
                    new WorldItemPayload("ore", 1), transfer)), Is.True);
                Assert.That(registry.TryRegister(new DoorToggleObject(new WorldObjectId("door-" + i), Origin)), Is.True);
                Assert.That(registry.TryRegister(new NestedSubsceneToggleObject(new WorldObjectId("nested-" + i), Origin,
                    "scene-" + i)), Is.True);
            }
            var captured = registry.CaptureState();
            Assert.That(captured.Count, Is.EqualTo(9));
            for (var i = 1; i < captured.Count; i++)
                Assert.That(captured[i - 1].ObjectId.CompareTo(captured[i].ObjectId), Is.LessThan(0));
            Assert.That(registry.RestoreState(captured).Succeeded, Is.True);
        }

        [Test]
        public void AcceptedPickupPublishesOneProgressionFactAndDoesNotDuplicateProcessing()
        {
            var characters = new FakeCharacters(ActorId, Origin, 42UL);
            var inventory = new FakeInventoryTransactions();
            var bindings = new CharacterInventoryBindings();
            bindings.TryBind(ActorId, new InventoryId("inventory-1"));
            var pickup = new ItemPickupObject(new WorldObjectId("pickup"), Origin,
                new WorldItemPayload("ore", 1), new WorldObjectLootAdapter(inventory, bindings));
            var registry = new WorldObjectRegistry();
            registry.TryRegister(pickup);
            var progressionFacts = new RecordingProgressionFacts();
            var processor = new InteractionClickedProcessor(characters, registry,
                new WorldObjectProgressionAdapter(progressionFacts));
            Assert.That(processor.Process(42UL).Succeeded, Is.True);
            Assert.That(inventory.AddCalls, Is.EqualTo(1));
            Assert.That(progressionFacts.Facts.Count, Is.EqualTo(1));
            Assert.That(progressionFacts.Facts[0].SubjectId, Is.EqualTo("pickup"));
            Assert.That(processor.Process(42UL).Failure, Is.EqualTo(WorldInteractionFailure.InvalidState));
            Assert.That(inventory.AddCalls, Is.EqualTo(1));
            Assert.That(progressionFacts.Facts.Count, Is.EqualTo(1));
        }

        [Test]
        public void RejectedTransitionPublishesNoFact()
        {
            var characters = new FakeCharacters(ActorId, Origin, 42UL);
            var registry = new WorldObjectRegistry();
            registry.TryRegister(new ItemPickupObject(new WorldObjectId("pickup"), Origin,
                new WorldItemPayload("ore", 1),
                new StubPickupTransfer(WorldInteractionResult.Reject(WorldInteractionFailure.InventoryRejected))));
            var facts = new RecordingWorldFacts();
            Assert.That(new InteractionClickedProcessor(characters, registry, facts).Process(42UL).Failure,
                Is.EqualTo(WorldInteractionFailure.InventoryRejected));
            Assert.That(facts.Facts, Is.Empty);
        }

        private sealed class FakeCharacters : ICharacterQuery
        {
            private readonly CharacterId _id;
            private readonly CharacterSnapshot _snapshot;
            private readonly CharacterBinding _binding;

            public FakeCharacters(CharacterId id, CharacterVector3 position, ulong steamId)
            {
                _id = id;
                _binding = new CharacterBinding("steam", steamId.ToString());
                _snapshot = new CharacterSnapshot(
                    new CharacterDefinition(id, CharacterTraits.PlayerControlled),
                    CharacterLifecycleState.Active,
                    new CharacterKinematicState(position, default, default),
                    1);
            }

            public IReadOnlyList<CharacterSnapshot> GetAll() => new[] { _snapshot };
            public bool TryGet(CharacterId id, out CharacterSnapshot snapshot)
            {
                snapshot = _snapshot;
                return id == _id;
            }
            public bool TryResolve(CharacterBinding binding, out CharacterId id)
            {
                id = _id;
                return binding.Equals(_binding);
            }
        }

        private sealed class StubPickupTransfer : IWorldItemPickupTransfer
        {
            public WorldInteractionResult Result;
            public StubPickupTransfer(WorldInteractionResult result) { Result = result; }
            public WorldInteractionResult TryTransfer(CharacterId actorId, WorldObjectId objectId, WorldItemPayload payload) => Result;
        }

        private sealed class RecordingWorldFacts : IWorldInteractionFactSink
        {
            public readonly List<WorldInteractionFact> Facts = new List<WorldInteractionFact>();
            public void Publish(WorldInteractionFact fact) => Facts.Add(fact);
        }

        private sealed class RecordingProgressionFacts : IProgressionFactSink
        {
            public readonly List<ProgressionFact> Facts = new List<ProgressionFact>();
            public void Publish(ProgressionFact fact) => Facts.Add(fact);
        }

        private sealed class FakeInventoryTransactions : IInventoryTransactions
        {
            public InventoryFailureReason Failure;
            public int AddCalls;
            public InventoryId LastInventoryId;
            public InventoryTransactionResult TryAdd(InventoryId inventoryId, ItemRef item, int quantity)
            {
                AddCalls++;
                LastInventoryId = inventoryId;
                return Result(InventoryMutationKind.Add);
            }
            public InventoryTransactionResult TryRemove(InventoryId inventoryId, ItemRef item, int quantity) => Result(InventoryMutationKind.Remove);
            public InventoryTransactionResult TryTransfer(InventoryId sourceInventoryId, InventoryId destinationInventoryId, ItemRef item, int quantity) => Result(InventoryMutationKind.Transfer);
            public int Count(InventoryId inventoryId, ItemRef item) => 0;
            public IReadOnlyList<InventoryQuantitySnapshot> Capture() => Array.Empty<InventoryQuantitySnapshot>();
            public bool TryRestore(IReadOnlyList<InventoryQuantitySnapshot> snapshots) => true;
            private InventoryTransactionResult Result(InventoryMutationKind kind)
            {
                if (Failure != InventoryFailureReason.None) return InventoryTransactionResult.Reject(kind, Failure);
                return new InventoryTransactionResult(default, kind, InventoryFailureReason.None,
                    false, default, false, default, Array.Empty<InventoryChangeEvent>());
            }
        }
    }
}
