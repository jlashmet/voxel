using System;
using Game.Composition.Kentridge.Api;
using Game.Inventory.Api;
using Game.Quests.Api;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Kentridge;
using UnityEngine;

namespace Game.Kentridge.PlayableSlice
{
    /// <summary>
    /// Read-only player-facing presentation for the recovered well quest and inventory.
    /// Production composition must bind the canonical inventory and quest query capabilities;
    /// this component never discovers, creates, or mutates gameplay/session authority.
    /// </summary>
    public sealed class KentridgeWellQuestInventoryPresentation : MonoBehaviour
    {
        public const float ItemTileSizePixels = 64f;
        private const float WellInteractionRangeMetres = 3.2f;

        private IInventoryQuery _inventory;
        private InventoryId _inventoryId;
        private Func<QuestSnapshot> _questSnapshotProvider;
        private Vector3 _wellWorldPosition;
        private bool _inventoryOpen;
        private string _statusMessage = string.Empty;

        public bool IsBound => _inventory != null && _questSnapshotProvider != null;
        public bool InventoryOpen => _inventoryOpen;
        public int VisibleTileCount
        {
            get
            {
                InventorySnapshot snapshot;
                return _inventory != null && _inventory.TryGetSnapshot(_inventoryId, out snapshot)
                    ? snapshot.Entries.Count
                    : 0;
            }
        }
        public int RewardCount => _inventory?.Count(_inventoryId, RewardRef) ?? 0;
        public bool QuestCompleted
        {
            get
            {
                QuestSnapshot snapshot = TryGetQuestSnapshot();
                return snapshot != null && snapshot.Status == QuestStatus.Completed;
            }
        }
        public Vector3 WellWorldPosition => _wellWorldPosition;

        private static ItemRef RewardRef => new ItemRef(KentridgeWellQuestDefinition.RewardItemId);

        /// <summary>
        /// Binds read-only production capabilities supplied by the canonical composition root.
        /// No fallback lookup or local service construction is permitted here.
        /// </summary>
        public void BindReadModel(
            IInventoryQuery inventory,
            InventoryId inventoryId,
            Func<QuestSnapshot> questSnapshotProvider,
            Vector3 wellWorldPosition)
        {
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            if (!inventoryId.IsValid)
                throw new ArgumentException("Inventory id is required.", nameof(inventoryId));
            _questSnapshotProvider = questSnapshotProvider
                ?? throw new ArgumentNullException(nameof(questSnapshotProvider));
            _inventoryId = inventoryId;
            _wellWorldPosition = wellWorldPosition;
        }

        public void ToggleInventory()
        {
            if (_inventoryOpen) CloseInventory();
            else OpenInventory();
        }

        public void OpenInventory()
        {
            if (!IsBound)
                throw new InvalidOperationException(
                    "Kentridge well/inventory presentation requires production read-model composition before use.");
            _inventoryOpen = true;
        }

        public void CloseInventory()
        {
            _inventoryOpen = false;
        }

        public void SetStatusMessage(string message)
        {
            _statusMessage = message ?? string.Empty;
        }

        public static Vector3 ResolveWellWorldPosition(uint seed)
        {
            return ResolveWellWorldPosition(KentridgeDefinition.Build(seed));
        }

        public static Vector3 ResolveWellWorldPosition(SettlementPlan settlement)
        {
            if (settlement == null) throw new ArgumentNullException(nameof(settlement));
            for (var i = 0; i < settlement.Plots.Count; i++)
            {
                BuildingPlot plot = settlement.Plots[i];
                if (plot.RoleId != (int)KentridgeRole.Well) continue;
                Int3 footprint = KentridgeDefinition.FootprintDm(plot.Archetype);
                return new Vector3(
                    (plot.PositionDm.X + footprint.X * 0.5f) * 0.1f,
                    0f,
                    (plot.PositionDm.Y + footprint.Z * 0.5f) * 0.1f);
            }
            throw new InvalidOperationException("Generated Kentridge contains no Well role plot.");
        }

        private QuestSnapshot TryGetQuestSnapshot()
        {
            return _questSnapshotProvider?.Invoke();
        }

        private bool IsPlayerNearWell()
        {
            Camera camera = Camera.main;
            if (camera == null) return false;
            Vector3 delta = camera.transform.position - _wellWorldPosition;
            delta.y = 0f;
            return delta.sqrMagnitude <= WellInteractionRangeMetres * WellInteractionRangeMetres;
        }

        private static bool HasActiveTarget(QuestSnapshot snapshot, string targetId)
        {
            if (snapshot == null) return false;
            for (var i = 0; i < snapshot.Steps.Count; i++)
                if (snapshot.Steps[i].Status == QuestStepStatus.Active
                    && string.Equals(snapshot.Steps[i].TargetId, targetId, StringComparison.Ordinal))
                    return true;
            return false;
        }

        private string CurrentQuestText()
        {
            QuestSnapshot snapshot = TryGetQuestSnapshot();
            if (snapshot == null)
                return "Kentridge quest presentation is not bound to production composition.";
            if (snapshot.Status == QuestStatus.Inactive)
                return "Kid in the Well: waiting for the opening story.";
            if (snapshot.Status == QuestStatus.Completed)
                return "Kid in the Well: complete. Open inventory to view the reward.";
            if (HasActiveTarget(snapshot, KentridgeWellQuestDefinition.WellTargetId))
                return "Madeline: A boy fell into the old market well. Please help him.";
            return "The boy is safe. Return to Madeline.";
        }

        private void OnGUI()
        {
            if (!IsBound) return;

            GUI.Box(new Rect(18f, 18f, 520f, 62f), CurrentQuestText());
            if (!string.IsNullOrEmpty(_statusMessage))
                GUI.Label(new Rect(28f, 82f, 620f, 28f), _statusMessage);
            if (!_inventoryOpen && IsPlayerNearWell())
                GUI.Label(new Rect(28f, 110f, 420f, 28f), "Interact with the market well");

            if (!_inventoryOpen) return;

            const float panelWidth = 520f;
            const float panelHeight = 330f;
            Rect panel = new Rect(
                (Screen.width - panelWidth) * 0.5f,
                (Screen.height - panelHeight) * 0.5f,
                panelWidth,
                panelHeight);
            GUI.Box(panel, "Inventory");

            InventorySnapshot snapshot;
            if (!_inventory.TryGetSnapshot(_inventoryId, out snapshot)) return;
            for (var i = 0; i < snapshot.Entries.Count; i++)
            {
                int column = i % 6;
                int row = i / 6;
                float x = panel.x + 26f + column * 78f;
                float y = panel.y + 48f + row * 92f;
                Rect tile = new Rect(x, y, ItemTileSizePixels, ItemTileSizePixels);
                InventoryEntry item = snapshot.Entries[i];
                ItemDefinition definition;
                if (!_inventory.TryGetDefinition(item.Item, out definition)) continue;
                GUI.Box(tile, definition.IconText);
                GUI.Label(new Rect(x, y + ItemTileSizePixels + 2f, 72f, 24f),
                    item.Quantity > 1
                        ? definition.DisplayName + " x" + item.Quantity
                        : definition.DisplayName);
            }
        }
    }
}
