using System;
using System.Collections.Generic;
using Game.Inventory.Api;

namespace Game.Inventory.Runtime
{
    /// <summary>
    /// Small deterministic ownership store. Definitions establish stable display order; mutable state
    /// is quantity only. UI and quest composition consume snapshots instead of owning item state.
    /// </summary>
    public sealed class InventoryRuntime : IInventoryRuntime
    {
        private readonly ItemDefinition[] _definitions;
        private readonly Dictionary<ItemRef, ItemDefinition> _byRef =
            new Dictionary<ItemRef, ItemDefinition>();
        private readonly Dictionary<ItemRef, int> _quantities =
            new Dictionary<ItemRef, int>();

        public InventoryRuntime(IReadOnlyList<ItemDefinition> definitions)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            _definitions = new ItemDefinition[definitions.Count];
            for (var i = 0; i < definitions.Count; i++)
            {
                ItemDefinition definition = definitions[i]
                    ?? throw new InvalidOperationException(
                        "Inventory definition collection contains null at index " + i + ".");
                if (_byRef.ContainsKey(definition.Ref))
                    throw new InvalidOperationException(
                        "Inventory definition collection contains duplicate item '" + definition.Ref + "'.");
                _definitions[i] = definition;
                _byRef.Add(definition.Ref, definition);
            }
        }

        public bool TryAddUnique(ItemRef item)
        {
            RequireDefinition(item);
            if (Count(item) > 0) return false;
            _quantities[item] = 1;
            return true;
        }

        public void Add(ItemRef item, int quantity = 1)
        {
            RequireDefinition(item);
            if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
            int existing;
            _quantities.TryGetValue(item, out existing);
            checked { _quantities[item] = existing + quantity; }
        }

        public int Count(ItemRef item)
        {
            RequireDefinition(item);
            int quantity;
            return _quantities.TryGetValue(item, out quantity) ? quantity : 0;
        }

        public IReadOnlyList<InventoryItemSnapshot> Snapshot()
        {
            var items = new List<InventoryItemSnapshot>();
            for (var i = 0; i < _definitions.Length; i++)
            {
                ItemDefinition definition = _definitions[i];
                int quantity;
                if (!_quantities.TryGetValue(definition.Ref, out quantity) || quantity <= 0) continue;
                items.Add(new InventoryItemSnapshot(definition, quantity));
            }
            return items.ToArray();
        }

        private ItemDefinition RequireDefinition(ItemRef item)
        {
            ItemDefinition definition;
            if (!_byRef.TryGetValue(item, out definition))
                throw new InvalidOperationException("Unknown inventory item '" + item + "'.");
            return definition;
        }
    }
}
