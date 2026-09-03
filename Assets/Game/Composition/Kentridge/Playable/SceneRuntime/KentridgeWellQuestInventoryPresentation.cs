using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Composition.Kentridge.Api;
using Game.Composition.Kentridge.Runtime;
using Game.Input.Api;
using Game.Input.Runtime;
using Game.Inventory.Api;
using Game.Quests.Api;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Kentridge;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Kentridge.PlayableSlice
{
    /// <summary>
    /// Thin player-facing presentation for the recovered well quest. Quest/reward/item state stays
    /// in the Kentridge campaign session; this component owns generated-well proximity, prompts and
    /// the read-only square-tile inventory drawing only.
    /// </summary>
    public sealed class KentridgeWellQuestInventoryPresentation : MonoBehaviour
    {
        public const float ItemTileSizePixels = 64f;
        private const float WellInteractionRangeMetres = 3.2f;

        private static readonly FieldInfo SessionField = typeof(KentridgePlayableSlice).GetField(
            "_session", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo SettlementField = typeof(KentridgePlayableSlice).GetField(
            "_kentridgePlan", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly IInputContextService _inputContexts = new InputContextService();
        private KentridgeCampaignSession _session;
        private IInventoryQuery _inventory;
        private InventoryId _inventoryId;
        private IInputContextLease _uiLease;
        private Vector3 _wellWorldPosition;
        private bool _inventoryOpen;
        private string _statusMessage = string.Empty;

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
        public InputContextId ActiveInputContext => _inputContexts.ActiveContext;
        public bool QuestCompleted =>
            _session != null && _session.Runtime.IsQuestCompleted(KentridgeWellQuestDefinition.Ref);
        public Vector3 WellWorldPosition => _wellWorldPosition;

        private static ItemRef RewardRef => new ItemRef(KentridgeWellQuestDefinition.RewardItemId);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallForKentridge()
        {
            if (!string.Equals(
                    SceneManager.GetActiveScene().name,
                    "KentridgePlayableSlice",
                    StringComparison.Ordinal))
                return;
            if (FindFirstObjectByType<KentridgeWellQuestInventoryPresentation>() != null)
                return;

            var host = new GameObject("Kentridge Well Quest + Inventory");
            host.AddComponent<KentridgeWellQuestInventoryPresentation>();
        }

        private void OnDestroy()
        {
            CloseInventory();
        }

        private void Update()
        {
            BindLiveSessionIfReady();
            if (_inventory == null) return;

            if (Input.GetKeyDown(KeyCode.I))
                ToggleInventory();
            if (_inventoryOpen && Input.GetKeyDown(KeyCode.Escape))
                CloseInventory();

            if (_session != null && _session.SynchronizeRewards())
                _statusMessage = "Madeline: Thank you. Keep this Well Rescue Token.";

            if (_inventoryOpen)
            {
                // Kentridge still has a legacy direct UnityEngine.Input exploration reader. The
                // canonical Ui lease is authoritative for new readers; clearing axes bridges that
                // legacy reader until the slice is fully migrated to Game.Input.Runtime.
                Input.ResetInputAxes();
                return;
            }
            if (_session == null || _session.Runtime.HasActiveCutscene) return;

            if (Input.GetKeyDown(KeyCode.E) && IsPlayerNearWell())
                TryInteractWithWell();
        }

        public void SetInventory(IInventoryQuery inventory, InventoryId inventoryId)
        {
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            if (!inventoryId.IsValid) throw new ArgumentException("Inventory id is required.", nameof(inventoryId));
            _inventoryId = inventoryId;
        }

        public void ToggleInventory()
        {
            if (_inventoryOpen) CloseInventory();
            else OpenInventory();
        }

        public void OpenInventory()
        {
            if (_inventoryOpen) return;
            _uiLease = _inputContexts.Push(InputContextId.Ui);
            _inventoryOpen = true;
        }

        public void CloseInventory()
        {
            _inventoryOpen = false;
            _uiLease?.Dispose();
            _uiLease = null;
        }

        public bool TryInteractWithWell()
        {
            if (_session == null || !_session.Runtime.IsQuestActive(KentridgeWellQuestDefinition.Ref))
                return false;

            QuestSnapshot snapshot = _session.Runtime.GetQuestSnapshot(KentridgeWellQuestDefinition.Ref);
            if (!HasActiveTarget(snapshot, KentridgeWellQuestDefinition.WellTargetId))
                return false;

            IReadOnlyList<QuestEvent> events = _session.ObserveQuest(
                QuestObservation.Interacted(KentridgeWellQuestDefinition.WellTargetId));
            if (events.Count == 0) return false;

            _statusMessage = "You lower a rope into the old well. The boy climbs out safely. Return to Madeline.";
            return true;
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

        private void BindLiveSessionIfReady()
        {
            if (_session != null || SessionField == null || SettlementField == null) return;
            KentridgePlayableSlice slice = FindFirstObjectByType<KentridgePlayableSlice>();
            if (slice == null) return;

            var live = SessionField.GetValue(slice) as KentridgeCampaignSession;
            var settlement = SettlementField.GetValue(slice) as SettlementPlan;
            if (live == null || settlement == null) return;

            _session = live;
            _inventory = live.Inventory;
            _inventoryId = live.PlayerInventoryId;
            _wellWorldPosition = ResolveWellWorldPosition(settlement);
            _session.SynchronizeRewards();
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
            for (var i = 0; i < snapshot.Steps.Count; i++)
                if (snapshot.Steps[i].Status == QuestStepStatus.Active
                    && string.Equals(snapshot.Steps[i].TargetId, targetId, StringComparison.Ordinal))
                    return true;
            return false;
        }

        private string CurrentQuestText()
        {
            if (_session == null) return "Preparing Kentridge quest...";
            QuestSnapshot snapshot = _session.Runtime.GetQuestSnapshot(KentridgeWellQuestDefinition.Ref);
            if (snapshot.Status == QuestStatus.Inactive)
                return "Kid in the Well: waiting for the opening story.";
            if (snapshot.Status == QuestStatus.Completed)
                return "Kid in the Well: complete. Press I to view the reward.";
            if (HasActiveTarget(snapshot, KentridgeWellQuestDefinition.WellTargetId))
                return "Madeline: A boy fell into the old market well. Please help him.";
            return "The boy is safe. Return to Madeline and press E.";
        }

        private void OnGUI()
        {
            if (_inventory == null) return;

            GUI.Box(new Rect(18f, 18f, 520f, 62f), CurrentQuestText());
            if (!string.IsNullOrEmpty(_statusMessage))
                GUI.Label(new Rect(28f, 82f, 620f, 28f), _statusMessage);
            if (!_inventoryOpen && IsPlayerNearWell())
                GUI.Label(new Rect(28f, 110f, 360f, 28f), "E  Interact with the market well");

            if (!_inventoryOpen) return;

            const float panelWidth = 520f;
            const float panelHeight = 330f;
            Rect panel = new Rect(
                (Screen.width - panelWidth) * 0.5f,
                (Screen.height - panelHeight) * 0.5f,
                panelWidth,
                panelHeight);
            GUI.Box(panel, "Inventory   (I or Esc to close)");

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
