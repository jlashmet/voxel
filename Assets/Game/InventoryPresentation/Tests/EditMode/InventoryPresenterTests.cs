using System;
using Game.Characters.Api;
using Game.Input.Api;
using Game.Input.Runtime;
using Game.Inventory.Api;
using Game.Inventory.Runtime;
using Game.InventoryPresentation.Api;
using Game.InventoryPresentation.Runtime;
using Game.Loot.Api;
using Game.Loot.Runtime;
using Game.WorldObjects.Api;
using NUnit.Framework;

namespace Game.InventoryPresentation.Tests
{
    public sealed class InventoryPresenterTests
    {
        [Test]
        public void Capture_ProjectsAuthoritativeRowsAndKeepsSemanticSelectionAcrossRevision()
        {
            Fixture f = new Fixture();
            f.Seed(f.Personal, f.Apple, 4);
            InventoryPresentationSnapshot first = f.Presenter.Capture();
            Assert.That(RowQuantity(first, f.Personal, f.Apple), Is.EqualTo(4));
            Assert.That(Panel(first, f.Chest).Rows.Count, Is.EqualTo(0));

            Assert.That(f.Presenter.Select(new InventoryRowKey(f.Personal, f.Apple)), Is.True);
            f.Seed(f.Personal, f.Gem, 2);
            f.Presenter.SetFilter(f.Personal, "ap");
            f.Presenter.SetSort(f.Personal, InventorySortMode.Quantity, false);

            InventoryPanelPresentation panel = Panel(f.Presenter.Capture(), f.Personal);
            Assert.That(panel.Revision, Is.GreaterThan(first.Panels[0].Revision));
            Assert.That(panel.HasSelection, Is.True);
            Assert.That(panel.Selection.Item, Is.EqualTo(f.Apple));
            Assert.That(panel.Rows.Count, Is.EqualTo(1));
            Assert.That(panel.Rows[0].Key, Is.EqualTo(new InventoryRowKey(f.Personal, f.Apple)));
        }

        [Test]
        public void QueuedTransfer_IsPendingWithoutSpeculation_ThenRefreshesFromAuthority()
        {
            Fixture f = new Fixture();
            f.Seed(f.Personal, f.Apple, 5);
            PendingOperationId operation = f.Presenter.QueueTransfer(new InventoryTransferIntent(
                new ContainerTransferRequest(f.Actor, f.ContainerObject, f.Personal, f.Chest, f.Apple, 2)));

            InventoryPresentationSnapshot pending = f.Presenter.Capture();
            Assert.That(RowQuantity(pending, f.Personal, f.Apple), Is.EqualTo(5));
            Assert.That(RowQuantity(pending, f.Chest, f.Apple), Is.EqualTo(0));
            Assert.That(Operation(pending, operation).Status, Is.EqualTo(PendingOperationStatus.Pending));

            Assert.That(f.Presenter.Execute(operation), Is.True);
            InventoryPresentationSnapshot committed = f.Presenter.Capture();
            Assert.That(RowQuantity(committed, f.Personal, f.Apple), Is.EqualTo(3));
            Assert.That(RowQuantity(committed, f.Chest, f.Apple), Is.EqualTo(2));
            Assert.That(Operation(committed, operation).Status, Is.EqualTo(PendingOperationStatus.Succeeded));
        }

        [Test]
        public void CompetingTransfers_LoserRejectsAndConvergesWithoutSpeculativeResidue()
        {
            Fixture f = new Fixture();
            f.Seed(f.Personal, f.Apple, 5);
            var other = new InventoryPresenter(f.Inventory, f.Loot, f.Input);
            other.ShowInventories(new[] { f.Personal, f.Chest });

            PendingOperationId first = f.Presenter.QueueTransfer(new InventoryTransferIntent(
                new ContainerTransferRequest(f.Actor, f.ContainerObject, f.Personal, f.Chest, f.Apple, 4)));
            PendingOperationId second = other.QueueTransfer(new InventoryTransferIntent(
                new ContainerTransferRequest(f.Actor, f.ContainerObject, f.Personal, f.Chest, f.Apple, 4)));

            Assert.That(f.Presenter.Execute(first), Is.True);
            Assert.That(other.Execute(second), Is.False);
            InventoryPresentationSnapshot loser = other.Capture();
            Assert.That(RowQuantity(loser, f.Personal, f.Apple), Is.EqualTo(1));
            Assert.That(RowQuantity(loser, f.Chest, f.Apple), Is.EqualTo(4));
            Assert.That(Operation(loser, second).Status, Is.EqualTo(PendingOperationStatus.Rejected));
            Assert.That(Operation(loser, second).Error, Does.Contain("InsufficientQuantity"));
        }

