using System;
using System.Collections.Generic;

namespace Game.WorldBuilder.Api
{
    public enum SecretScope
    {
        ExplorableSites = 0
    }

    public enum SecretEntranceType
    {
        DestroyableFalseWall = 0
    }

    public enum ContainerArchetype
    {
        TreasureChest = 0,
        Crate = 1,
        Satchel = 2,
        Sarcophagus = 3,
        Corpse = 4,
        Pedestal = 5
    }

    public readonly struct SecretDistribution
    {
        public int MinimumPerEligibleSite { get; }
        public int MaximumPerEligibleSite { get; }
        public int ProbabilityBasisPoints { get; }

        public SecretDistribution(int minimumPerEligibleSite, int maximumPerEligibleSite, int probabilityBasisPoints)
        {
            if (minimumPerEligibleSite < 0) throw new ArgumentOutOfRangeException(nameof(minimumPerEligibleSite));
            if (maximumPerEligibleSite < minimumPerEligibleSite) throw new ArgumentOutOfRangeException(nameof(maximumPerEligibleSite));
            if (probabilityBasisPoints < 0 || probabilityBasisPoints > 10000) throw new ArgumentOutOfRangeException(nameof(probabilityBasisPoints));

            MinimumPerEligibleSite = minimumPerEligibleSite;
            MaximumPerEligibleSite = maximumPerEligibleSite;
            ProbabilityBasisPoints = probabilityBasisPoints;
        }
    }

    public sealed class SecretPolicySpec
    {
        public SecretPolicyRef Ref { get; }
        public SecretScope Scope { get; }
        public IReadOnlyList<SecretEntranceType> EntranceTypes { get; }
        public SecretDistribution Distribution { get; }
        public bool RequiresHiddenSpace { get; }
        public ContainerArchetype Container { get; }
        public LootTableRef Reward { get; }

        internal SecretPolicySpec(
            SecretPolicyRef @ref,
            SecretScope scope,
            SecretEntranceType[] entranceTypes,
            SecretDistribution distribution,
            bool requiresHiddenSpace,
            ContainerArchetype container,
            LootTableRef reward)
        {
            Ref = @ref;
            Scope = scope;
            EntranceTypes = entranceTypes ?? Array.Empty<SecretEntranceType>();
            Distribution = distribution;
            RequiresHiddenSpace = requiresHiddenSpace;
            Container = container;
            Reward = reward;
        }
    }

    public enum LootCategory
    {
        Currency = 0,
        Consumable = 1,
        CraftingMaterial = 2,
        Equipment = 3,
        RareEquipment = 4
    }

    /// <summary>
    /// Stable gameplay item identity. The inventory/item subsystem owns the item definition; the
    /// world blueprint only states that a loot result must reference this identity.
    /// </summary>
    public readonly struct LootItemId : IEquatable<LootItemId>
    {
        public string Id { get; }
        public LootItemId(string id) => Id = WorldIdRules.Require(id, nameof(id));
        public bool Equals(LootItemId other) => string.Equals(Id, other.Id, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is LootItemId other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Id ?? string.Empty);
        public override string ToString() => Id ?? string.Empty;
    }

    public readonly struct LootQuantityRange
    {
        public int Minimum { get; }
        public int Maximum { get; }

        public LootQuantityRange(int minimum, int maximum)
        {
            if (minimum < 1) throw new ArgumentOutOfRangeException(nameof(minimum));
            if (maximum < minimum) throw new ArgumentOutOfRangeException(nameof(maximum));
            Minimum = minimum;
            Maximum = maximum;
        }

        public static LootQuantityRange Exactly(int quantity) => new LootQuantityRange(quantity, quantity);
    }

    public readonly struct WeightedLootCategory
    {
        public LootCategory Category { get; }
        public int Weight { get; }

        public WeightedLootCategory(LootCategory category, int weight)
        {
            if (weight < 1) throw new ArgumentOutOfRangeException(nameof(weight));
            Category = category;
            Weight = weight;
        }
    }

    public readonly struct GuaranteedLootItem
    {
        public LootItemId Item { get; }
        public LootQuantityRange Quantity { get; }

        public GuaranteedLootItem(LootItemId item, LootQuantityRange quantity)
        {
            Item = item;
            Quantity = quantity;
        }
    }

    public readonly struct WeightedLootItem
    {
        public LootItemId Item { get; }
        public int Weight { get; }
        public LootQuantityRange Quantity { get; }

        public WeightedLootItem(LootItemId item, int weight, LootQuantityRange quantity)
        {
            if (weight < 1) throw new ArgumentOutOfRangeException(nameof(weight));
            Item = item;
            Weight = weight;
            Quantity = quantity;
        }
    }

    public sealed class LootTableSpec
    {
        public LootTableRef Ref { get; }
        public int MinimumRolls { get; }
        public int MaximumRolls { get; }
        public IReadOnlyList<LootCategory> GuaranteedCategories { get; }
        public IReadOnlyList<GuaranteedLootItem> GuaranteedItems { get; }
        public IReadOnlyList<WeightedLootCategory> WeightedCategories { get; }
        public IReadOnlyList<WeightedLootItem> WeightedItems { get; }

        internal LootTableSpec(
            LootTableRef @ref,
            int minimumRolls,
            int maximumRolls,
            LootCategory[] guaranteedCategories,
            GuaranteedLootItem[] guaranteedItems,
            WeightedLootCategory[] weightedCategories,
            WeightedLootItem[] weightedItems)
        {
            Ref = @ref;
            MinimumRolls = minimumRolls;
            MaximumRolls = maximumRolls;
            GuaranteedCategories = guaranteedCategories ?? Array.Empty<LootCategory>();
            GuaranteedItems = guaranteedItems ?? Array.Empty<GuaranteedLootItem>();
            WeightedCategories = weightedCategories ?? Array.Empty<WeightedLootCategory>();
            WeightedItems = weightedItems ?? Array.Empty<WeightedLootItem>();
        }
    }
}
