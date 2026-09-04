using System;
using System.Collections.Generic;
using System.Globalization;
using Game.Input.Api;
using Game.Inventory.Api;
using Game.InventoryPresentation.Api;
using Game.Loot.Api;

namespace Game.InventoryPresentation.Runtime
{
    public sealed class InventoryPresenter
    {
        private sealed class PanelLocalState
        {
            public bool HasSelection;
            public ItemRef Selection;
            public string Filter = string.Empty;
            public InventorySortMode SortMode = InventorySortMode.ItemId;
            public bool SortAscending = true;
        }

        private sealed class OperationRecord
        {
            public PendingOperationId Id;
            public PendingOperationKind Kind;
            public PendingOperationStatus Status;
            public ContainerTransferRequest Transfer;
            public DropRequest Drop;
            public string Error = string.Empty;
        }

        private readonly IInventoryQuery _inventory;
        private readonly ILootRuntime _loot;
        private readonly IInputContextService _inputContexts;
        private readonly List<InventoryId> _visibleInventories = new List<InventoryId>();
        private readonly Dictionary<InventoryId, PanelLocalState> _local = new Dictionary<InventoryId, PanelLocalState>();
        private readonly Dictionary<PendingOperationId, OperationRecord> _operations = new Dictionary<PendingOperationId, OperationRecord>();
        private long _nextOperationId;

        public InventoryPresenter(IInventoryQuery inventory, ILootRuntime loot, IInputContextService inputContexts)
        {
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _loot = loot ?? throw new ArgumentNullException(nameof(loot));
            _inputContexts = inputContexts ?? throw new ArgumentNullException(nameof(inputContexts));
        }

        public IInputContextLease OpenUi() => _inputContexts.Push(InputContextId.Ui);

        public void ShowInventories(IReadOnlyList<InventoryId> inventoryIds)
        {
            if (inventoryIds == null) throw new ArgumentNullException(nameof(inventoryIds));
            _visibleInventories.Clear();
            for (var i = 0; i < inventoryIds.Count; i++)
            {
                InventoryId id = inventoryIds[i];
                if (!id.IsValid) continue;
                bool duplicate = false;
                for (var j = 0; j < _visibleInventories.Count; j++)
                    if (_visibleInventories[j] == id) { duplicate = true; break; }
                if (!duplicate) _visibleInventories.Add(id);
                GetLocal(id);
            }
        }

        public bool Select(InventoryRowKey row)
        {
            InventorySnapshot snapshot;
            if (!_inventory.TryGetSnapshot(row.InventoryId, out snapshot) || snapshot.Count(row.Item) <= 0)
                return false;
            PanelLocalState state = GetLocal(row.InventoryId);
            state.HasSelection = true;
            state.Selection = row.Item;
            return true;
        }

        public void ClearSelection(InventoryId inventoryId)
        {
            PanelLocalState state = GetLocal(inventoryId);
            state.HasSelection = false;
            state.Selection = default;
        }

        public void SetFilter(InventoryId inventoryId, string filter) => GetLocal(inventoryId).Filter = filter == null ? string.Empty : filter.Trim();

        public void SetSort(InventoryId inventoryId, InventorySortMode mode, bool ascending)
        {
            PanelLocalState state = GetLocal(inventoryId);
            state.SortMode = mode;
            state.SortAscending = ascending;
        }

        public PendingOperationId QueueTransfer(InventoryTransferIntent intent)
        {
            PendingOperationId id = NextOperationId();
            _operations.Add(id, new OperationRecord
            {
                Id = id,
                Kind = PendingOperationKind.Transfer,
                Status = PendingOperationStatus.Pending,
                Transfer = intent.Request
            });
            return id;
        }

        public PendingOperationId QueueDrop(InventoryDropIntent intent)
        {
            PendingOperationId id = NextOperationId();
            _operations.Add(id, new OperationRecord
            {
                Id = id,
                Kind = PendingOperationKind.Drop,
                Status = PendingOperationStatus.Pending,
                Drop = intent.Request
            });
            return id;
        }

        public bool Execute(PendingOperationId id)
        {
            OperationRecord operation;
            if (!_operations.TryGetValue(id, out operation) || operation.Status != PendingOperationStatus.Pending)
                return false;

            LootTransferResult result = operation.Kind == PendingOperationKind.Transfer
                ? _loot.TryContainerTransfer(operation.Transfer)
                : _loot.TryDrop(operation.Drop);

            operation.Status = result.Succeeded ? PendingOperationStatus.Succeeded : PendingOperationStatus.Rejected;
            operation.Error = result.Succeeded
                ? string.Empty
                : result.Failure.ToString() + (result.InventoryFailure == InventoryFailureReason.None ? string.Empty : ":" + result.InventoryFailure);
            return result.Succeeded;
        }

        public bool DismissOperation(PendingOperationId id) => _operations.Remove(id);

