using System;
using System.Collections.Generic;
using System.Globalization;
using Game.Inventory.Api;

namespace Game.Inventory.Runtime
{
    /// <summary>
    /// Authoritative deterministic inventory service. All quantity changes are serialized through
    /// transaction ids; internal quantity dictionaries never escape this assembly.
    /// </summary>
    public sealed class InventoryRuntime : IInventoryQuery, IInventoryAuthority, IInventoryStatePort
    {
        private sealed class InventoryState
        {
            public InventoryDescriptor Descriptor;
            public ulong Revision;
            public readonly Dictionary<ItemRef, int> Quantities = new Dictionary<ItemRef, int>();
        }

        private sealed class JournalEntry
        {
            public string Fingerprint;
            public InventoryTransactionResult Result;
        }

        private readonly object _gate = new object();
        private readonly Dictionary<ItemRef, ItemDefinition> _definitions =
            new Dictionary<ItemRef, ItemDefinition>();
        private readonly Dictionary<InventoryId, InventoryDescriptor> _descriptors =
            new Dictionary<InventoryId, InventoryDescriptor>();
        private Dictionary<InventoryId, InventoryState> _states =
            new Dictionary<InventoryId, InventoryState>();
        private readonly Dictionary<InventoryTransactionId, JournalEntry> _journal =
            new Dictionary<InventoryTransactionId, JournalEntry>();

        public event Action<InventoryChangeEvent> Changed;

        public InventoryRuntime(
            IReadOnlyList<ItemDefinition> definitions,
            IReadOnlyList<InventoryDescriptor> inventories)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            if (inventories == null) throw new ArgumentNullException(nameof(inventories));

            for (var i = 0; i < definitions.Count; i++)
            {
                ItemDefinition definition = definitions[i]
                    ?? throw new InvalidOperationException(
                        "Inventory definition collection contains null at index " + i + ".");
                if (_definitions.ContainsKey(definition.Ref))
                    throw new InvalidOperationException(
                        "Inventory definition collection contains duplicate item '" + definition.Ref + "'.");
                _definitions.Add(definition.Ref, definition);
            }

            for (var i = 0; i < inventories.Count; i++)
            {
                InventoryDescriptor descriptor = inventories[i];
                if (!descriptor.Id.IsValid || !descriptor.Binding.IsValid)
                    throw new InvalidOperationException(
                        "Inventory descriptor collection contains an invalid descriptor at index " + i + ".");
                if (_descriptors.ContainsKey(descriptor.Id))
                    throw new InvalidOperationException(
                        "Inventory descriptor collection contains duplicate inventory '" + descriptor.Id + "'.");
                _descriptors.Add(descriptor.Id, descriptor);
                _states.Add(descriptor.Id, new InventoryState { Descriptor = descriptor, Revision = 0UL });
            }
        }

        public bool TryGetDescriptor(InventoryId inventoryId, out InventoryDescriptor descriptor)
        {
            lock (_gate)
                return _descriptors.TryGetValue(inventoryId, out descriptor);
        }

        public bool TryGetDefinition(ItemRef item, out ItemDefinition definition)
        {
            lock (_gate)
                return _definitions.TryGetValue(item, out definition);
        }

        public bool TryGetSnapshot(InventoryId inventoryId, out InventorySnapshot snapshot)
        {
            lock (_gate)
            {
                InventoryState state;
                if (!_states.TryGetValue(inventoryId, out state))
                {
                    snapshot = default;
                    return false;
                }
                snapshot = SnapshotOf(state);
                return true;
            }
        }

        public int Count(InventoryId inventoryId, ItemRef item)
        {
            lock (_gate)
            {
                InventoryState state;
                if (!_states.TryGetValue(inventoryId, out state)) return 0;
                int quantity;
                return state.Quantities.TryGetValue(item, out quantity) ? quantity : 0;
            }
        }

        public IReadOnlyList<InventorySnapshot> GetAllSnapshots()
        {
            lock (_gate)
            {
                var ids = new List<InventoryId>(_states.Keys);
                ids.Sort();
                var snapshots = new InventorySnapshot[ids.Count];
                for (var i = 0; i < ids.Count; i++)
                    snapshots[i] = SnapshotOf(_states[ids[i]]);
                return snapshots;
            }
        }

