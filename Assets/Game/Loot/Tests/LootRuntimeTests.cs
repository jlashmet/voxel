using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Game.Characters.Api;
using Game.Inventory.Api;
using Game.Inventory.Runtime;
using Game.Loot.Api;
using Game.Loot.Runtime;
using Game.WorldObjects.Api;
using NUnit.Framework;

namespace Game.Loot.Tests
{
    public sealed class LootRuntimeTests
    {
        private ItemRef _item;
        private InventoryId _actorInventory;
        private InventoryId _containerInventory;
        private InventoryTransactionsRuntime _inventory;
        private TestInteractionValidator _validator;
        private LootRuntime _loot;

        [SetUp]
        public void SetUp()
        {
            _item = new ItemRef("fixture.apple");
            _actorInventory = new InventoryId("inventory.actor");
            _containerInventory = new InventoryId("inventory.container");
            _inventory = new InventoryTransactionsRuntime(new[]
            {
                new ItemDefinition(_item, "Fixture Apple")
            });
            _inventory.Register(_actorInventory);
            _inventory.Register(_containerInventory);
            _validator = new TestInteractionValidator();
            _loot = new LootRuntime(_inventory, _validator);
        }

        [Test]
        public void TwoActorPickupRace_CommitsExactlyOnceAndConservesQuantity()
        {
            var objectId = new WorldObjectId("fixture.race.loot");
            Assert.That(_loot.TryBind(objectId, new LootPayload(_item, 1)), Is.True);

            var actorA = new CharacterId("actor:a");
            var actorB = new CharacterId("actor:b");
            var start = new ManualResetEventSlim(false);
            LootTransferResult resultA = default;
            LootTransferResult resultB = default;

            var taskA = Task.Run(() =>
            {
                start.Wait();
                resultA = _loot.TryPickup(new PickupRequest(actorA, objectId, _actorInventory));
            });
            var taskB = Task.Run(() =>
            {
                start.Wait();
                resultB = _loot.TryPickup(new PickupRequest(actorB, objectId, _actorInventory));
            });

            start.Set();
            Task.WaitAll(taskA, taskB);

            Assert.That((resultA.Succeeded ? 1 : 0) + (resultB.Succeeded ? 1 : 0), Is.EqualTo(1));
            Assert.That(_inventory.Count(_actorInventory, _item), Is.EqualTo(1));
            var snapshots = _loot.Capture();
            Assert.That(snapshots, Has.Count.EqualTo(1));
            Assert.That(snapshots[0].Availability, Is.EqualTo(LootAvailability.Removed));
        }

        [Test]
        public void FailedPickup_LeavesWorldStateUnchanged()
        {
            var objectId = new WorldObjectId("fixture.failed.loot");
            Assert.That(_loot.TryBind(objectId, new LootPayload(_item, 2)), Is.True);

            var result = _loot.TryPickup(new PickupRequest(
                new CharacterId("actor:a"),
                objectId,
                new InventoryId("inventory.missing")));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Failure, Is.EqualTo(LootTransferFailure.InventoryRejected));
            Assert.That(result.InventoryFailure, Is.EqualTo(InventoryTransactionFailure.UnknownInventory));
            var snapshot = _loot.Capture()[0];
            Assert.That(snapshot.Availability, Is.EqualTo(LootAvailability.Available));
            Assert.That(snapshot.ClaimedBy.IsValid, Is.False);
        }

