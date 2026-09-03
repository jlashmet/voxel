using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Game.Characters.Api;
using Game.Inventory.Api;
using Game.Inventory.Runtime;
using NUnit.Framework;

namespace Game.Inventory.Tests
{
    public sealed class InventoryTransactionTests
    {
        private static readonly ItemRef Coin = new ItemRef("item.currency.coin");
        private static readonly ItemRef Potion = new ItemRef("item.consumable.potion");
        private static readonly CharacterId Hero = CharacterId.FromStableKey("player", "hero");
        private static readonly InventoryId CharacterInventory = new InventoryId("inventory.hero");
        private static readonly InventoryId ContainerInventory = new InventoryId("inventory.chest-a");

        [Test]
        public void AddRemove_ValidateAndCommitDeterministically()
        {
            InventoryRuntime runtime = CreateRuntime();

            InventoryTransactionResult add = runtime.Add(new InventoryAddRequest(
                Tx("add-coin"), CharacterInventory, Coin, 5));
            Assert.That(add.Succeeded, Is.True);
            Assert.That(add.SourceSnapshot.Revision, Is.EqualTo(1UL));
            Assert.That(runtime.Count(CharacterInventory, Coin), Is.EqualTo(5));

            InventoryTransactionResult remove = runtime.Remove(new InventoryRemoveRequest(
                Tx("remove-coin"), CharacterInventory, Coin, 2));
            Assert.That(remove.Succeeded, Is.True);
            Assert.That(remove.SourceSnapshot.Revision, Is.EqualTo(2UL));
            Assert.That(runtime.Count(CharacterInventory, Coin), Is.EqualTo(3));

            Assert.That(runtime.Remove(new InventoryRemoveRequest(
                Tx("remove-too-many"), CharacterInventory, Coin, 4)).FailureReason,
                Is.EqualTo(InventoryFailureReason.InsufficientQuantity));
            Assert.That(runtime.Count(CharacterInventory, Coin), Is.EqualTo(3));
            Assert.That(Snapshot(runtime, CharacterInventory).Revision, Is.EqualTo(2UL));

            Assert.That(runtime.Add(new InventoryAddRequest(
                Tx("bad-amount"), CharacterInventory, Coin, 0)).FailureReason,
                Is.EqualTo(InventoryFailureReason.InvalidQuantity));
            Assert.That(runtime.Add(new InventoryAddRequest(
                Tx("bad-inventory"), default, Coin, 1)).FailureReason,
                Is.EqualTo(InventoryFailureReason.InvalidInventoryId));
            Assert.That(runtime.Add(new InventoryAddRequest(
                Tx("unknown-inventory"), new InventoryId("inventory.missing"), Coin, 1)).FailureReason,
                Is.EqualTo(InventoryFailureReason.UnknownInventory));
            Assert.That(runtime.Add(new InventoryAddRequest(
                Tx("unknown-item"), CharacterInventory, new ItemRef("item.missing"), 1)).FailureReason,
                Is.EqualTo(InventoryFailureReason.UnknownItem));
        }

        [Test]
        public void Transfer_IsAtomicAndConservesQuantity()
        {
            InventoryRuntime runtime = CreateRuntime();
            runtime.Add(new InventoryAddRequest(Tx("seed"), CharacterInventory, Coin, 7));

            InventorySnapshot beforeSource = Snapshot(runtime, CharacterInventory);
            InventorySnapshot beforeDestination = Snapshot(runtime, ContainerInventory);
            InventoryTransactionResult failed = runtime.Transfer(new InventoryTransferRequest(
                Tx("too-many"), CharacterInventory, ContainerInventory, Coin, 8));

            Assert.That(failed.FailureReason, Is.EqualTo(InventoryFailureReason.InsufficientQuantity));
            Assert.That(Snapshot(runtime, CharacterInventory).Revision, Is.EqualTo(beforeSource.Revision));
            Assert.That(Snapshot(runtime, ContainerInventory).Revision, Is.EqualTo(beforeDestination.Revision));
            Assert.That(runtime.Count(CharacterInventory, Coin), Is.EqualTo(7));
            Assert.That(runtime.Count(ContainerInventory, Coin), Is.Zero);

            var changes = new List<InventoryChangeEvent>();
            runtime.Changed += changes.Add;
            InventoryTransactionResult transferred = runtime.Transfer(new InventoryTransferRequest(
                Tx("move-four"), CharacterInventory, ContainerInventory, Coin, 4));

            Assert.That(transferred.Succeeded, Is.True);
            Assert.That(runtime.Count(CharacterInventory, Coin), Is.EqualTo(3));
            Assert.That(runtime.Count(ContainerInventory, Coin), Is.EqualTo(4));
            Assert.That(runtime.Count(CharacterInventory, Coin) + runtime.Count(ContainerInventory, Coin), Is.EqualTo(7));
            Assert.That(changes.Count, Is.EqualTo(2));
            Assert.That(changes[0].QuantityDelta, Is.EqualTo(-4));
            Assert.That(changes[1].QuantityDelta, Is.EqualTo(4));
        }