        public InventoryTransactionResult Add(InventoryAddRequest request)
        {
            InventoryChangeEvent[] committed = null;
            InventoryTransactionResult result;
            lock (_gate)
            {
                string fingerprint = Fingerprint(
                    InventoryMutationKind.Add,
                    request.InventoryId.Value,
                    string.Empty,
                    request.Item.Id,
                    request.Quantity);
                result = ResolveJournaled(
                    request.TransactionId,
                    InventoryMutationKind.Add,
                    fingerprint,
                    () => ExecuteAdd(request),
                    out committed);
            }
            Publish(committed);
            return result;
        }

        public InventoryTransactionResult Remove(InventoryRemoveRequest request)
        {
            InventoryChangeEvent[] committed = null;
            InventoryTransactionResult result;
            lock (_gate)
            {
                string fingerprint = Fingerprint(
                    InventoryMutationKind.Remove,
                    request.InventoryId.Value,
                    string.Empty,
                    request.Item.Id,
                    request.Quantity);
                result = ResolveJournaled(
                    request.TransactionId,
                    InventoryMutationKind.Remove,
                    fingerprint,
                    () => ExecuteRemove(request),
                    out committed);
            }
            Publish(committed);
            return result;
        }

        public InventoryTransactionResult Transfer(InventoryTransferRequest request)
        {
            InventoryChangeEvent[] committed = null;
            InventoryTransactionResult result;
            lock (_gate)
            {
                string fingerprint = Fingerprint(
                    InventoryMutationKind.Transfer,
                    request.SourceInventoryId.Value,
                    request.DestinationInventoryId.Value,
                    request.Item.Id,
                    request.Quantity);
                result = ResolveJournaled(
                    request.TransactionId,
                    InventoryMutationKind.Transfer,
                    fingerprint,
                    () => ExecuteTransfer(request),
                    out committed);
            }
            Publish(committed);
            return result;
        }

        public InventoryStateCapture CaptureState()
        {
            return new InventoryStateCapture(GetAllSnapshots());
        }

        public InventoryFailureReason RestoreState(InventoryStateCapture capture)
        {
            lock (_gate)
            {
                if (capture.Inventories == null || capture.Inventories.Count != _descriptors.Count)
                    return InventoryFailureReason.InvalidRestore;

                var restored = new Dictionary<InventoryId, InventoryState>();
                for (var i = 0; i < capture.Inventories.Count; i++)
                {
                    InventorySnapshot snapshot = capture.Inventories[i];
                    InventoryDescriptor descriptor;
                    if (!snapshot.Id.IsValid || !_descriptors.TryGetValue(snapshot.Id, out descriptor))
                        return InventoryFailureReason.InvalidRestore;
                    if (restored.ContainsKey(snapshot.Id))
                        return InventoryFailureReason.InvalidRestore;
                    if (snapshot.Entries == null)
                        return InventoryFailureReason.InvalidRestore;

                    var state = new InventoryState
                    {
                        Descriptor = descriptor,
                        Revision = snapshot.Revision
                    };
                    for (var j = 0; j < snapshot.Entries.Count; j++)
                    {
                        InventoryEntry entry = snapshot.Entries[j];
                        if (!entry.Item.IsValid || entry.Quantity <= 0 || !_definitions.ContainsKey(entry.Item))
                            return InventoryFailureReason.InvalidRestore;
                        if (state.Quantities.ContainsKey(entry.Item))
                            return InventoryFailureReason.InvalidRestore;
                        state.Quantities.Add(entry.Item, entry.Quantity);
                    }
                    restored.Add(snapshot.Id, state);
                }

                foreach (InventoryId id in _descriptors.Keys)
                    if (!restored.ContainsKey(id))
                        return InventoryFailureReason.InvalidRestore;

                _states = restored;
                _journal.Clear();
                return InventoryFailureReason.None;
            }
        }

        private InventoryTransactionResult ResolveJournaled(
            InventoryTransactionId transactionId,
            InventoryMutationKind kind,
            string fingerprint,
            Func<InventoryTransactionResult> execute,
            out InventoryChangeEvent[] committed)
        {
            committed = null;
            if (!transactionId.IsValid)
                return Failure(transactionId, kind, InventoryFailureReason.InvalidTransactionId);

            JournalEntry existing;
            if (_journal.TryGetValue(transactionId, out existing))
            {
                if (string.Equals(existing.Fingerprint, fingerprint, StringComparison.Ordinal))
                    return existing.Result;
                return Failure(transactionId, kind, InventoryFailureReason.TransactionConflict);
            }

            InventoryTransactionResult result = execute();
            _journal.Add(transactionId, new JournalEntry { Fingerprint = fingerprint, Result = result });
            if (result.Succeeded && result.Changes.Count > 0)
            {
                committed = new InventoryChangeEvent[result.Changes.Count];
                for (var i = 0; i < result.Changes.Count; i++) committed[i] = result.Changes[i];
            }
            return result;
        }

