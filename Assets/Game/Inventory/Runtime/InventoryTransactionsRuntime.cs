using System;
using System.Collections.Generic;
using Game.Inventory.Api;

namespace Game.Inventory.Runtime
{
    /// <summary>
    /// Minimal authoritative multi-inventory transaction store used by cross-domain gameplay systems.
    /// It deliberately has no slot/weight/capacity policy; destinations may be rejected by higher-level
    /// adapters before a transaction is attempted.
    /// </summary>
    public sealed class InventoryTransactionsRuntime : IInventoryTransactions
    {
        private readonly object _gate = new object();
        private readonly HashSet<ItemRef> _knownItems = new HashSet<ItemRef>();
        private readonly Dictionary<InventoryId, Dictionary<ItemRef, int>> _inventories =
            new Dictionary<InventoryId, Dictionary<ItemRef, int>>();

        public InventoryTransactionsRuntime(IReadOnlyList<ItemDefinition> definitions)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i] ?? throw new InvalidOperationException("Inventory definition collection contains null at index " + i + ".");
                if (!_knownItems.Add(definition.Ref))
                    throw new InvalidOperationException("Inventory definition collection contains duplicate item '" + definition.Ref + "'.");
            }
        }

        public void Register(InventoryId inventoryId)
        {
            lock (_gate)
            {
                if (!_inventories.ContainsKey(inventoryId))
                    _inventories.Add(inventoryId, new Dictionary<ItemRef, int>());
            }
        }

        public InventoryTransactionResult TryAdd(InventoryId inventoryId, ItemRef item, int quantity)
        {
            lock (_gate)
            {
                Dictionary<ItemRef, int> inventory;
                var validation = ValidateMutation(inventoryId, item, quantity, out inventory);
                if (!validation.Succeeded) return validation;

                int existing;
                inventory.TryGetValue(item, out existing);
                int next;
                try
                {
                    checked { next = existing + quantity; }
                }
                catch (OverflowException)
                {
                    return InventoryTransactionResult.Reject(InventoryTransactionFailure.ArithmeticOverflow);
                }
                inventory[item] = next;
                return InventoryTransactionResult.Success();
            }
        }

        public InventoryTransactionResult TryRemove(InventoryId inventoryId, ItemRef item, int quantity)
        {
            lock (_gate)
            {
                Dictionary<ItemRef, int> inventory;
                var validation = ValidateMutation(inventoryId, item, quantity, out inventory);
                if (!validation.Succeeded) return validation;

                int existing;
                if (!inventory.TryGetValue(item, out existing) || existing < quantity)
                    return InventoryTransactionResult.Reject(InventoryTransactionFailure.InsufficientQuantity);

                var remaining = existing - quantity;
                if (remaining == 0) inventory.Remove(item);
                else inventory[item] = remaining;
                return InventoryTransactionResult.Success();
            }
        }

        public InventoryTransactionResult TryTransfer(InventoryId sourceInventoryId, InventoryId destinationInventoryId, ItemRef item, int quantity)
        {
            lock (_gate)
            {
                Dictionary<ItemRef, int> source;
                var sourceValidation = ValidateMutation(sourceInventoryId, item, quantity, out source);
                if (!sourceValidation.Succeeded) return sourceValidation;

                Dictionary<ItemRef, int> destination;
                var destinationValidation = ValidateMutation(destinationInventoryId, item, quantity, out destination);
                if (!destinationValidation.Succeeded) return destinationValidation;

                int sourceQuantity;
                if (!source.TryGetValue(item, out sourceQuantity) || sourceQuantity < quantity)
                    return InventoryTransactionResult.Reject(InventoryTransactionFailure.InsufficientQuantity);

                int destinationQuantity;
                destination.TryGetValue(item, out destinationQuantity);
                int nextDestination;
                try
                {
                    checked { nextDestination = destinationQuantity + quantity; }
                }
                catch (OverflowException)
                {
                    return InventoryTransactionResult.Reject(InventoryTransactionFailure.ArithmeticOverflow);
                }

                var nextSource = sourceQuantity - quantity;
                if (nextSource == 0) source.Remove(item);
                else source[item] = nextSource;
                destination[item] = nextDestination;
                return InventoryTransactionResult.Success();
            }
        }

        public int Count(InventoryId inventoryId, ItemRef item)
        {
            lock (_gate)
            {
                Dictionary<ItemRef, int> inventory;
                if (!_inventories.TryGetValue(inventoryId, out inventory)) return 0;
                if (!_knownItems.Contains(item)) return 0;
                int quantity;
                return inventory.TryGetValue(item, out quantity) ? quantity : 0;
            }
        }

        public IReadOnlyList<InventoryQuantitySnapshot> Capture()
        {
            lock (_gate)
            {
                var snapshots = new List<InventoryQuantitySnapshot>();
                var inventoryIds = new List<InventoryId>(_inventories.Keys);
                inventoryIds.Sort();
                for (var i = 0; i < inventoryIds.Count; i++)
                {
                    var inventoryId = inventoryIds[i];
                    var entries = new List<KeyValuePair<ItemRef, int>>(_inventories[inventoryId]);
                    entries.Sort((left, right) => StringComparer.Ordinal.Compare(left.Key.Id, right.Key.Id));
                    for (var j = 0; j < entries.Count; j++)
                        snapshots.Add(new InventoryQuantitySnapshot(inventoryId, entries[j].Key, entries[j].Value));
                }
                return snapshots.ToArray();
            }
        }

        public bool TryRestore(IReadOnlyList<InventoryQuantitySnapshot> snapshots)
        {
            if (snapshots == null) return false;

            lock (_gate)
            {
                var restored = new Dictionary<InventoryId, Dictionary<ItemRef, int>>();
                foreach (var inventoryId in _inventories.Keys)
                    restored.Add(inventoryId, new Dictionary<ItemRef, int>());

                for (var i = 0; i < snapshots.Count; i++)
                {
                    var snapshot = snapshots[i];
                    Dictionary<ItemRef, int> inventory;
                    if (!restored.TryGetValue(snapshot.InventoryId, out inventory)) return false;
                    if (!_knownItems.Contains(snapshot.Item) || snapshot.Quantity <= 0) return false;
                    if (inventory.ContainsKey(snapshot.Item)) return false;
                    inventory.Add(snapshot.Item, snapshot.Quantity);
                }

                _inventories.Clear();
                foreach (var pair in restored) _inventories.Add(pair.Key, pair.Value);
                return true;
            }
        }

        private InventoryTransactionResult ValidateMutation(InventoryId inventoryId, ItemRef item, int quantity, out Dictionary<ItemRef, int> inventory)
        {
            inventory = null;
            if (quantity <= 0)
                return InventoryTransactionResult.Reject(InventoryTransactionFailure.InvalidQuantity);
            if (!_knownItems.Contains(item))
                return InventoryTransactionResult.Reject(InventoryTransactionFailure.UnknownItem);
            if (!_inventories.TryGetValue(inventoryId, out inventory))
                return InventoryTransactionResult.Reject(InventoryTransactionFailure.UnknownInventory);
            return InventoryTransactionResult.Success();
        }
    }
}
