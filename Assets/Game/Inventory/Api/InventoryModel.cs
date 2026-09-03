using System;
using System.Collections.Generic;

namespace Game.Inventory.Api
{
    public readonly struct ItemRef : IEquatable<ItemRef>
    {
        public string Id { get; }

        public ItemRef(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Item id is required.", nameof(id));
            Id = id;
        }

        public bool Equals(ItemRef other) => string.Equals(Id, other.Id, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is ItemRef other && Equals(other);
        public override int GetHashCode() => Id == null ? 0 : StringComparer.Ordinal.GetHashCode(Id);
        public override string ToString() => Id ?? "<unset-item>";
        public static bool operator ==(ItemRef left, ItemRef right) => left.Equals(right);
        public static bool operator !=(ItemRef left, ItemRef right) => !left.Equals(right);
    }

    public readonly struct InventoryId : IEquatable<InventoryId>, IComparable<InventoryId>
    {
        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);

        public InventoryId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Inventory id is required.", nameof(value));
            Value = value;
        }

        public int CompareTo(InventoryId other) =>
            StringComparer.Ordinal.Compare(Value ?? string.Empty, other.Value ?? string.Empty);
        public bool Equals(InventoryId other) =>
            string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is InventoryId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(InventoryId left, InventoryId right) => left.Equals(right);
        public static bool operator !=(InventoryId left, InventoryId right) => !left.Equals(right);
    }

    public sealed class ItemDefinition
    {
        public ItemRef Ref { get; }
        public string DisplayName { get; }
        public string IconText { get; }

        public ItemDefinition(ItemRef @ref, string displayName, string iconText = "?")
        {
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("Item display name is required.", nameof(displayName));
            Ref = @ref;
            DisplayName = displayName;
            IconText = string.IsNullOrWhiteSpace(iconText) ? "?" : iconText;
        }
    }

    public readonly struct InventoryItemSnapshot
    {
        public ItemDefinition Definition { get; }
        public int Quantity { get; }

        public InventoryItemSnapshot(ItemDefinition definition, int quantity)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
            Quantity = quantity;
        }
    }

    public enum InventoryTransactionFailure
    {
        None = 0,
        UnknownInventory = 1,
        UnknownItem = 2,
        InvalidQuantity = 3,
        InsufficientQuantity = 4,
        DestinationRejected = 5,
        ArithmeticOverflow = 6
    }

    public readonly struct InventoryTransactionResult
    {
        public bool Succeeded { get; }
        public InventoryTransactionFailure Failure { get; }

        private InventoryTransactionResult(bool succeeded, InventoryTransactionFailure failure)
        {
            Succeeded = succeeded;
            Failure = failure;
        }

        public static InventoryTransactionResult Success() =>
            new InventoryTransactionResult(true, InventoryTransactionFailure.None);

        public static InventoryTransactionResult Reject(InventoryTransactionFailure failure)
        {
            if (failure == InventoryTransactionFailure.None)
                throw new ArgumentException("A rejected transaction requires a failure reason.", nameof(failure));
            return new InventoryTransactionResult(false, failure);
        }
    }

    public interface IInventoryTransactions
    {
        InventoryTransactionResult TryAdd(InventoryId inventoryId, ItemRef item, int quantity);
        InventoryTransactionResult TryRemove(InventoryId inventoryId, ItemRef item, int quantity);
        InventoryTransactionResult TryTransfer(InventoryId sourceInventoryId, InventoryId destinationInventoryId, ItemRef item, int quantity);
        int Count(InventoryId inventoryId, ItemRef item);
    }

    public interface IInventoryRuntime
    {
        bool TryAddUnique(ItemRef item);
        void Add(ItemRef item, int quantity = 1);
        int Count(ItemRef item);
        IReadOnlyList<InventoryItemSnapshot> Snapshot();
    }
}