        private InventoryTransactionResult ExecuteAdd(InventoryAddRequest request)
        {
            InventoryFailureReason validation = ValidateSingle(request.InventoryId, request.Item, request.Quantity);
            if (validation != InventoryFailureReason.None)
                return FailureWithCurrent(request.TransactionId, InventoryMutationKind.Add, validation, request.InventoryId);

            InventoryState state = _states[request.InventoryId];
            int current;
            state.Quantities.TryGetValue(request.Item, out current);
            if (current > int.MaxValue - request.Quantity)
                return FailureWithCurrent(
                    request.TransactionId,
                    InventoryMutationKind.Add,
                    InventoryFailureReason.QuantityOverflow,
                    request.InventoryId);

            int next = current + request.Quantity;
            state.Quantities[request.Item] = next;
            state.Revision++;
            var change = new InventoryChangeEvent(
                request.TransactionId,
                InventoryMutationKind.Add,
                request.InventoryId,
                request.Item,
                request.Quantity,
                state.Revision);
            return Success(
                request.TransactionId,
                InventoryMutationKind.Add,
                SnapshotOf(state),
                false,
                default,
                new[] { change });
        }

        private InventoryTransactionResult ExecuteRemove(InventoryRemoveRequest request)
        {
            InventoryFailureReason validation = ValidateSingle(request.InventoryId, request.Item, request.Quantity);
            if (validation != InventoryFailureReason.None)
                return FailureWithCurrent(request.TransactionId, InventoryMutationKind.Remove, validation, request.InventoryId);

            InventoryState state = _states[request.InventoryId];
            int current;
            state.Quantities.TryGetValue(request.Item, out current);
            if (current < request.Quantity)
                return FailureWithCurrent(
                    request.TransactionId,
                    InventoryMutationKind.Remove,
                    InventoryFailureReason.InsufficientQuantity,
                    request.InventoryId);

            int next = current - request.Quantity;
            if (next == 0) state.Quantities.Remove(request.Item);
            else state.Quantities[request.Item] = next;
            state.Revision++;
            var change = new InventoryChangeEvent(
                request.TransactionId,
                InventoryMutationKind.Remove,
                request.InventoryId,
                request.Item,
                -request.Quantity,
                state.Revision);
            return Success(
                request.TransactionId,
                InventoryMutationKind.Remove,
                SnapshotOf(state),
                false,
                default,
                new[] { change });
        }

        private InventoryTransactionResult ExecuteTransfer(InventoryTransferRequest request)
        {
            if (!request.SourceInventoryId.IsValid || !request.DestinationInventoryId.IsValid)
                return Failure(request.TransactionId, InventoryMutationKind.Transfer, InventoryFailureReason.InvalidInventoryId);
            if (request.SourceInventoryId == request.DestinationInventoryId)
                return FailureWithCurrent(
                    request.TransactionId,
                    InventoryMutationKind.Transfer,
                    InventoryFailureReason.SameInventory,
                    request.SourceInventoryId);
            if (!request.Item.IsValid)
                return Failure(request.TransactionId, InventoryMutationKind.Transfer, InventoryFailureReason.InvalidItem);
            if (request.Quantity <= 0)
                return Failure(request.TransactionId, InventoryMutationKind.Transfer, InventoryFailureReason.InvalidQuantity);
            if (!_definitions.ContainsKey(request.Item))
                return Failure(request.TransactionId, InventoryMutationKind.Transfer, InventoryFailureReason.UnknownItem);

            InventoryState source;
            InventoryState destination;
            if (!_states.TryGetValue(request.SourceInventoryId, out source) ||
                !_states.TryGetValue(request.DestinationInventoryId, out destination))
                return Failure(request.TransactionId, InventoryMutationKind.Transfer, InventoryFailureReason.UnknownInventory);

            int sourceQuantity;
            source.Quantities.TryGetValue(request.Item, out sourceQuantity);
            if (sourceQuantity < request.Quantity)
                return Failure(
                    request.TransactionId,
                    InventoryMutationKind.Transfer,
                    InventoryFailureReason.InsufficientQuantity,
                    true,
                    SnapshotOf(source),
                    true,
                    SnapshotOf(destination));

            int destinationQuantity;
            destination.Quantities.TryGetValue(request.Item, out destinationQuantity);
            if (destinationQuantity > int.MaxValue - request.Quantity)
                return Failure(
                    request.TransactionId,
                    InventoryMutationKind.Transfer,
                    InventoryFailureReason.QuantityOverflow,
                    true,
                    SnapshotOf(source),
                    true,
                    SnapshotOf(destination));

            int sourceNext = sourceQuantity - request.Quantity;
            if (sourceNext == 0) source.Quantities.Remove(request.Item);
            else source.Quantities[request.Item] = sourceNext;
            destination.Quantities[request.Item] = destinationQuantity + request.Quantity;
            source.Revision++;
            destination.Revision++;

            var changes = new[]
            {
                new InventoryChangeEvent(
                    request.TransactionId,
                    InventoryMutationKind.Transfer,
                    source.Descriptor.Id,
                    request.Item,
                    -request.Quantity,
                    source.Revision),
                new InventoryChangeEvent(
                    request.TransactionId,
                    InventoryMutationKind.Transfer,
                    destination.Descriptor.Id,
                    request.Item,
                    request.Quantity,
                    destination.Revision)
            };
            return Success(
                request.TransactionId,
                InventoryMutationKind.Transfer,
                SnapshotOf(source),
                true,
                SnapshotOf(destination),
                changes);
        }

