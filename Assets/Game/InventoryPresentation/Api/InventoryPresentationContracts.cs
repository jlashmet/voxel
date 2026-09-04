using System;
using System.Collections.Generic;
using Game.Inventory.Api;
using Game.Loot.Api;

namespace Game.InventoryPresentation.Api
{
    public readonly struct InventoryRowKey : IEquatable<InventoryRowKey>, IComparable<InventoryRowKey>
    {
        public InventoryId InventoryId { get; }
        public ItemRef Item { get; }

        public InventoryRowKey(InventoryId inventoryId, ItemRef item)
        {
            if (!inventoryId.IsValid) throw new ArgumentException("Inventory id is required.", nameof(inventoryId));
            if (!item.IsValid) throw new ArgumentException("Item reference is required.", nameof(item));
            InventoryId = inventoryId;
            Item = item;
        }

        public int CompareTo(InventoryRowKey other)
        {
            int inventory = InventoryId.CompareTo(other.InventoryId);
            return inventory != 0 ? inventory : Item.CompareTo(other.Item);
        }

        public bool Equals(InventoryRowKey other) => InventoryId == other.InventoryId && Item == other.Item;
        public override bool Equals(object obj) => obj is InventoryRowKey other && Equals(other);
        public override int GetHashCode() => (InventoryId.GetHashCode() * 397) ^ Item.GetHashCode();
        public static bool operator ==(InventoryRowKey left, InventoryRowKey right) => left.Equals(right);
        public static bool operator !=(InventoryRowKey left, InventoryRowKey right) => !left.Equals(right);
    }

    public readonly struct InventoryRowPresentation
    {
        public InventoryRowKey Key { get; }
        public string DisplayName { get; }
        public string IconText { get; }
        public int Quantity { get; }

        public InventoryRowPresentation(InventoryRowKey key, string displayName, string iconText, int quantity)
        {
            if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
            Key = key;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? key.Item.Id : displayName;
            IconText = string.IsNullOrWhiteSpace(iconText) ? "?" : iconText;
            Quantity = quantity;
        }
    }

    public enum InventorySortMode : byte
    {
        ItemId = 0,
        DisplayName = 1,
        Quantity = 2
    }

    public readonly struct InventoryPanelPresentation
    {
        public InventoryId InventoryId { get; }
        public ulong Revision { get; }
        public string BindingKind { get; }
        public string StableOwnerId { get; }
        public IReadOnlyList<InventoryRowPresentation> Rows { get; }
        public bool HasSelection { get; }
        public InventoryRowKey Selection { get; }
        public string Filter { get; }
        public InventorySortMode SortMode { get; }
        public bool SortAscending { get; }

        public InventoryPanelPresentation(
            InventoryId inventoryId,
            ulong revision,
            string bindingKind,
            string stableOwnerId,
            IReadOnlyList<InventoryRowPresentation> rows,
            bool hasSelection,
            InventoryRowKey selection,
            string filter,
            InventorySortMode sortMode,
            bool sortAscending)
        {
            InventoryId = inventoryId;
            Revision = revision;
            BindingKind = bindingKind ?? string.Empty;
            StableOwnerId = stableOwnerId ?? string.Empty;
            Rows = rows ?? throw new ArgumentNullException(nameof(rows));
            HasSelection = hasSelection;
            Selection = selection;
            Filter = filter ?? string.Empty;
            SortMode = sortMode;
            SortAscending = sortAscending;
        }
    }

    public readonly struct PendingOperationId : IEquatable<PendingOperationId>, IComparable<PendingOperationId>
    {
        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);

        public PendingOperationId(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Pending operation id is required.", nameof(value));
            Value = value.Trim();
        }

        public int CompareTo(PendingOperationId other) => StringComparer.Ordinal.Compare(Value ?? string.Empty, other.Value ?? string.Empty);
        public bool Equals(PendingOperationId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is PendingOperationId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
    }

    public enum PendingOperationKind : byte
    {
        Transfer = 0,
        Drop = 1
    }

    public enum PendingOperationStatus : byte
    {
        Pending = 0,
        Succeeded = 1,
        Rejected = 2
    }

    public readonly struct PendingOperationPresentation
    {
        public PendingOperationId Id { get; }
        public PendingOperationKind Kind { get; }
        public PendingOperationStatus Status { get; }
        public InventoryId SourceInventoryId { get; }
        public InventoryId DestinationInventoryId { get; }
        public ItemRef Item { get; }
        public int Quantity { get; }
        public string Error { get; }

        public PendingOperationPresentation(
            PendingOperationId id,
            PendingOperationKind kind,
            PendingOperationStatus status,
            InventoryId sourceInventoryId,
            InventoryId destinationInventoryId,
            ItemRef item,
            int quantity,
            string error)
        {
            Id = id;
            Kind = kind;
            Status = status;
            SourceInventoryId = sourceInventoryId;
            DestinationInventoryId = destinationInventoryId;
            Item = item;
            Quantity = quantity;
            Error = error ?? string.Empty;
        }
    }

    public readonly struct InventoryTransferIntent
    {
        public ContainerTransferRequest Request { get; }
        public InventoryTransferIntent(ContainerTransferRequest request) => Request = request;
    }

    public readonly struct InventoryDropIntent
    {
        public DropRequest Request { get; }
        public InventoryDropIntent(DropRequest request) => Request = request;
    }

    public readonly struct InventoryPresentationSnapshot
    {
        public IReadOnlyList<InventoryPanelPresentation> Panels { get; }
        public IReadOnlyList<PendingOperationPresentation> Operations { get; }

        public InventoryPresentationSnapshot(
            IReadOnlyList<InventoryPanelPresentation> panels,
            IReadOnlyList<PendingOperationPresentation> operations)
        {
            Panels = panels ?? throw new ArgumentNullException(nameof(panels));
            Operations = operations ?? throw new ArgumentNullException(nameof(operations));
        }
    }
}