        [Test]
        public void QueuedDrop_DoesNotChangeQuantityUntilLootAuthorityCreatesWorldItem()
        {
            Fixture f = new Fixture();
            f.Seed(f.Personal, f.Gem, 3);
            WorldObjectId dropped = new WorldObjectId("loot:dropped-gem");
            PendingOperationId operation = f.Presenter.QueueDrop(new InventoryDropIntent(
                new DropRequest(f.Actor, f.ContainerObject, dropped, f.Personal, new LootPayload(f.Gem, 2))));

            Assert.That(RowQuantity(f.Presenter.Capture(), f.Personal, f.Gem), Is.EqualTo(3));
            Assert.That(f.Loot.Capture().Count, Is.EqualTo(0));
            Assert.That(f.Presenter.Execute(operation), Is.True);
            Assert.That(RowQuantity(f.Presenter.Capture(), f.Personal, f.Gem), Is.EqualTo(1));
            Assert.That(f.Loot.Capture().Count, Is.EqualTo(1));
            Assert.That(f.Loot.Capture()[0].ObjectId, Is.EqualTo(dropped));
        }

        [Test]
        public void UiContext_NestedInventoryScopesUnwindToPriorGameplayContext()
        {
            Fixture f = new Fixture();
            Assert.That(f.Input.ActiveContext, Is.EqualTo(InputContextId.Exploration));
            IInputContextLease outer = f.Presenter.OpenUi();
            Assert.That(f.Input.ActiveContext, Is.EqualTo(InputContextId.Ui));
            IInputContextLease inner = f.Presenter.OpenUi();
            Assert.That(f.Input.ActiveContext, Is.EqualTo(InputContextId.Ui));
            inner.Dispose();
            Assert.That(f.Input.ActiveContext, Is.EqualTo(InputContextId.Ui));
            outer.Dispose();
            Assert.That(f.Input.ActiveContext, Is.EqualTo(InputContextId.Exploration));
        }

        [Test]
        public void Rebuild_DropsStalePendingAndReprojectsRestoredTruth()
        {
            Fixture f = new Fixture();
            f.Seed(f.Personal, f.Apple, 5);
            f.Presenter.Select(new InventoryRowKey(f.Personal, f.Apple));
            f.Presenter.QueueTransfer(new InventoryTransferIntent(
                new ContainerTransferRequest(f.Actor, f.ContainerObject, f.Personal, f.Chest, f.Apple, 2)));

            InventoryTransactionResult removed = f.Inventory.Remove(new InventoryRemoveRequest(
                new InventoryTransactionId("external:restore"), f.Personal, f.Apple, 5));
            Assert.That(removed.Succeeded, Is.True);
            f.Presenter.RebuildFromAuthoritative();

            InventoryPresentationSnapshot rebuilt = f.Presenter.Capture();
            Assert.That(rebuilt.Operations.Count, Is.EqualTo(0));
            Assert.That(RowQuantity(rebuilt, f.Personal, f.Apple), Is.EqualTo(0));
            Assert.That(Panel(rebuilt, f.Personal).HasSelection, Is.False);
        }