        [Test]
        public void DuplicateAndCompetingRequests_DoNotDuplicateOrGoNegative()
        {
            InventoryRuntime runtime = CreateRuntime();
            InventoryAddRequest request = new InventoryAddRequest(Tx("same-add"), CharacterInventory, Coin, 2);

            InventoryTransactionResult first = runtime.Add(request);
            InventoryTransactionResult duplicate = runtime.Add(request);
            Assert.That(duplicate, Is.SameAs(first));
            Assert.That(runtime.Count(CharacterInventory, Coin), Is.EqualTo(2));

            InventoryTransactionResult conflict = runtime.Add(new InventoryAddRequest(
                Tx("same-add"), CharacterInventory, Coin, 3));
            Assert.That(conflict.FailureReason, Is.EqualTo(InventoryFailureReason.TransactionConflict));
            Assert.That(runtime.Count(CharacterInventory, Coin), Is.EqualTo(2));

            runtime.Add(new InventoryAddRequest(Tx("race-seed"), CharacterInventory, Potion, 5));
            var results = new InventoryTransactionResult[2];
            Task firstRemove = Task.Run(() => results[0] = runtime.Remove(new InventoryRemoveRequest(
                Tx("race-a"), CharacterInventory, Potion, 4)));
            Task secondRemove = Task.Run(() => results[1] = runtime.Remove(new InventoryRemoveRequest(
                Tx("race-b"), CharacterInventory, Potion, 4)));
            Task.WaitAll(firstRemove, secondRemove);

            int successCount = (results[0].Succeeded ? 1 : 0) + (results[1].Succeeded ? 1 : 0);
            Assert.That(successCount, Is.EqualTo(1));
            Assert.That(runtime.Count(CharacterInventory, Potion), Is.EqualTo(1));
            Assert.That(results[0].FailureReason == InventoryFailureReason.InsufficientQuantity ||
                        results[1].FailureReason == InventoryFailureReason.InsufficientQuantity,
                Is.True);
        }

        [Test]
        public void CharacterAndContainerBindings_UseIdenticalAuthorityPath()
        {
            InventoryRuntime runtime = CreateRuntime();
            InventoryDescriptor character;
            InventoryDescriptor container;
            Assert.That(runtime.TryGetDescriptor(CharacterInventory, out character), Is.True);
            Assert.That(runtime.TryGetDescriptor(ContainerInventory, out container), Is.True);
            Assert.That(character.Binding.Kind, Is.EqualTo("character"));
            Assert.That(character.Binding.StableOwnerId, Is.EqualTo(Hero.Value));
            Assert.That(container.Binding.Kind, Is.EqualTo("container"));
            Assert.That(container.Binding.StableOwnerId, Is.EqualTo("world.chest-a"));

            Assert.That(runtime.Add(new InventoryAddRequest(
                Tx("character-grant"), CharacterInventory, Potion, 2)).Succeeded, Is.True);
            Assert.That(runtime.Add(new InventoryAddRequest(
                Tx("container-grant"), ContainerInventory, Potion, 3)).Succeeded, Is.True);
            Assert.That(runtime.Transfer(new InventoryTransferRequest(
                Tx("character-to-container"), CharacterInventory, ContainerInventory, Potion, 1)).Succeeded, Is.True);

            Assert.That(runtime.Count(CharacterInventory, Potion), Is.EqualTo(1));
            Assert.That(runtime.Count(ContainerInventory, Potion), Is.EqualTo(4));
        }

        [Test]
        public void CaptureRestore_PreservesStableOrderingIdentityRevisionAndContents()
        {
            InventoryRuntime runtime = CreateRuntime();
            runtime.Add(new InventoryAddRequest(Tx("potion-first"), ContainerInventory, Potion, 2));
            runtime.Add(new InventoryAddRequest(Tx("coin-second"), ContainerInventory, Coin, 9));
            runtime.Add(new InventoryAddRequest(Tx("hero-coin"), CharacterInventory, Coin, 1));

            InventoryStateCapture captured = runtime.CaptureState();
            string before = Stable(captured);
            InventoryRuntime restored = CreateRuntime();
            Assert.That(restored.RestoreState(captured), Is.EqualTo(InventoryFailureReason.None));
            string after = Stable(restored.CaptureState());

            Assert.That(after, Is.EqualTo(before));
            Assert.That(captured.Inventories[0].Id.CompareTo(captured.Inventories[1].Id), Is.LessThan(0));
            InventorySnapshot container = Snapshot(restored, ContainerInventory);
            Assert.That(container.Entries[0].Item.CompareTo(container.Entries[1].Item), Is.LessThan(0));
            Assert.That(container.Revision, Is.EqualTo(2UL));
        }

        private static InventoryRuntime CreateRuntime()
        {
            return new InventoryRuntime(
                new[]
                {
                    new ItemDefinition(Potion, "Potion", "P"),
                    new ItemDefinition(Coin, "Coin", "C")
                },
                new[]
                {
                    new InventoryDescriptor(
                        CharacterInventory,
                        new InventoryBindingMetadata("character", Hero.Value)),
                    new InventoryDescriptor(
                        ContainerInventory,
                        new InventoryBindingMetadata("container", "world.chest-a"))
                });
        }

        private static InventoryTransactionId Tx(string value) => new InventoryTransactionId("test:" + value);

        private static InventorySnapshot Snapshot(IInventoryQuery query, InventoryId id)
        {
            InventorySnapshot snapshot;
            Assert.That(query.TryGetSnapshot(id, out snapshot), Is.True);
            return snapshot;
        }

        private static string Stable(InventoryStateCapture capture)
        {
            var text = new StringBuilder();
            for (var i = 0; i < capture.Inventories.Count; i++)
            {
                InventorySnapshot snapshot = capture.Inventories[i];
                text.Append(snapshot.Id.Value).Append('@').Append(snapshot.Revision).Append(':');
                for (var j = 0; j < snapshot.Entries.Count; j++)
                {
                    InventoryEntry entry = snapshot.Entries[j];
                    text.Append(entry.Item.Id).Append('=').Append(entry.Quantity).Append(';');
                }
                text.Append('|');
            }
            return text.ToString();
        }
    }
}
