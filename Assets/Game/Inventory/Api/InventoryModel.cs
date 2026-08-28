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

    public interface IInventoryRuntime
    {
        bool TryAddUnique(ItemRef item);
        void Add(ItemRef item, int quantity = 1);
        int Count(ItemRef item);
        IReadOnlyList<InventoryItemSnapshot> Snapshot();
    }
}
