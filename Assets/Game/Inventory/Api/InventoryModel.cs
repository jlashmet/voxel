using System;
using System.Collections.Generic;

namespace Game.Inventory.Api
{
    public readonly struct ItemRef : IEquatable<ItemRef>, IComparable<ItemRef>
    {
        public string Id { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Id);

        public ItemRef(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Item id is required.", nameof(id));
            Id = id.Trim();
        }

        public int CompareTo(ItemRef other) => StringComparer.Ordinal.Compare(Id ?? string.Empty, other.Id ?? string.Empty);
        public bool Equals(ItemRef other) => string.Equals(Id, other.Id, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is ItemRef other && Equals(other);
        public override int GetHashCode() => Id == null ? 0 : StringComparer.Ordinal.GetHashCode(Id);
        public override string ToString() => Id ?? "<unset-item>";
        public static bool operator ==(ItemRef left, ItemRef right) => left.Equals(right);
        public static bool operator !=(ItemRef left, ItemRef right) => !left.Equals(right);
    }

    public sealed class ItemDefinition
    {
        public ItemRef Ref { get; }
        public string DisplayName { get; }
        public string IconText { get; }

        public ItemDefinition(ItemRef @ref, string displayName, string iconText = "?")
        {
            if (!@ref.IsValid) throw new ArgumentException("Item reference is required.", nameof(@ref));
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("Item display name is required.", nameof(displayName));
            Ref = @ref;
            DisplayName = displayName;
            IconText = string.IsNullOrWhiteSpace(iconText) ? "?" : iconText;
        }
    }

    /// <summary>Stable inventory identity. Ownership type is deliberately not encoded in this value.</summary>
    public readonly struct InventoryId : IEquatable<InventoryId>, IComparable<InventoryId>
    {
        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);

        public InventoryId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Inventory id is required.", nameof(value));
            Value = value.Trim();
        }

        public int CompareTo(InventoryId other) => StringComparer.Ordinal.Compare(Value ?? string.Empty, other.Value ?? string.Empty);
        public bool Equals(InventoryId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is InventoryId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? "<unset-inventory>";
        public static bool operator ==(InventoryId left, InventoryId right) => left.Equals(right);
        public static bool operator !=(InventoryId left, InventoryId right) => !left.Equals(right);
    }

