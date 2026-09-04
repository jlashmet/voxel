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
    /// <summary>
    /// Thin module-local validation composition. Runtime behavior and visible inventory realization come from
    /// InventoryPresenter + InventoryPresentationView; this component only supplies deterministic authoritative
    /// fixtures and timed interaction intents for the shared built-player harness.
    /// </summary>
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
        private InventoryPresentationView _view;
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
            CreatePresenterAndView();
            _transfer = _presenter.QueueTransfer(new InventoryTransferIntent(
                new ContainerTransferRequest(_actor, _container, _personal, _chest, _apple, 2)));
            Debug.Log("INVENTORY_PRESENTATION_VALIDATION production-view-bound: " + _view.IsBound);
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
                DestroyPresentationView();
                CreatePresenterAndView();
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
            if (_view != null) _view.Unbind();
        }

        private void CreatePresenterAndView()
        {
            _presenter = new InventoryPresenter(_inventory, _loot, _input);
            _presenter.ShowInventories(new[] { _personal, _chest });

            var viewObject = new GameObject("Inventory Presentation Production View");
            viewObject.transform.SetParent(transform, false);
            _view = viewObject.AddComponent<InventoryPresentationView>();
            _view.Bind(_presenter);
        }

        private void DestroyPresentationView()
        {
            if (_view == null) return;
            GameObject viewObject = _view.gameObject;
            _view.Unbind();
            _view = null;
            Destroy(viewObject);
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
