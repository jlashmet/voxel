using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Game.Inventory.Api;

namespace Game.Inventory.Runtime
{
    /// <summary>
    /// Stateless cross-domain adapter over the single authoritative Inventory runtime. It owns no item
    /// quantities: mutations delegate to IInventoryAuthority, reads to IInventoryQuery, and restore to
    /// IInventoryStatePort.
    /// </summary>
    public sealed class InventoryTransactionsAdapter : IInventoryTransactions
    {
        private readonly IInventoryAuthority _authority;
        private readonly IInventoryQuery _query;
        private readonly IInventoryStatePort _state;
        private long _transactionSequence;

        public InventoryTransactionsAdapter(
            IInventoryAuthority authority,
            IInventoryQuery query,
            IInventoryStatePort state)
        {
            _authority = authority ?? throw new ArgumentNullException(nameof(authority));
            _query = query ?? throw new ArgumentNullException(nameof(query));
            _state = state ?? throw new ArgumentNullException(nameof(state));
        }

        public InventoryTransactionResult TryAdd(InventoryId inventoryId, ItemRef item, int quantity) =>
            _authority.Add(new InventoryAddRequest(NextTransactionId(), inventoryId, item, quantity));

        public InventoryTransactionResult TryRemove(InventoryId inventoryId, ItemRef item, int quantity) =>
            _authority.Remove(new InventoryRemoveRequest(NextTransactionId(), inventoryId, item, quantity));

        public InventoryTransactionResult TryTransfer(
            InventoryId sourceInventoryId,
            InventoryId destinationInventoryId,
            ItemRef item,
            int quantity) =>
            _authority.Transfer(new InventoryTransferRequest(
                NextTransactionId(),
                sourceInventoryId,
                destinationInventoryId,
                item,
                quantity));

        public int Count(InventoryId inventoryId, ItemRef item) => _query.Count(inventoryId, item);

        public IReadOnlyList<InventoryQuantitySnapshot> Capture()
        {
            IReadOnlyList<InventorySnapshot> inventories = _query.GetAllSnapshots();
            var flattened = new List<InventoryQuantitySnapshot>();
            for (var i = 0; i < inventories.Count; i++)
            {
                InventorySnapshot inventory = inventories[i];
                for (var j = 0; j < inventory.Entries.Count; j++)
                {
                    InventoryEntry entry = inventory.Entries[j];
                    flattened.Add(new InventoryQuantitySnapshot(inventory.Id, entry.Item, entry.Quantity));
                }
            }
            return flattened.ToArray();
        }

        public bool TryRestore(IReadOnlyList<InventoryQuantitySnapshot> snapshots)
        {
            if (snapshots == null) return false;

            IReadOnlyList<InventorySnapshot> current = _query.GetAllSnapshots();
            var entriesByInventory = new Dictionary<InventoryId, List<InventoryEntry>>();
            for (var i = 0; i < current.Count; i++)
                entriesByInventory.Add(current[i].Id, new List<InventoryEntry>());

            for (var i = 0; i < snapshots.Count; i++)
            {
                InventoryQuantitySnapshot snapshot = snapshots[i];
                List<InventoryEntry> entries;
                if (!snapshot.InventoryId.IsValid ||
                    !snapshot.Item.IsValid ||
                    snapshot.Quantity <= 0 ||
                    !entriesByInventory.TryGetValue(snapshot.InventoryId, out entries))
                    return false;

                ItemDefinition ignored;
                if (!_query.TryGetDefinition(snapshot.Item, out ignored)) return false;
                for (var j = 0; j < entries.Count; j++)
                    if (entries[j].Item == snapshot.Item)
                        return false;
                entries.Add(new InventoryEntry(snapshot.Item, snapshot.Quantity));
            }

            var restored = new InventorySnapshot[current.Count];
            for (var i = 0; i < current.Count; i++)
            {
                InventoryId id = current[i].Id;
                List<InventoryEntry> entries = entriesByInventory[id];
                entries.Sort((left, right) => left.Item.CompareTo(right.Item));
                restored[i] = new InventorySnapshot(id, 0UL, entries.ToArray());
            }

            return _state.RestoreState(new InventoryStateCapture(restored)) == InventoryFailureReason.None;
        }

        private InventoryTransactionId NextTransactionId()
        {
            long sequence = Interlocked.Increment(ref _transactionSequence);
            return new InventoryTransactionId(
                "loot-adapter:" + sequence.ToString(CultureInfo.InvariantCulture));
        }
    }
}
