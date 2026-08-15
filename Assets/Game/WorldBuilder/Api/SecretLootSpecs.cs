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
        public LootTableRef Reward { get; }

        internal SecretPolicySpec(
            SecretPolicyRef @ref,
            SecretScope scope,
            SecretEntranceType[] entranceTypes,
            SecretDistribution distribution,
            bool requiresHiddenSpace,
            LootTableRef reward)
        {
            Ref = @ref;
            Scope = scope;
            EntranceTypes = entranceTypes ?? Array.Empty<SecretEntranceType>();
            Distribution = distribution;
            RequiresHiddenSpace = requiresHiddenSpace;
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

    public sealed class LootTableSpec
    {
        public LootTableRef Ref { get; }
        public int MinimumRolls { get; }
        public int MaximumRolls { get; }
        public IReadOnlyList<LootCategory> GuaranteedCategories { get; }
        public IReadOnlyList<WeightedLootCategory> WeightedCategories { get; }

        internal LootTableSpec(
            LootTableRef @ref,
            int minimumRolls,
            int maximumRolls,
            LootCategory[] guaranteedCategories,
            WeightedLootCategory[] weightedCategories)
        {
            Ref = @ref;
            MinimumRolls = minimumRolls;
            MaximumRolls = maximumRolls;
            GuaranteedCategories = guaranteedCategories ?? Array.Empty<LootCategory>();
            WeightedCategories = weightedCategories ?? Array.Empty<WeightedLootCategory>();
        }
    }
}
