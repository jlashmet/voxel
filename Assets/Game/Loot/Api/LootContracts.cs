using System;
using System.Collections.Generic;
using Game.Characters.Api;
using Game.Inventory.Api;
using Game.WorldObjects.Api;

namespace Game.Loot.Api
{
    public readonly struct LootPayload : IEquatable<LootPayload>
    {
        public ItemRef Item { get; }
        public int Quantity { get; }

        public LootPayload(ItemRef item, int quantity)
        {
            if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
            Item = item;
            Quantity = quantity;
        }

        public bool Equals(LootPayload other) => Item == other.Item && Quantity == other.Quantity;
        public override bool Equals(object obj) => obj is LootPayload other && Equals(other);
        public override int GetHashCode() => (Item.GetHashCode() * 397) ^ Quantity;
    }

    public enum LootAvailability
    {
        Available = 0,
        Claimed = 1,
        Removed = 2
    }

    public readonly struct LootStateSnapshot
    {
        public WorldObjectId ObjectId { get; }
        public LootPayload Payload { get; }
        public LootAvailability Availability { get; }
        public CharacterId ClaimedBy { get; }

        public LootStateSnapshot(WorldObjectId objectId, LootPayload payload, LootAvailability availability, CharacterId claimedBy = default)
        {
            ObjectId = objectId;
            Payload = payload;
            Availability = availability;
            ClaimedBy = claimedBy;
        }
    }

    public enum LootTransferKind
    {
        Pickup = 0,
        ContainerTransfer = 1,
        Drop = 2
    }

    public enum LootTransferFailure
    {
        None = 0,
        UnknownLoot = 1,
        DuplicateWorldObject = 2,
        AlreadyClaimed = 3,
        AlreadyRemoved = 4,
        InteractionRejected = 5,
        InventoryRejected = 6,
        InvalidRestoreState = 7
    }

    public readonly struct LootTransferFact
    {
        public LootTransferKind Kind { get; }
        public CharacterId ActorId { get; }
        public WorldObjectId ObjectId { get; }
        public LootPayload Payload { get; }
        public InventoryId SourceInventoryId { get; }
        public InventoryId DestinationInventoryId { get; }

        public LootTransferFact(LootTransferKind kind, CharacterId actorId, WorldObjectId objectId, LootPayload payload,
            InventoryId sourceInventoryId = default, InventoryId destinationInventoryId = default)
        {
            Kind = kind;
            ActorId = actorId;
            ObjectId = objectId;
            Payload = payload;
            SourceInventoryId = sourceInventoryId;
            DestinationInventoryId = destinationInventoryId;
        }
    }

    public readonly struct LootTransferResult
    {
        public bool Succeeded { get; }
        public LootTransferFailure Failure { get; }
        public WorldInteractionFailure InteractionFailure { get; }
        public InventoryFailureReason InventoryFailure { get; }
        public LootTransferFact Fact { get; }

        private LootTransferResult(bool succeeded, LootTransferFailure failure, WorldInteractionFailure interactionFailure,
            InventoryFailureReason inventoryFailure, LootTransferFact fact)
        {
            Succeeded = succeeded;
            Failure = failure;
            InteractionFailure = interactionFailure;
            InventoryFailure = inventoryFailure;
            Fact = fact;
        }

        public static LootTransferResult Success(LootTransferFact fact) =>
            new LootTransferResult(true, LootTransferFailure.None, WorldInteractionFailure.None, InventoryFailureReason.None, fact);

        public static LootTransferResult Reject(LootTransferFailure failure,
            WorldInteractionFailure interactionFailure = WorldInteractionFailure.None,
            InventoryFailureReason inventoryFailure = InventoryFailureReason.None) =>
            new LootTransferResult(false, failure, interactionFailure, inventoryFailure, default);
    }

    public readonly struct PickupRequest
    {
        public CharacterId ActorId { get; }
        public WorldObjectId ObjectId { get; }
        public InventoryId DestinationInventoryId { get; }

        public PickupRequest(CharacterId actorId, WorldObjectId objectId, InventoryId destinationInventoryId)
        {
            ActorId = actorId;
            ObjectId = objectId;
            DestinationInventoryId = destinationInventoryId;
        }
    }

    public readonly struct ContainerTransferRequest
    {
        public CharacterId ActorId { get; }
        public WorldObjectId ContextObjectId { get; }
        public InventoryId SourceInventoryId { get; }
        public InventoryId DestinationInventoryId { get; }
        public ItemRef Item { get; }
        public int Quantity { get; }

        public ContainerTransferRequest(CharacterId actorId, WorldObjectId contextObjectId, InventoryId sourceInventoryId,
            InventoryId destinationInventoryId, ItemRef item, int quantity)
        {
            if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
            ActorId = actorId;
            ContextObjectId = contextObjectId;
            SourceInventoryId = sourceInventoryId;
            DestinationInventoryId = destinationInventoryId;
            Item = item;
            Quantity = quantity;
        }
    }

    public readonly struct DropRequest
    {
        public CharacterId ActorId { get; }
        public WorldObjectId ContextObjectId { get; }
        public WorldObjectId NewObjectId { get; }
        public InventoryId SourceInventoryId { get; }
        public LootPayload Payload { get; }

        public DropRequest(CharacterId actorId, WorldObjectId contextObjectId, WorldObjectId newObjectId,
            InventoryId sourceInventoryId, LootPayload payload)
        {
            ActorId = actorId;
            ContextObjectId = contextObjectId;
            NewObjectId = newObjectId;
            SourceInventoryId = sourceInventoryId;
            Payload = payload;
        }
    }

    public interface ILootRuntime
    {
        bool TryBind(WorldObjectId objectId, LootPayload payload);
        LootTransferResult TryPickup(PickupRequest request);
        LootTransferResult TryContainerTransfer(ContainerTransferRequest request);
        LootTransferResult TryDrop(DropRequest request);
        IReadOnlyList<LootStateSnapshot> Capture();
        bool TryRestore(IReadOnlyList<LootStateSnapshot> snapshots);
    }
}
