using Game.Composition.Kentridge.Api;
using Game.Composition.Kentridge.Runtime;
using Game.Input.Api;
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
            var inventory = new InventoryRuntime(new[]
            {
                new ItemDefinition(reward, "Well Rescue Token", "W")
            });
            var rewardRuntime = new KentridgeWellQuestRewardRuntime(inventory);
            Assert.That(rewardRuntime.Synchronize(quests.IsCompleted(KentridgeWellQuestDefinition.Ref)), Is.True);
            Assert.That(rewardRuntime.Synchronize(quests.IsCompleted(KentridgeWellQuestDefinition.Ref)), Is.False,
                "Replaying completion/reward synchronization must not duplicate the quest item.");
            Assert.That(inventory.Count(reward), Is.EqualTo(1));
            Assert.That(inventory.Snapshot().Count, Is.EqualTo(1));
            Assert.That(inventory.Snapshot()[0].Definition.Ref, Is.EqualTo(reward));

            var host = new GameObject("inventory-view-test");
            try
            {
                var presentation = host.AddComponent<KentridgeWellQuestInventoryPresentation>();
                presentation.SetInventory(inventory);
                Assert.That(presentation.InventoryOpen, Is.False);
                Assert.That(presentation.ActiveInputContext, Is.EqualTo(InputContextId.Exploration));
                presentation.ToggleInventory();
                Assert.That(presentation.InventoryOpen, Is.True);
                Assert.That(presentation.ActiveInputContext, Is.EqualTo(InputContextId.Ui));
                Assert.That(presentation.VisibleTileCount, Is.EqualTo(1));
                Assert.That(KentridgeWellQuestInventoryPresentation.ItemTileSizePixels, Is.EqualTo(64f));
                presentation.ToggleInventory();
                Assert.That(presentation.InventoryOpen, Is.False);
                Assert.That(presentation.ActiveInputContext, Is.EqualTo(InputContextId.Exploration));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }
    }
}
