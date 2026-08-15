using System.Linq;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class SecretLootBlueprintTests
    {
        [Test]
        public void SecretChestCanGuaranteeExactItemsAndAddProceduralLoot()
        {
            var game = Campaign.Create("loot-test");
            var requiredKey = new LootItemId("story.ancient-key");
            var mooncap = new LootItemId("ingredient.mooncap");

            LootTableRef treasure = game.Loot.Table("hidden-cache", loot => loot
                .RollCount(2, 4)
                .Guaranteed(requiredKey, 1)
                .Guaranteed(LootCategory.Currency)
                .Weighted(mooncap, weight: 5, minimumQuantity: 1, maximumQuantity: 3)
                .Weighted(LootCategory.RareEquipment, weight: 1));

            game.World.Secrets.Policy("false-wall-cache", secret => secret
                .Scope(SecretScope.ExplorableSites)
                .Entrance(SecretEntranceType.DestroyableFalseWall)
                .Distribution(new SecretDistribution(0, 2, 2500))
                .RequireHiddenSpace()
                .Container(ContainerArchetype.TreasureChest)
                .RewardWith(treasure));

            CampaignBlueprint blueprint = game.Build();
            BlueprintValidationResult validation = BlueprintValidator.Validate(blueprint);

            Assert.That(validation.IsValid, Is.True);

            LootTableSpec table = blueprint.LootTables.Single();
            Assert.That(table.GuaranteedItems.Count, Is.EqualTo(1));
            Assert.That(table.GuaranteedItems[0].Item, Is.EqualTo(requiredKey));
            Assert.That(table.GuaranteedItems[0].Quantity.Minimum, Is.EqualTo(1));
            Assert.That(table.GuaranteedItems[0].Quantity.Maximum, Is.EqualTo(1));

            WeightedLootItem weighted = table.WeightedItems.Single();
            Assert.That(weighted.Item, Is.EqualTo(mooncap));
            Assert.That(weighted.Weight, Is.EqualTo(5));
            Assert.That(weighted.Quantity.Minimum, Is.EqualTo(1));
            Assert.That(weighted.Quantity.Maximum, Is.EqualTo(3));

            SecretPolicySpec policy = blueprint.SecretPolicies.Single();
            Assert.That(policy.Container, Is.EqualTo(ContainerArchetype.TreasureChest));
            Assert.That(policy.EntranceTypes.Single(), Is.EqualTo(SecretEntranceType.DestroyableFalseWall));
            Assert.That(policy.Reward, Is.EqualTo(treasure));
        }
    }
}
