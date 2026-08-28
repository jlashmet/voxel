using System;
using System.Collections.Generic;
using Game.Composition.Kentridge.Api;
using Game.Composition.Kentridge.Runtime;
using Game.Inventory.Api;
using Game.Inventory.Runtime;
using Game.Quests.Api;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Kentridge;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Kentridge.PlayableSlice
{
    /// <summary>
    /// Thin player-facing presentation for the recovered well quest. Quest state stays in
    /// CampaignRuntime/QuestRuntime and item state stays in IInventoryRuntime; this component owns
    /// only generated-well proximity, input prompts, and the read-only inventory drawing.
    /// </summary>
    public sealed class KentridgeWellQuestInventoryPresentation : MonoBehaviour
    {
        public const float ItemTileSizePixels = 64f;
        private const uint DefaultKentridgeSeed = 0x4B454E54u;
        private const float WellInteractionRangeMetres = 3.2f;

        private KentridgeCampaignSession _session;
        private IInventoryRuntime _inventory;
        private Vector3 _wellWorldPosition;
        private bool _inventoryOpen;
        private string _statusMessage = string.Empty;

        public bool InventoryOpen => _inventoryOpen;
        public int VisibleTileCount => _inventory?.Snapshot().Count ?? 0;
        public int RewardCount => _inventory?.Count(RewardRef) ?? 0;
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
            if (_session != null)
                KentridgeCampaignSessionBootstrap.ClearActiveSession(_session);
        }

        private void Update()
        {
            BindLiveSessionIfReady();
            if (_inventory == null) return;

            if (Input.GetKeyDown(KeyCode.I))
                ToggleInventory();
            if (_inventoryOpen && Input.GetKeyDown(KeyCode.Escape))
                CloseInventory();

            SyncReward();
            if (_inventoryOpen || _session == null || _session.Runtime.HasActiveCutscene) return;

            if (Input.GetKeyDown(KeyCode.E) && IsPlayerNearWell())
                TryInteractWithWell();
        }

        public void SetInventory(IInventoryRuntime inventory)
        {
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        }

        public void ToggleInventory() => _inventoryOpen = !_inventoryOpen;
        public void CloseInventory() => _inventoryOpen = false;

        public bool TryInteractWithWell()
        {
            if (_session == null || !_session.Runtime.IsQuestActive(KentridgeWellQuestDefinition.Ref))
                return false;

            QuestSnapshot snapshot = _session.Runtime.GetQuestSnapshot(KentridgeWellQuestDefinition.Ref);
            if (!HasActiveTarget(snapshot, KentridgeWellQuestDefinition.WellTargetId))
                return false;

            IReadOnlyList<QuestEvent> events = _session.Runtime.ObserveQuest(
                QuestObservation.Interacted(KentridgeWellQuestDefinition.WellTargetId));
            if (events.Count == 0) return false;

            _statusMessage = "You lower a rope into the old well. The boy climbs out safely. Return to Madeline.";
            SyncReward();
            return true;
        }

        public static Vector3 ResolveWellWorldPosition(uint seed)
        {
            SettlementPlan settlement = KentridgeDefinition.Build(seed);
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
            KentridgeCampaignSession live = KentridgeCampaignSessionBootstrap.ActiveSession;
            if (live == null || ReferenceEquals(live, _session)) return;

            _session = live;
            _wellWorldPosition = ResolveWellWorldPosition(DefaultKentridgeSeed);
            SetInventory(new InventoryRuntime(new[]
            {
                new ItemDefinition(RewardRef, "Well Rescue Token", "W")
            }));
            SyncReward();
        }

        private void SyncReward()
        {
            if (_session == null || _inventory == null) return;
            if (!_session.Runtime.IsQuestCompleted(KentridgeWellQuestDefinition.Ref)) return;
            if (_inventory.TryAddUnique(RewardRef))
                _statusMessage = "Madeline: Thank you. Keep this Well Rescue Token.";
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

            IReadOnlyList<InventoryItemSnapshot> items = _inventory.Snapshot();
            for (var i = 0; i < items.Count; i++)
            {
                int column = i % 6;
                int row = i / 6;
                float x = panel.x + 26f + column * 78f;
                float y = panel.y + 48f + row * 92f;
                Rect tile = new Rect(x, y, ItemTileSizePixels, ItemTileSizePixels);
                InventoryItemSnapshot item = items[i];
                GUI.Box(tile, item.Definition.IconText);
                GUI.Label(new Rect(x, y + ItemTileSizePixels + 2f, 72f, 24f),
                    item.Quantity > 1
                        ? item.Definition.DisplayName + " x" + item.Quantity
                        : item.Definition.DisplayName);
            }
        }
    }
}
