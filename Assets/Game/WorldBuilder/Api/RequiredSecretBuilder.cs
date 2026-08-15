using System;

namespace Game.WorldBuilder.Api
{
    public sealed partial class WorldBlueprintBuilder
    {
        public SecretRef RequireSecret(string id, Action<RequiredSecretBuilder> configure)
        {
            var secretRef = new SecretRef(id);
            var builder = new RequiredSecretBuilder(secretRef);
            configure?.Invoke(builder);
            _campaign.RequiredSecrets.Add(builder.Build());
            return secretRef;
        }
    }

    public sealed class RequiredSecretBuilder
    {
        private readonly SecretRef _ref;
        private SiteRef _site;
        private bool _hasSite;
        private SecretEntranceType _entrance;
        private bool _hasEntrance;
        private bool _requiresHiddenSpace;
        private ContainerArchetype _container = ContainerArchetype.TreasureChest;
        private LootTableRef _reward;
        private bool _hasReward;

        internal RequiredSecretBuilder(SecretRef @ref) => _ref = @ref;

        public RequiredSecretBuilder Inside(SiteRef site)
        {
            _site = site;
            _hasSite = true;
            return this;
        }

        public RequiredSecretBuilder Entrance(SecretEntranceType entrance)
        {
            _entrance = entrance;
            _hasEntrance = true;
            return this;
        }

        public RequiredSecretBuilder RequireHiddenSpace()
        {
            _requiresHiddenSpace = true;
            return this;
        }

        public RequiredSecretBuilder Container(ContainerArchetype container)
        {
            _container = container;
            return this;
        }

        public RequiredSecretBuilder RewardWith(LootTableRef reward)
        {
            _reward = reward;
            _hasReward = true;
            return this;
        }

        internal RequiredSecretSpec Build()
        {
            if (!_hasSite)
                throw new InvalidOperationException("Required secret '" + _ref + "' requires a host site.");
            if (!_hasEntrance)
                throw new InvalidOperationException("Required secret '" + _ref + "' requires an entrance type.");
            if (!_hasReward)
                throw new InvalidOperationException("Required secret '" + _ref + "' requires a reward table.");

            return new RequiredSecretSpec(
                _ref,
                _site,
                _entrance,
                _requiresHiddenSpace,
                _container,
                _reward);
        }
    }
}