        [Test]
        public void RecreatingPresenter_CannotLoseOrInventAuthoritativeInventoryTruth()
        {
            Fixture f = new Fixture();
            f.Seed(f.Personal, f.Apple, 6);
            PendingOperationId transfer = f.Presenter.QueueTransfer(new InventoryTransferIntent(
                new ContainerTransferRequest(f.Actor, f.ContainerObject, f.Personal, f.Chest, f.Apple, 2)));
            Assert.That(f.Presenter.Execute(transfer), Is.True);

            var recreated = new InventoryPresenter(f.Inventory, f.Loot, f.Input);
            recreated.ShowInventories(new[] { f.Personal, f.Chest });
            InventoryPresentationSnapshot snapshot = recreated.Capture();
            Assert.That(RowQuantity(snapshot, f.Personal, f.Apple), Is.EqualTo(4));
            Assert.That(RowQuantity(snapshot, f.Chest, f.Apple), Is.EqualTo(2));
            Assert.That(snapshot.Operations.Count, Is.EqualTo(0));
        }

        private static InventoryPanelPresentation Panel(InventoryPresentationSnapshot snapshot, InventoryId inventoryId)
        {
            for (var i = 0; i < snapshot.Panels.Count; i++)
                if (snapshot.Panels[i].InventoryId == inventoryId) return snapshot.Panels[i];
            Assert.Fail("Missing inventory panel " + inventoryId);
            return default;
        }

        private static int RowQuantity(InventoryPresentationSnapshot snapshot, InventoryId inventoryId, ItemRef item)
        {
            InventoryPanelPresentation panel = Panel(snapshot, inventoryId);
            for (var i = 0; i < panel.Rows.Count; i++)
                if (panel.Rows[i].Key.Item == item) return panel.Rows[i].Quantity;
            return 0;
        }

        private static PendingOperationPresentation Operation(InventoryPresentationSnapshot snapshot, PendingOperationId id)
        {
            for (var i = 0; i < snapshot.Operations.Count; i++)
                if (snapshot.Operations[i].Id.Equals(id)) return snapshot.Operations[i];
            Assert.Fail("Missing pending operation " + id);
            return default;
        }

        private sealed class Fixture
        {
            private int _seedSequence;
            public ItemRef Apple { get; } = new ItemRef("item:apple");
            public ItemRef Gem { get; } = new ItemRef("item:gem");
            public InventoryId Personal { get; } = new InventoryId("inventory:character");
            public InventoryId Chest { get; } = new InventoryId("inventory:chest");
            public CharacterId Actor { get; } = new CharacterId("character:tester");
            public WorldObjectId ContainerObject { get; } = new WorldObjectId("container:test");
            public InventoryRuntime Inventory { get; }
            public LootRuntime Loot { get; }
            public InputContextService Input { get; }
            public InventoryPresenter Presenter { get; }

            public Fixture()
            {
                Inventory = new InventoryRuntime(
                    new[]
                    {
                        new ItemDefinition(Apple, "Apple", "A"),
                        new ItemDefinition(Gem, "Moon Gem", "G")
                    },
                    new[]
                    {
                        new InventoryDescriptor(Personal, new InventoryBindingMetadata("character", "character:tester")),
                        new InventoryDescriptor(Chest, new InventoryBindingMetadata("container", "container:test"))
                    });
                var transactions = new InventoryTransactionsAdapter(Inventory, Inventory, Inventory);
                Loot = new LootRuntime(transactions, new AllowAllInteractions());
                Input = new InputContextService();
                Presenter = new InventoryPresenter(Inventory, Loot, Input);
                Presenter.ShowInventories(new[] { Personal, Chest });
            }

            public void Seed(InventoryId inventoryId, ItemRef item, int quantity)
            {
                _seedSequence++;
                InventoryTransactionResult result = Inventory.Add(new InventoryAddRequest(
                    new InventoryTransactionId("seed:" + _seedSequence), inventoryId, item, quantity));
                Assert.That(result.Succeeded, Is.True);
            }
        }

        private sealed class AllowAllInteractions : IWorldInteractionValidator
        {
            public WorldInteractionResult Validate(CharacterId actorId, WorldObjectId objectId) => WorldInteractionResult.Success();
        }
    }
}