        private InventoryFailureReason ValidateSingle(InventoryId inventoryId, ItemRef item, int quantity)
        {
            if (!inventoryId.IsValid) return InventoryFailureReason.InvalidInventoryId;
            if (!item.IsValid) return InventoryFailureReason.InvalidItem;
            if (quantity <= 0) return InventoryFailureReason.InvalidQuantity;
            if (!_definitions.ContainsKey(item)) return InventoryFailureReason.UnknownItem;
            if (!_states.ContainsKey(inventoryId)) return InventoryFailureReason.UnknownInventory;
            return InventoryFailureReason.None;
        }

        private InventoryTransactionResult FailureWithCurrent(
            InventoryTransactionId transactionId,
            InventoryMutationKind kind,
            InventoryFailureReason reason,
            InventoryId inventoryId)
        {
            InventoryState state;
            return _states.TryGetValue(inventoryId, out state)
                ? Failure(transactionId, kind, reason, true, SnapshotOf(state), false, default)
                : Failure(transactionId, kind, reason);
        }

        private static InventoryTransactionResult Failure(
            InventoryTransactionId transactionId,
            InventoryMutationKind kind,
            InventoryFailureReason reason,
            bool hasSource = false,
            InventorySnapshot source = default,
            bool hasDestination = false,
            InventorySnapshot destination = default)
        {
            return new InventoryTransactionResult(
                transactionId,
                kind,
                reason,
                hasSource,
                source,
                hasDestination,
                destination,
                Array.Empty<InventoryChangeEvent>());
        }

        private static InventoryTransactionResult Success(
            InventoryTransactionId transactionId,
            InventoryMutationKind kind,
            InventorySnapshot source,
            bool hasDestination,
            InventorySnapshot destination,
            IReadOnlyList<InventoryChangeEvent> changes)
        {
            return new InventoryTransactionResult(
                transactionId,
                kind,
                InventoryFailureReason.None,
                true,
                source,
                hasDestination,
                destination,
                changes);
        }

        private static string Fingerprint(
            InventoryMutationKind kind,
            string source,
            string destination,
            string item,
            int quantity)
        {
            return ((int)kind).ToString(CultureInfo.InvariantCulture) + "|" +
                   Part(source) + Part(destination) + Part(item) +
                   quantity.ToString(CultureInfo.InvariantCulture);
        }

        private static string Part(string value)
        {
            value = value ?? string.Empty;
            return value.Length.ToString(CultureInfo.InvariantCulture) + ":" + value + "|";
        }

        private InventorySnapshot SnapshotOf(InventoryState state)
        {
            var items = new List<ItemRef>(state.Quantities.Keys);
            items.Sort();
            var entries = new InventoryEntry[items.Count];
            for (var i = 0; i < items.Count; i++)
                entries[i] = new InventoryEntry(items[i], state.Quantities[items[i]]);
            return new InventorySnapshot(state.Descriptor.Id, state.Revision, entries);
        }

        private void Publish(InventoryChangeEvent[] committed)
        {
            if (committed == null || committed.Length == 0) return;
            Action<InventoryChangeEvent> handler = Changed;
            if (handler == null) return;
            for (var i = 0; i < committed.Length; i++) handler(committed[i]);
        }
    }
}
