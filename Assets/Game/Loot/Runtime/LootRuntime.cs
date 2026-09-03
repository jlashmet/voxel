using System;
using System.Collections.Generic;
using Game.Inventory.Api;
using Game.Loot.Api;
using Game.WorldObjects.Api;

namespace Game.Loot.Runtime
{
    /// <summary>
    /// Coordinates authoritative world-item lifecycle with inventory transactions. All operations are
    /// serialized so a world payload is committed exactly once and world state only changes after the
    /// corresponding inventory transaction succeeds.
    /// </summary>
    public sealed class LootRuntime : ILootRuntime
    {
        private readonly object _gate = new object();
        private readonly IInventoryTransactions _inventory;
        private readonly IWorldInteractionValidator _interaction;
        private readonly Dictionary<WorldObjectId, LootStateSnapshot> _loot =
            new Dictionary<WorldObjectId, LootStateSnapshot>();

        public LootRuntime(IInventoryTransactions inventory, IWorldInteractionValidator interaction)
        {
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _interaction = interaction ?? throw new ArgumentNullException(nameof(interaction));
        }

        public bool TryBind(WorldObjectId objectId, LootPayload payload)
        {
            lock (_gate)
            {
                if (_loot.ContainsKey(objectId)) return false;
                _loot.Add(objectId, new LootStateSnapshot(objectId, payload, LootAvailability.Available));
                return true;
            }
        }

        public LootTransferResult TryPickup(PickupRequest request)
        {
            lock (_gate)
            {
                LootStateSnapshot current;
                if (!_loot.TryGetValue(request.ObjectId, out current))
                    return LootTransferResult.Reject(LootTransferFailure.UnknownLoot);
                if (current.Availability == LootAvailability.Removed)
                    return LootTransferResult.Reject(LootTransferFailure.AlreadyRemoved);
                if (current.Availability == LootAvailability.Claimed)
                    return LootTransferResult.Reject(LootTransferFailure.AlreadyClaimed);

                var interaction = _interaction.Validate(request.ActorId, request.ObjectId);
                if (!interaction.Succeeded)
                    return LootTransferResult.Reject(LootTransferFailure.InteractionRejected, interaction.Failure);

                _loot[request.ObjectId] = new LootStateSnapshot(
                    current.ObjectId, current.Payload, LootAvailability.Claimed, request.ActorId);

                var inventory = _inventory.TryAdd(
                    request.DestinationInventoryId, current.Payload.Item, current.Payload.Quantity);
                if (!inventory.Succeeded)
                {
                    _loot[request.ObjectId] = current;
                    return LootTransferResult.Reject(
                        LootTransferFailure.InventoryRejected,
                        inventoryFailure: inventory.Failure);
                }

                var removed = new LootStateSnapshot(
                    current.ObjectId, current.Payload, LootAvailability.Removed, request.ActorId);
                _loot[request.ObjectId] = removed;
                return LootTransferResult.Success(new LootTransferFact(
                    LootTransferKind.Pickup,
                    request.ActorId,
                    request.ObjectId,
                    current.Payload,
                    destinationInventoryId: request.DestinationInventoryId));
            }
        }

        public LootTransferResult TryContainerTransfer(ContainerTransferRequest request)
        {
            lock (_gate)
            {
                var interaction = _interaction.Validate(request.ActorId, request.ContextObjectId);
                if (!interaction.Succeeded)
                    return LootTransferResult.Reject(LootTransferFailure.InteractionRejected, interaction.Failure);

                var inventory = _inventory.TryTransfer(
                    request.SourceInventoryId,
                    request.DestinationInventoryId,
                    request.Item,
                    request.Quantity);
                if (!inventory.Succeeded)
                    return LootTransferResult.Reject(
                        LootTransferFailure.InventoryRejected,
                        inventoryFailure: inventory.Failure);

                return LootTransferResult.Success(new LootTransferFact(
                    LootTransferKind.ContainerTransfer,
                    request.ActorId,
                    request.ContextObjectId,
                    new LootPayload(request.Item, request.Quantity),
                    request.SourceInventoryId,
                    request.DestinationInventoryId));
            }
        }

        public LootTransferResult TryDrop(DropRequest request)
        {
            lock (_gate)
            {
                if (_loot.ContainsKey(request.NewObjectId))
                    return LootTransferResult.Reject(LootTransferFailure.DuplicateWorldObject);

                var interaction = _interaction.Validate(request.ActorId, request.ContextObjectId);
                if (!interaction.Succeeded)
                    return LootTransferResult.Reject(LootTransferFailure.InteractionRejected, interaction.Failure);

                var inventory = _inventory.TryRemove(
                    request.SourceInventoryId,
                    request.Payload.Item,
                    request.Payload.Quantity);
                if (!inventory.Succeeded)
                    return LootTransferResult.Reject(
                        LootTransferFailure.InventoryRejected,
                        inventoryFailure: inventory.Failure);

                _loot.Add(request.NewObjectId, new LootStateSnapshot(
                    request.NewObjectId,
                    request.Payload,
                    LootAvailability.Available));

                return LootTransferResult.Success(new LootTransferFact(
                    LootTransferKind.Drop,
                    request.ActorId,
                    request.NewObjectId,
                    request.Payload,
                    sourceInventoryId: request.SourceInventoryId));
            }
        }

        public IReadOnlyList<LootStateSnapshot> Capture()
        {
            lock (_gate)
            {
                var snapshots = new List<LootStateSnapshot>(_loot.Values);
                snapshots.Sort((left, right) => left.ObjectId.CompareTo(right.ObjectId));
                return snapshots.ToArray();
            }
        }

        public bool TryRestore(IReadOnlyList<LootStateSnapshot> snapshots)
        {
            if (snapshots == null) return false;

            lock (_gate)
            {
                var restored = new Dictionary<WorldObjectId, LootStateSnapshot>();
                for (var i = 0; i < snapshots.Count; i++)
                {
                    var snapshot = snapshots[i];
                    if (!snapshot.ObjectId.IsValid || snapshot.Payload.Quantity <= 0)
                        return false;
                    if (snapshot.Availability != LootAvailability.Available &&
                        snapshot.Availability != LootAvailability.Claimed &&
                        snapshot.Availability != LootAvailability.Removed)
                        return false;
                    if (restored.ContainsKey(snapshot.ObjectId)) return false;
                    restored.Add(snapshot.ObjectId, snapshot);
                }

                _loot.Clear();
                foreach (var pair in restored) _loot.Add(pair.Key, pair.Value);
                return true;
            }
        }
    }
}