        public void RebuildFromAuthoritative()
        {
            _operations.Clear();
            var ids = new List<InventoryId>(_local.Keys);
            for (var i = 0; i < ids.Count; i++)
            {
                PanelLocalState state = _local[ids[i]];
                if (!state.HasSelection) continue;
                InventorySnapshot snapshot;
                if (!_inventory.TryGetSnapshot(ids[i], out snapshot) || snapshot.Count(state.Selection) <= 0)
                {
                    state.HasSelection = false;
                    state.Selection = default;
                }
            }
        }

        public InventoryPresentationSnapshot Capture()
        {
            IReadOnlyList<InventorySnapshot> all = _inventory.GetAllSnapshots();
            var panels = new List<InventoryPanelPresentation>();

            if (_visibleInventories.Count == 0)
            {
                for (var i = 0; i < all.Count; i++) panels.Add(Project(all[i]));
            }
            else
            {
                for (var i = 0; i < _visibleInventories.Count; i++)
                {
                    InventorySnapshot snapshot;
                    if (_inventory.TryGetSnapshot(_visibleInventories[i], out snapshot)) panels.Add(Project(snapshot));
                }
            }

            var operations = new List<PendingOperationPresentation>(_operations.Count);
            foreach (OperationRecord operation in _operations.Values) operations.Add(Project(operation));
            operations.Sort((left, right) => left.Id.CompareTo(right.Id));
            return new InventoryPresentationSnapshot(panels.ToArray(), operations.ToArray());
        }

        private InventoryPanelPresentation Project(InventorySnapshot snapshot)
        {
            PanelLocalState local = GetLocal(snapshot.Id);
            var rows = new List<InventoryRowPresentation>(snapshot.Entries.Count);
            for (var i = 0; i < snapshot.Entries.Count; i++)
            {
                InventoryEntry entry = snapshot.Entries[i];
                ItemDefinition definition;
                string displayName = entry.Item.Id;
                string icon = "?";
                if (_inventory.TryGetDefinition(entry.Item, out definition))
                {
                    displayName = definition.DisplayName;
                    icon = definition.IconText;
                }
                if (!Matches(local.Filter, entry.Item.Id, displayName)) continue;
                rows.Add(new InventoryRowPresentation(new InventoryRowKey(snapshot.Id, entry.Item), displayName, icon, entry.Quantity));
            }

            rows.Sort((left, right) => CompareRows(left, right, local));
            bool hasSelection = local.HasSelection && snapshot.Count(local.Selection) > 0;
            InventoryRowKey selection = hasSelection ? new InventoryRowKey(snapshot.Id, local.Selection) : default;

            InventoryDescriptor descriptor;
            string kind = string.Empty;
            string owner = string.Empty;
            if (_inventory.TryGetDescriptor(snapshot.Id, out descriptor))
            {
                kind = descriptor.Binding.Kind;
                owner = descriptor.Binding.StableOwnerId;
            }

            return new InventoryPanelPresentation(
                snapshot.Id,
                snapshot.Revision,
                kind,
                owner,
                rows.ToArray(),
                hasSelection,
                selection,
                local.Filter,
                local.SortMode,
                local.SortAscending);
        }

        private static bool Matches(string filter, string itemId, string displayName)
        {
            if (string.IsNullOrWhiteSpace(filter)) return true;
            return (itemId ?? string.Empty).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   (displayName ?? string.Empty).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int CompareRows(InventoryRowPresentation left, InventoryRowPresentation right, PanelLocalState local)
        {
            int result;
            switch (local.SortMode)
            {
                case InventorySortMode.DisplayName:
                    result = StringComparer.OrdinalIgnoreCase.Compare(left.DisplayName, right.DisplayName);
                    break;
                case InventorySortMode.Quantity:
                    result = left.Quantity.CompareTo(right.Quantity);
                    break;
                default:
                    result = left.Key.Item.CompareTo(right.Key.Item);
                    break;
            }
            if (result == 0) result = left.Key.CompareTo(right.Key);
            return local.SortAscending ? result : -result;
        }

        private PendingOperationPresentation Project(OperationRecord operation)
        {
            if (operation.Kind == PendingOperationKind.Transfer)
            {
                return new PendingOperationPresentation(
                    operation.Id,
                    operation.Kind,
                    operation.Status,
                    operation.Transfer.SourceInventoryId,
                    operation.Transfer.DestinationInventoryId,
                    operation.Transfer.Item,
                    operation.Transfer.Quantity,
                    operation.Error);
            }

            return new PendingOperationPresentation(
                operation.Id,
                operation.Kind,
                operation.Status,
                operation.Drop.SourceInventoryId,
                default,
                operation.Drop.Payload.Item,
                operation.Drop.Payload.Quantity,
                operation.Error);
        }

        private PanelLocalState GetLocal(InventoryId inventoryId)
        {
            PanelLocalState state;
            if (_local.TryGetValue(inventoryId, out state)) return state;
            state = new PanelLocalState();
            _local.Add(inventoryId, state);
            return state;
        }

        private PendingOperationId NextOperationId()
        {
            _nextOperationId++;
            return new PendingOperationId("inventory-ui:" + _nextOperationId.ToString(CultureInfo.InvariantCulture));
        }
    }
}
