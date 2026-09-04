using System;
using System.Collections.Generic;
using Game.Characters.Api;
using Game.Inventory.Api;
using Game.WorldObjects.Api;

namespace Game.Loot.Runtime
{
    /// <summary>Explicit composition-owned mapping from a gameplay character to its authoritative inventory.</summary>
    public interface ICharacterInventoryResolver
    {
        bool TryResolve(CharacterId characterId, out InventoryId inventoryId);
    }

    public sealed class CharacterInventoryBindings : ICharacterInventoryResolver
    {
        private readonly Dictionary<CharacterId, InventoryId> _bindings = new Dictionary<CharacterId, InventoryId>();

        public bool TryBind(CharacterId characterId, InventoryId inventoryId)
        {
            if (!characterId.IsValid || !inventoryId.IsValid || _bindings.ContainsKey(characterId)) return false;
            _bindings.Add(characterId, inventoryId);
            return true;
        }

        public bool TryResolve(CharacterId characterId, out InventoryId inventoryId) =>
            _bindings.TryGetValue(characterId, out inventoryId);
    }

    /// <summary>
    /// WorldObjects owns the pickup state transition; this adapter performs only the Loot/Inventory side effect
    /// against the same IInventoryTransactions authority used by the rest of GameSystem10.
    /// </summary>
    public sealed class WorldObjectLootAdapter : IWorldItemPickupTransfer
    {
        private readonly IInventoryTransactions _inventory;
        private readonly ICharacterInventoryResolver _inventoryResolver;

        public WorldObjectLootAdapter(
            IInventoryTransactions inventory,
            ICharacterInventoryResolver inventoryResolver)
        {
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _inventoryResolver = inventoryResolver ?? throw new ArgumentNullException(nameof(inventoryResolver));
        }

        public WorldInteractionResult TryTransfer(
            CharacterId actorId,
            WorldObjectId objectId,
            WorldItemPayload payload)
        {
            if (!payload.IsValid)
                return WorldInteractionResult.Reject(WorldInteractionFailure.InvalidPayload);

            InventoryId inventoryId;
            if (!_inventoryResolver.TryResolve(actorId, out inventoryId))
                return WorldInteractionResult.Reject(WorldInteractionFailure.MissingInventory);

            var result = _inventory.TryAdd(inventoryId, new ItemRef(payload.ItemId), payload.Quantity);
            if (result.Succeeded) return WorldInteractionResult.Success();

            if (result.FailureReason == InventoryFailureReason.UnknownInventory)
                return WorldInteractionResult.Reject(WorldInteractionFailure.MissingInventory);

            return WorldInteractionResult.Reject(WorldInteractionFailure.InventoryRejected);
        }
    }
}
