using System;
using System.Collections.Generic;
using Game.Characters.Api;

namespace Game.WorldObjects.Api
{
    public readonly struct WorldObjectId : IEquatable<WorldObjectId>, IComparable<WorldObjectId>
    {
        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);

        public WorldObjectId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("World object id is required.", nameof(value));
            Value = value;
        }

        public int CompareTo(WorldObjectId other) =>
            StringComparer.Ordinal.Compare(Value ?? string.Empty, other.Value ?? string.Empty);

        public bool Equals(WorldObjectId other) =>
            string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is WorldObjectId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(WorldObjectId left, WorldObjectId right) => left.Equals(right);
        public static bool operator !=(WorldObjectId left, WorldObjectId right) => !left.Equals(right);
    }

    public enum WorldObjectKind : byte
    {
        ItemPickup = 0,
        DoorToggle = 1,
        NestedSubsceneToggle = 2
    }

    public enum WorldInteractionFailure
    {
        None = 0,
        UnknownActor = 1,
        UnknownObject = 2,
        OutOfRange = 3,
        NotPermitted = 4,
        InvalidState = 5,
        UnsupportedCapability = 6,
        NoTarget = 7,
        InvalidPayload = 8,
        MissingInventory = 9,
        InventoryRejected = 10
    }

    public readonly struct WorldInteractionResult
    {
        public bool Succeeded { get; }
        public WorldInteractionFailure Failure { get; }

        private WorldInteractionResult(bool succeeded, WorldInteractionFailure failure)
        {
            Succeeded = succeeded;
            Failure = failure;
        }

        public static WorldInteractionResult Success() =>
            new WorldInteractionResult(true, WorldInteractionFailure.None);

        public static WorldInteractionResult Reject(WorldInteractionFailure failure)
        {
            if (failure == WorldInteractionFailure.None)
                throw new ArgumentException("A rejection requires a failure reason.", nameof(failure));
            return new WorldInteractionResult(false, failure);
        }
    }

    public readonly struct WorldItemPayload
    {
        public string ItemId { get; }
        public int Quantity { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(ItemId) && Quantity > 0;

        public WorldItemPayload(string itemId, int quantity)
        {
            ItemId = itemId == null ? string.Empty : itemId.Trim();
            Quantity = quantity;
        }
    }

    public readonly struct WorldObjectStateSnapshot
    {
        public WorldObjectId ObjectId { get; }
        public WorldObjectKind Kind { get; }
        public bool Enabled { get; }
        public int StateCode { get; }
        public ulong Revision { get; }

        public WorldObjectStateSnapshot(
            WorldObjectId objectId,
            WorldObjectKind kind,
            bool enabled,
            int stateCode,
            ulong revision)
        {
            ObjectId = objectId;
            Kind = kind;
            Enabled = enabled;
            StateCode = stateCode;
            Revision = revision;
        }
    }

    public readonly struct WorldInteractionContext
    {
        public CharacterId ActorId { get; }

        public WorldInteractionContext(CharacterId actorId)
        {
            ActorId = actorId;
        }
    }

    public readonly struct WorldInteractionFact
    {
        public ulong Sequence { get; }
        public CharacterId ActorId { get; }
        public WorldObjectId ObjectId { get; }
        public WorldObjectKind Kind { get; }
        public int StateCode { get; }
        public ulong ObjectRevision { get; }

        public WorldInteractionFact(
            ulong sequence,
            CharacterId actorId,
            WorldObjectId objectId,
            WorldObjectKind kind,
            int stateCode,
            ulong objectRevision)
        {
            Sequence = sequence;
            ActorId = actorId;
            ObjectId = objectId;
            Kind = kind;
            StateCode = stateCode;
            ObjectRevision = objectRevision;
        }
    }

    public interface IWorldInteractionValidator
    {
        WorldInteractionResult Validate(CharacterId actorId, WorldObjectId objectId);
    }

    public interface IWorldItemPickupTransfer
    {
        WorldInteractionResult TryTransfer(CharacterId actorId, WorldObjectId objectId, WorldItemPayload payload);
    }

    public interface IWorldObjectBehavior
    {
        WorldObjectId Id { get; }
        WorldObjectKind Kind { get; }
        CharacterVector3 Position { get; }
        WorldInteractionResult Interact(WorldInteractionContext context);
        WorldObjectStateSnapshot CaptureState();
        WorldInteractionResult RestoreState(WorldObjectStateSnapshot snapshot);
    }

    public interface IWorldObjectRegistry
    {
        bool TryRegister(IWorldObjectBehavior behavior);
        bool TryGet(WorldObjectId objectId, out IWorldObjectBehavior behavior);
        IReadOnlyList<IWorldObjectBehavior> GetAt(CharacterVector3 position);
        IReadOnlyList<WorldObjectStateSnapshot> CaptureState();
        WorldInteractionResult RestoreState(IReadOnlyList<WorldObjectStateSnapshot> snapshots);
    }

    public interface IWorldInteractionFactSink
    {
        void Publish(WorldInteractionFact fact);
    }
}
