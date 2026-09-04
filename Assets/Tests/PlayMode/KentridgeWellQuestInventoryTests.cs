using Game.Composition.Kentridge.Api;
using Game.Composition.Kentridge.Runtime;
using Game.Inventory.Api;
using Game.Inventory.Runtime;
using Game.Kentridge.PlayableSlice;
using Game.Quests.Api;
using Game.Quests.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class KentridgeWellQuestInventoryTests
    {
        [Test]
        [Category("Gameplay")]
        public void WellQuestProgressionGrantsExactlyOneVisibleInventoryItem()
        {
            var quests = new QuestRuntime(KentridgeWellQuestDefinition.CreateDefinitions());
            quests.Start(KentridgeWellQuestDefinition.Ref);

            QuestSnapshot started = quests.GetSnapshot(KentridgeWellQuestDefinition.Ref);
            Assert.That(started.Status, Is.EqualTo(QuestStatus.Active));
            Assert.That(started.Steps[0].TargetId, Is.EqualTo(KentridgeWellQuestDefinition.WellTargetId));
            Assert.That(started.Steps[0].Status, Is.EqualTo(QuestStepStatus.Active));

            quests.Observe(QuestObservation.Interacted(KentridgeWellQuestDefinition.WellTargetId));
            QuestSnapshot rescued = quests.GetSnapshot(KentridgeWellQuestDefinition.Ref);
            Assert.That(rescued.Steps[0].Status, Is.EqualTo(QuestStepStatus.Completed));
            Assert.That(rescued.Steps[1].Status, Is.EqualTo(QuestStepStatus.Active));
            Assert.That(rescued.Steps[1].TargetId, Is.EqualTo(KentridgeWellQuestDefinition.MadelineNpcId));

            quests.Observe(QuestObservation.NpcInteracted(KentridgeWellQuestDefinition.MadelineNpcId));
            Assert.That(quests.IsCompleted(KentridgeWellQuestDefinition.Ref), Is.True);

            ItemRef reward = new ItemRef(KentridgeWellQuestDefinition.RewardItemId);
            var inventoryId = new InventoryId("inventory.test.player");
            var inventory = new InventoryRuntime(
                new[]
                {
                    new ItemDefinition(reward, "Well Rescue Token", "W")
                },
                new[]
                {
                    new InventoryDescriptor(
                        inventoryId,
                        new InventoryBindingMetadata("character", "character.test.player"))
                });
            var rewardRuntime = new KentridgeWellQuestRewardRuntime(inventory, inventory, inventoryId);
            Assert.That(rewardRuntime.Synchronize(quests.IsCompleted(KentridgeWellQuestDefinition.Ref)), Is.True);
            Assert.That(rewardRuntime.Synchronize(quests.IsCompleted(KentridgeWellQuestDefinition.Ref)), Is.False,
                "Replaying completion/reward synchronization must not duplicate the quest item.");
            Assert.That(inventory.Count(inventoryId, reward), Is.EqualTo(1));
            Assert.That(inventory.TryGetSnapshot(inventoryId, out InventorySnapshot snapshot), Is.True);
            Assert.That(snapshot.Entries.Count, Is.EqualTo(1));
            Assert.That(snapshot.Entries[0].Item, Is.EqualTo(reward));
            Assert.That(snapshot.Entries[0].Quantity, Is.EqualTo(1));

            var host = new GameObject("inventory-view-test");
            try
            {
                var presentation = host.AddComponent<KentridgeWellQuestInventoryPresentation>();
                presentation.BindReadModel(
                    inventory,
                    inventoryId,
                    () => quests.GetSnapshot(KentridgeWellQuestDefinition.Ref),
                    Vector3.zero);
                Assert.That(presentation.IsBound, Is.True);
                Assert.That(presentation.InventoryOpen, Is.False);
                presentation.ToggleInventory();
                Assert.That(presentation.InventoryOpen, Is.True);
                Assert.That(presentation.VisibleTileCount, Is.EqualTo(1));
                Assert.That(KentridgeWellQuestInventoryPresentation.ItemTileSizePixels, Is.EqualTo(64f));
                presentation.ToggleInventory();
                Assert.That(presentation.InventoryOpen, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }
    }
}
