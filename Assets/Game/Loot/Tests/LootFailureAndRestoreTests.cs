using System.Collections.Generic;
using Game.Characters.Api;
using Game.Inventory.Api;
using Game.Inventory.Runtime;
using Game.Loot.Api;
using Game.Loot.Runtime;
using Game.WorldObjects.Api;
using NUnit.Framework;

namespace Game.Loot.Tests
{
    public sealed class LootFailureAndRestoreTests
    {
        [Test]
        public void FullDestinationRejection_LeavesWorldPayloadAvailable()
        {
            var item = new ItemRef("fixture.full.item");
            var objectId = new WorldObjectId("fixture.full.loot");
            var inventory = new RejectingInventoryTransactions(InventoryTransactionFailure.DestinationRejected);
            var runtime = new LootRuntime(inventory, new AcceptingInteractionValidator());
            Assert.That(runtime.TryBind(objectId, new LootPayload(item, 2)), Is.True);

            var result = runtime.TryPickup(new PickupRequest(
                new CharacterId("actor:full"), objectId, new InventoryId("inventory.full")));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.InventoryFailure, Is.EqualTo(InventoryTransactionFailure.DestinationRejected));
            var snapshot = runtime.Capture()[0];
            Assert.That(snapshot.Availability, Is.EqualTo(LootAvailability.Available));
            Assert.That(snapshot.Payload.Quantity, Is.EqualTo(2));
        }

        [Test]
        public void InventoryAndLootSnapshots_RestoreContainerCurrentTruth()
        {
            var item = new ItemRef("fixture.restore.item");
            var actorInventory = new InventoryId("restore.actor");
            var containerInventory = new InventoryId("restore.container");
            var inventory = new InventoryTransactionsRuntime(new[] { new ItemDefinition(item, "Restore Item") });
            inventory.Register(actorInventory);
            inventory.Register(containerInventory);
            Assert.That(inventory.TryAdd(actorInventory, item, 7).Succeeded, Is.True);
            Assert.That(inventory.TryTransfer(actorInventory, containerInventory, item, 3).Succeeded, Is.True);

            var loot = new LootRuntime(inventory, new AcceptingInteractionValidator());
            var worldObject = new WorldObjectId("restore.world.loot");
            Assert.That(loot.TryBind(worldObject, new LootPayload(item, 2)), Is.True);

            var inventorySnapshot = inventory.Capture();
            var lootSnapshot = loot.Capture();

            Assert.That(inventory.TryTransfer(containerInventory, actorInventory, item, 3).Succeeded, Is.True);
            Assert.That(loot.TryPickup(new PickupRequest(new CharacterId("actor:restore"), worldObject, actorInventory)).Succeeded, Is.True);

            Assert.That(inventory.TryRestore(inventorySnapshot), Is.True);
            Assert.That(loot.TryRestore(lootSnapshot), Is.True);

            Assert.That(inventory.Count(actorInventory, item), Is.EqualTo(4));
            Assert.That(inventory.Count(containerInventory, item), Is.EqualTo(3));
            Assert.That(loot.Capture()[0].Availability, Is.EqualTo(LootAvailability.Available));
        }

        private sealed class AcceptingInteractionValidator : IWorldInteractionValidator
        {
            public WorldInteractionResult Validate(CharacterId actorId, WorldObjectId objectId) =>
                WorldInteractionResult.Success();
        }

        private sealed class RejectingInventoryTransactions : IInventoryTransactions
        {
            private readonly InventoryTransactionFailure _failure;

            public RejectingInventoryTransactions(InventoryTransactionFailure failure)
            {
                _failure = failure;
            }

            public InventoryTransactionResult TryAdd(InventoryId inventoryId, ItemRef item, int quantity) =>
                InventoryTransactionResult.Reject(_failure);

            public InventoryTransactionResult TryRemove(InventoryId inventoryId, ItemRef item, int quantity) =>
                InventoryTransactionResult.Reject(_failure);

            public InventoryTransactionResult TryTransfer(InventoryId sourceInventoryId, InventoryId destinationInventoryId, ItemRef item, int quantity) =>
                InventoryTransactionResult.Reject(_failure);

            public int Count(InventoryId inventoryId, ItemRef item) => 0;
            public IReadOnlyList<InventoryQuantitySnapshot> Capture() => new InventoryQuantitySnapshot[0];
            public bool TryRestore(IReadOnlyList<InventoryQuantitySnapshot> snapshots) => true;
        }
    }
}
