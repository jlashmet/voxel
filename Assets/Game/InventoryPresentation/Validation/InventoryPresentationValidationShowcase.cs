using Game.Characters.Api;
using Game.Input.Api;
using Game.Input.Runtime;
using Game.Inventory.Api;
using Game.Inventory.Runtime;
using Game.InventoryPresentation.Api;
using Game.InventoryPresentation.Runtime;
using Game.Loot.Api;
using Game.Loot.Runtime;
using Game.WorldObjects.Api;
using UnityEngine;

namespace Game.InventoryPresentation.Validation
{
    public sealed class InventoryPresentationValidationShowcase : MonoBehaviour
    {
        private ItemRef _apple;
        private ItemRef _gem;
        private InventoryId _personal;
        private InventoryId _chest;
        private CharacterId _actor;
        private WorldObjectId _container;
        private InventoryRuntime _inventory;
        private LootRuntime _loot;
        private InputContextService _input;
        private InventoryPresenter _presenter;
        private IInputContextLease _uiLease;
        private PendingOperationId _transfer;
        private PendingOperationId _drop;
        private float _startedAt;
        private bool _pendingLogged;
        private bool _transferDone;
        private bool _dropQueued;
        private bool _dropDone;
        private bool _recreated;
        private bool _unwindLogged;
        private int _seedSequence;

        private void Start()
        {
            EnsureValidationCamera();
            _startedAt = Time.unscaledTime;
            _apple = new ItemRef("item:apple");
            _gem = new ItemRef("item:gem");
            _personal = new InventoryId("inventory:character");
            _chest = new InventoryId("inventory:chest");
            _actor = new CharacterId("character:validation");
            _container = new WorldObjectId("container:validation");

            _inventory = new InventoryRuntime(
                new[]
                {
                    new ItemDefinition(_apple, "Orchard Apple", "A"),
                    new ItemDefinition(_gem, "Moon Gem", "G")
                },
                new[]
                {
                    new InventoryDescriptor(_personal, new InventoryBindingMetadata("character", "character:validation")),
                    new InventoryDescriptor(_chest, new InventoryBindingMetadata("container", "container:validation"))
                });
            Seed(_personal, _apple, 6);
            Seed(_personal, _gem, 2);
            Seed(_chest, _apple, 3);

            var transactions = new InventoryTransactionsAdapter(_inventory, _inventory, _inventory);
            _loot = new LootRuntime(transactions, new AllowAllInteractions());
            _input = new InputContextService();
            CreatePresenter();
            _transfer = _presenter.QueueTransfer(new InventoryTransferIntent(
                new ContainerTransferRequest(_actor, _container, _personal, _chest, _apple, 2)));
            Debug.Log("INVENTORY_PRESENTATION_VALIDATION ready: personal=6 chest=3 input=" + _input.ActiveContext);
        }

        private void Update()
        {
            float elapsed = Time.unscaledTime - _startedAt;
            if (!_pendingLogged && elapsed >= 1f)
            {
                Debug.Log("INVENTORY_PRESENTATION_VALIDATION pending-stable: personal=" + Quantity(_personal, _apple));
                _pendingLogged = true;
            }
            if (!_transferDone && elapsed >= 3f)
            {
                if (!_presenter.Execute(_transfer)) Fail("transfer rejected");
                Debug.Log("INVENTORY_PRESENTATION_VALIDATION transfer-committed: personal=" + Quantity(_personal, _apple) + " chest=" + Quantity(_chest, _apple));
                _transferDone = true;
            }
            if (!_dropQueued && elapsed >= 5f)
            {
                _drop = _presenter.QueueDrop(new InventoryDropIntent(new DropRequest(
                    _actor,
                    _container,
                    new WorldObjectId("loot:validation-gem"),
                    _personal,
                    new LootPayload(_gem, 1))));
                Debug.Log("INVENTORY_PRESENTATION_VALIDATION drop-pending: gems=" + Quantity(_personal, _gem));
                _dropQueued = true;
            }
            if (!_dropDone && elapsed >= 6f)
            {
                if (!_presenter.Execute(_drop)) Fail("drop rejected");
                Debug.Log("INVENTORY_PRESENTATION_VALIDATION drop-committed: gems=" + Quantity(_personal, _gem));
                _dropDone = true;
            }
            if (!_recreated && elapsed >= 7f)
            {
                _uiLease.Dispose();
                CreatePresenter();
                _presenter.RebuildFromAuthoritative();
                Debug.Log("INVENTORY_PRESENTATION_VALIDATION recreate-stable: personal=" + Quantity(_personal, _apple) + " gems=" + Quantity(_personal, _gem));
                _recreated = true;
            }
            if (!_unwindLogged && elapsed >= 8f)
            {
                IInputContextLease nested = _presenter.OpenUi();
                nested.Dispose();
                Debug.Log("INVENTORY_PRESENTATION_VALIDATION nested-unwind: active=" + _input.ActiveContext);
                _unwindLogged = true;
            }
        }