        [Test]
        public void RejectedInteraction_LeavesWorldAndInventoryUnchanged()
        {
            var objectId = new WorldObjectId("fixture.rejected.loot");
            Assert.That(_loot.TryBind(objectId, new LootPayload(_item, 1)), Is.True);
            _validator.Reject(objectId, WorldInteractionFailure.OutOfRange);

            var result = _loot.TryPickup(new PickupRequest(
                new CharacterId("actor:a"), objectId, _actorInventory));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.InteractionFailure, Is.EqualTo(WorldInteractionFailure.OutOfRange));
            Assert.That(_inventory.Count(_actorInventory, _item), Is.Zero);
            Assert.That(_loot.Capture()[0].Availability, Is.EqualTo(LootAvailability.Available));
        }

        [Test]
        public void ContainerTransfers_BothDirectionsPreserveTotalQuantity()
        {
            Assert.That(_inventory.TryAdd(_actorInventory, _item, 5).Succeeded, Is.True);
            var containerObject = new WorldObjectId("fixture.container");
            var actor = new CharacterId("actor:a");

            var intoContainer = _loot.TryContainerTransfer(new ContainerTransferRequest(
                actor, containerObject, _actorInventory, _containerInventory, _item, 3));
            var backToActor = _loot.TryContainerTransfer(new ContainerTransferRequest(
                actor, containerObject, _containerInventory, _actorInventory, _item, 2));

            Assert.That(intoContainer.Succeeded, Is.True);
            Assert.That(backToActor.Succeeded, Is.True);
            Assert.That(_inventory.Count(_actorInventory, _item), Is.EqualTo(4));
            Assert.That(_inventory.Count(_containerInventory, _item), Is.EqualTo(1));
            Assert.That(_inventory.Count(_actorInventory, _item) + _inventory.Count(_containerInventory, _item), Is.EqualTo(5));
        }

        [Test]
        public void CompetingContainerTransfers_CommitAtMostAvailableQuantityAndConserveTotal()
        {
            Assert.That(_inventory.TryAdd(_actorInventory, _item, 1).Succeeded, Is.True);
            var containerObject = new WorldObjectId("fixture.container.race");
            var actorA = new CharacterId("actor:a");
            var actorB = new CharacterId("actor:b");
            var start = new ManualResetEventSlim(false);
            LootTransferResult resultA = default;
            LootTransferResult resultB = default;

            var taskA = Task.Run(() =>
            {
                start.Wait();
                resultA = _loot.TryContainerTransfer(new ContainerTransferRequest(
                    actorA, containerObject, _actorInventory, _containerInventory, _item, 1));
            });
            var taskB = Task.Run(() =>
            {
                start.Wait();
                resultB = _loot.TryContainerTransfer(new ContainerTransferRequest(
                    actorB, containerObject, _actorInventory, _containerInventory, _item, 1));
            });

            start.Set();
            Task.WaitAll(taskA, taskB);

            Assert.That((resultA.Succeeded ? 1 : 0) + (resultB.Succeeded ? 1 : 0), Is.EqualTo(1));
            Assert.That(_inventory.Count(_actorInventory, _item), Is.Zero);
            Assert.That(_inventory.Count(_containerInventory, _item), Is.EqualTo(1));
            Assert.That(_inventory.Count(_actorInventory, _item) + _inventory.Count(_containerInventory, _item), Is.EqualTo(1));
        }

        [Test]
        public void DropThenPickup_RoundTripsPayloadAndQuantity()
        {
            Assert.That(_inventory.TryAdd(_actorInventory, _item, 4).Succeeded, Is.True);
            var actor = new CharacterId("actor:a");
            var context = new WorldObjectId("fixture.drop.context");
            var droppedObject = new WorldObjectId("fixture.drop.loot");
            var payload = new LootPayload(_item, 3);

            var drop = _loot.TryDrop(new DropRequest(actor, context, droppedObject, _actorInventory, payload));
            Assert.That(drop.Succeeded, Is.True);
            Assert.That(_inventory.Count(_actorInventory, _item), Is.EqualTo(1));
            Assert.That(_loot.Capture()[0].Availability, Is.EqualTo(LootAvailability.Available));

            var pickup = _loot.TryPickup(new PickupRequest(actor, droppedObject, _actorInventory));
            Assert.That(pickup.Succeeded, Is.True);
            Assert.That(_inventory.Count(_actorInventory, _item), Is.EqualTo(4));
            Assert.That(pickup.Fact.Payload.Equals(payload), Is.True);
        }

        [Test]
        public void Restore_ReconstructsAvailableClaimedAndRemovedTruthWithoutDuplicates()
        {
            var snapshots = new[]
            {
                new LootStateSnapshot(new WorldObjectId("fixture.available"), new LootPayload(_item, 1), LootAvailability.Available),
                new LootStateSnapshot(new WorldObjectId("fixture.claimed"), new LootPayload(_item, 2), LootAvailability.Claimed, new CharacterId("actor:a")),
                new LootStateSnapshot(new WorldObjectId("fixture.removed"), new LootPayload(_item, 3), LootAvailability.Removed, new CharacterId("actor:b"))
            };

            Assert.That(_loot.TryRestore(snapshots), Is.True);
            var restored = _loot.Capture();
            Assert.That(restored, Has.Count.EqualTo(3));
            Assert.That(restored[0].ObjectId.Value, Is.EqualTo("fixture.available"));
            Assert.That(restored[1].Availability, Is.EqualTo(LootAvailability.Claimed));
            Assert.That(restored[2].Availability, Is.EqualTo(LootAvailability.Removed));

            var duplicate = new[] { snapshots[0], snapshots[0] };
            Assert.That(_loot.TryRestore(duplicate), Is.False);
            Assert.That(_loot.Capture(), Has.Count.EqualTo(3));
        }

        [Test]
        public void HarborFixture_UsesSameRuntimeWithoutSceneSpecificPolicy()
        {
            var harborCrate = new WorldObjectId("harbor.crate.loot");
            var harborActor = new CharacterId("harbor:porter");
            Assert.That(_loot.TryBind(harborCrate, new LootPayload(_item, 2)), Is.True);

            var result = _loot.TryPickup(new PickupRequest(harborActor, harborCrate, _actorInventory));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(_inventory.Count(_actorInventory, _item), Is.EqualTo(2));
            Assert.That(result.Fact.ObjectId, Is.EqualTo(harborCrate));
        }

        private sealed class TestInteractionValidator : IWorldInteractionValidator
        {
            private readonly Dictionary<WorldObjectId, WorldInteractionFailure> _rejections =
                new Dictionary<WorldObjectId, WorldInteractionFailure>();

            public void Reject(WorldObjectId objectId, WorldInteractionFailure failure)
            {
                _rejections[objectId] = failure;
            }

            public WorldInteractionResult Validate(CharacterId actorId, WorldObjectId objectId)
            {
                WorldInteractionFailure failure;
                return _rejections.TryGetValue(objectId, out failure)
                    ? WorldInteractionResult.Reject(failure)
                    : WorldInteractionResult.Success();
            }
        }
    }
}