    /// <summary>
    /// Generic stable binding metadata supplied by composition. Inventory does not interpret Kind or StableOwnerId.
    /// </summary>
    public readonly struct InventoryBindingMetadata : IEquatable<InventoryBindingMetadata>
    {
        public string Kind { get; }
        public string StableOwnerId { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Kind) && !string.IsNullOrWhiteSpace(StableOwnerId);

        public InventoryBindingMetadata(string kind, string stableOwnerId)
        {
            if (string.IsNullOrWhiteSpace(kind)) throw new ArgumentException("Binding kind is required.", nameof(kind));
            if (string.IsNullOrWhiteSpace(stableOwnerId)) throw new ArgumentException("Stable owner id is required.", nameof(stableOwnerId));
            Kind = kind.Trim();
            StableOwnerId = stableOwnerId.Trim();
        }

        public bool Equals(InventoryBindingMetadata other) =>
            string.Equals(Kind, other.Kind, StringComparison.Ordinal) &&
            string.Equals(StableOwnerId, other.StableOwnerId, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is InventoryBindingMetadata other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                return ((Kind == null ? 0 : StringComparer.Ordinal.GetHashCode(Kind)) * 397) ^
                       (StableOwnerId == null ? 0 : StringComparer.Ordinal.GetHashCode(StableOwnerId));
            }
        }
    }

    public readonly struct InventoryDescriptor
    {
        public InventoryId Id { get; }
        public InventoryBindingMetadata Binding { get; }

        public InventoryDescriptor(InventoryId id, InventoryBindingMetadata binding)
        {
            if (!id.IsValid) throw new ArgumentException("Inventory id is required.", nameof(id));
            if (!binding.IsValid) throw new ArgumentException("Binding metadata is required.", nameof(binding));
            Id = id;
            Binding = binding;
        }
    }

    public readonly struct InventoryEntry
    {
        public ItemRef Item { get; }
        public int Quantity { get; }

        public InventoryEntry(ItemRef item, int quantity)
        {
            if (!item.IsValid) throw new ArgumentException("Item reference is required.", nameof(item));
            if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
            Item = item;
            Quantity = quantity;
        }
    }

    public readonly struct InventorySnapshot
    {
        public InventoryId Id { get; }
        public ulong Revision { get; }
        public IReadOnlyList<InventoryEntry> Entries { get; }

        public InventorySnapshot(InventoryId id, ulong revision, IReadOnlyList<InventoryEntry> entries)
        {
            if (!id.IsValid) throw new ArgumentException("Inventory id is required.", nameof(id));
            Id = id;
            Revision = revision;
            Entries = entries ?? throw new ArgumentNullException(nameof(entries));
        }

        public int Count(ItemRef item)
        {
            if (!item.IsValid) return 0;
            for (var i = 0; i < Entries.Count; i++)
                if (Entries[i].Item == item)
                    return Entries[i].Quantity;
            return 0;
        }
    }

    /// <summary>
    /// Flat current-state projection retained for cross-domain consumers such as Loot. The richer
    /// InventoryStateCapture remains the authoritative persistence seam because it preserves revisions.
    /// </summary>
    public readonly struct InventoryQuantitySnapshot
    {
        public InventoryId InventoryId { get; }
        public ItemRef Item { get; }
        public int Quantity { get; }

        public InventoryQuantitySnapshot(InventoryId inventoryId, ItemRef item, int quantity)
        {
            if (!inventoryId.IsValid) throw new ArgumentException("Inventory id is required.", nameof(inventoryId));
            if (!item.IsValid) throw new ArgumentException("Item reference is required.", nameof(item));
            if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
            InventoryId = inventoryId;
            Item = item;
            Quantity = quantity;
        }
    }

    public readonly struct InventoryTransactionId : IEquatable<InventoryTransactionId>, IComparable<InventoryTransactionId>
    {
        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);

        public InventoryTransactionId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Inventory transaction id is required.", nameof(value));
            Value = value.Trim();
        }

        public int CompareTo(InventoryTransactionId other) => StringComparer.Ordinal.Compare(Value ?? string.Empty, other.Value ?? string.Empty);
        public bool Equals(InventoryTransactionId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is InventoryTransactionId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? "<unset-inventory-transaction>";
        public static bool operator ==(InventoryTransactionId left, InventoryTransactionId right) => left.Equals(right);
        public static bool operator !=(InventoryTransactionId left, InventoryTransactionId right) => !left.Equals(right);
    }

    public enum InventoryMutationKind : byte
    {
        Add = 0,
        Remove = 1,
        Transfer = 2
    }

    public enum InventoryFailureReason : byte
    {
        None = 0,
        InvalidTransactionId = 1,
        InvalidInventoryId = 2,
        UnknownInventory = 3,
        InvalidItem = 4,
        UnknownItem = 5,
        InvalidQuantity = 6,
        InsufficientQuantity = 7,
        SameInventory = 8,
        QuantityOverflow = 9,
        TransactionConflict = 10,
        InvalidRestore = 11,
        DestinationRejected = 12
    }

    public readonly struct InventoryAddRequest
    {
        public InventoryTransactionId TransactionId { get; }
        public InventoryId InventoryId { get; }
        public ItemRef Item { get; }
        public int Quantity { get; }

        public InventoryAddRequest(InventoryTransactionId transactionId, InventoryId inventoryId, ItemRef item, int quantity)
        {
            TransactionId = transactionId;
            InventoryId = inventoryId;
            Item = item;
            Quantity = quantity;
        }
    }

    public readonly struct InventoryRemoveRequest
    {
        public InventoryTransactionId TransactionId { get; }
        public InventoryId InventoryId { get; }
        public ItemRef Item { get; }
        public int Quantity { get; }

        public InventoryRemoveRequest(InventoryTransactionId transactionId, InventoryId inventoryId, ItemRef item, int quantity)
        {
            TransactionId = transactionId;
            InventoryId = inventoryId;
            Item = item;
            Quantity = quantity;
        }
    }

    public readonly struct InventoryTransferRequest
    {
        public InventoryTransactionId TransactionId { get; }
        public InventoryId SourceInventoryId { get; }
        public InventoryId DestinationInventoryId { get; }
        public ItemRef Item { get; }
        public int Quantity { get; }

        public InventoryTransferRequest(
            InventoryTransactionId transactionId,
            InventoryId sourceInventoryId,
            InventoryId destinationInventoryId,
            ItemRef item,
            int quantity)
        {
            TransactionId = transactionId;
            SourceInventoryId = sourceInventoryId;
            DestinationInventoryId = destinationInventoryId;
            Item = item;
            Quantity = quantity;
        }
    }

    public readonly struct InventoryChangeEvent
    {
        public InventoryTransactionId TransactionId { get; }
        public InventoryMutationKind Kind { get; }
        public InventoryId InventoryId { get; }
        public ItemRef Item { get; }
        public int QuantityDelta { get; }
        public ulong Revision { get; }

        public InventoryChangeEvent(
            InventoryTransactionId transactionId,
            InventoryMutationKind kind,
            InventoryId inventoryId,
            ItemRef item,
            int quantityDelta,
            ulong revision)
        {
            TransactionId = transactionId;
            Kind = kind;
            InventoryId = inventoryId;
            Item = item;
            QuantityDelta = quantityDelta;
            Revision = revision;
        }
    }

    public sealed class InventoryTransactionResult
    {
        public InventoryTransactionId TransactionId { get; }
        public InventoryMutationKind Kind { get; }
        public InventoryFailureReason FailureReason { get; }
        public bool Succeeded => FailureReason == InventoryFailureReason.None;
        public bool HasSourceSnapshot { get; }
        public InventorySnapshot SourceSnapshot { get; }
        public bool HasDestinationSnapshot { get; }
        public InventorySnapshot DestinationSnapshot { get; }
        public IReadOnlyList<InventoryChangeEvent> Changes { get; }

        public InventoryTransactionResult(
            InventoryTransactionId transactionId,
            InventoryMutationKind kind,
            InventoryFailureReason failureReason,
            bool hasSourceSnapshot,
            InventorySnapshot sourceSnapshot,
            bool hasDestinationSnapshot,
            InventorySnapshot destinationSnapshot,
            IReadOnlyList<InventoryChangeEvent> changes)
        {
            TransactionId = transactionId;
            Kind = kind;
            FailureReason = failureReason;
            HasSourceSnapshot = hasSourceSnapshot;
            SourceSnapshot = sourceSnapshot;
            HasDestinationSnapshot = hasDestinationSnapshot;
            DestinationSnapshot = destinationSnapshot;
            Changes = changes ?? Array.Empty<InventoryChangeEvent>();
        }

        public static InventoryTransactionResult Reject(InventoryMutationKind kind, InventoryFailureReason failureReason)
        {
            if (failureReason == InventoryFailureReason.None)
                throw new ArgumentException("A rejected transaction requires a failure reason.", nameof(failureReason));
            return new InventoryTransactionResult(
                default,
                kind,
                failureReason,
                false,
                default,
                false,
                default,
                Array.Empty<InventoryChangeEvent>());
        }
    }

    public readonly struct InventoryStateCapture
    {
        public IReadOnlyList<InventorySnapshot> Inventories { get; }

        public InventoryStateCapture(IReadOnlyList<InventorySnapshot> inventories)
        {
            Inventories = inventories ?? throw new ArgumentNullException(nameof(inventories));
        }
    }

    public interface IInventoryQuery
    {
        bool TryGetDescriptor(InventoryId inventoryId, out InventoryDescriptor descriptor);
        bool TryGetDefinition(ItemRef item, out ItemDefinition definition);
        bool TryGetSnapshot(InventoryId inventoryId, out InventorySnapshot snapshot);
        int Count(InventoryId inventoryId, ItemRef item);
        IReadOnlyList<InventorySnapshot> GetAllSnapshots();
    }

    public interface IInventoryAuthority
    {
        event Action<InventoryChangeEvent> Changed;
        InventoryTransactionResult Add(InventoryAddRequest request);
        InventoryTransactionResult Remove(InventoryRemoveRequest request);
        InventoryTransactionResult Transfer(InventoryTransferRequest request);
    }

    /// <summary>
    /// Cross-domain convenience seam. Implementations must delegate to the same authoritative transaction
    /// store used by IInventoryAuthority; this interface must never imply a second quantity store.
    /// </summary>
    public interface IInventoryTransactions
    {
        InventoryTransactionResult TryAdd(InventoryId inventoryId, ItemRef item, int quantity);
        InventoryTransactionResult TryRemove(InventoryId inventoryId, ItemRef item, int quantity);
        InventoryTransactionResult TryTransfer(InventoryId sourceInventoryId, InventoryId destinationInventoryId, ItemRef item, int quantity);
        int Count(InventoryId inventoryId, ItemRef item);
        IReadOnlyList<InventoryQuantitySnapshot> Capture();
        bool TryRestore(IReadOnlyList<InventoryQuantitySnapshot> snapshots);
    }

    /// <summary>Transport-agnostic deterministic capture/restore seam for persistence composition.</summary>
    public interface IInventoryStatePort
    {
        InventoryStateCapture CaptureState();
        InventoryFailureReason RestoreState(InventoryStateCapture state);
    }
}