        private void OnDestroy()
        {
            if (_uiLease != null) _uiLease.Dispose();
        }

        private void OnGUI()
        {
            if (_presenter == null) return;
            InventoryPresentationSnapshot snapshot = _presenter.Capture();
            GUI.Box(new Rect(30, 24, 1220, 640), string.Empty);
            GUI.Label(new Rect(55, 42, 1140, 36), "AUTHORITATIVE INVENTORY  •  Presentation-only UI");
            GUI.Label(new Rect(55, 78, 1140, 28), "Ui context: " + _input.ActiveContext + "   Pending operations: " + snapshot.Operations.Count);

            for (var p = 0; p < snapshot.Panels.Count; p++)
            {
                InventoryPanelPresentation panel = snapshot.Panels[p];
                float x = 55 + p * 580;
                GUI.Box(new Rect(x, 120, 540, 430), string.Empty);
                GUI.Label(new Rect(x + 24, 140, 490, 32), panel.BindingKind.ToUpperInvariant() + "  •  " + panel.StableOwnerId);
                GUI.Label(new Rect(x + 24, 174, 490, 24), "Revision " + panel.Revision + "   Inventory " + panel.InventoryId.Value);
                for (var r = 0; r < panel.Rows.Count; r++)
                {
                    InventoryRowPresentation row = panel.Rows[r];
                    float y = 220 + r * 58;
                    GUI.Label(new Rect(x + 30, y, 450, 28), row.IconText + "   " + row.DisplayName);
                    GUI.Label(new Rect(x + 350, y, 150, 28), "x " + row.Quantity);
                }
            }

            float operationY = 570;
            for (var i = 0; i < snapshot.Operations.Count; i++)
            {
                PendingOperationPresentation operation = snapshot.Operations[i];
                GUI.Label(new Rect(55 + i * 380, operationY, 360, 28), operation.Kind + "  " + operation.Status + (string.IsNullOrEmpty(operation.Error) ? string.Empty : "  " + operation.Error));
            }
        }

        private void CreatePresenter()
        {
            _presenter = new InventoryPresenter(_inventory, _loot, _input);
            _presenter.ShowInventories(new[] { _personal, _chest });
            _uiLease = _presenter.OpenUi();
        }

        private void Seed(InventoryId inventoryId, ItemRef item, int quantity)
        {
            _seedSequence++;
            InventoryTransactionResult result = _inventory.Add(new InventoryAddRequest(
                new InventoryTransactionId("validation-seed:" + _seedSequence), inventoryId, item, quantity));
            if (!result.Succeeded) Fail("seed rejected: " + result.FailureReason);
        }

        private int Quantity(InventoryId inventoryId, ItemRef item) => _inventory.Count(inventoryId, item);

        private static void Fail(string message)
        {
            Debug.LogError("INVENTORY_PRESENTATION_VALIDATION failure: " + message);
        }

        private static void EnsureValidationCamera()
        {
            if (Camera.main != null) return;
            var cameraObject = new GameObject("Inventory Presentation Validation Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.045f, 0.035f, 1f);
        }

        private sealed class AllowAllInteractions : IWorldInteractionValidator
        {
            public WorldInteractionResult Validate(CharacterId actorId, WorldObjectId objectId) => WorldInteractionResult.Success();
        }
    }
}
