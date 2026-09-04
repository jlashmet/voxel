using Game.Characters.Api;
using Game.Vfx.Api;
using Game.Vfx.Runtime;
using Game.Vitality.Api;
using Game.Vitality.Runtime;
using Game.WorldObjects.Api;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Edits.Api;

namespace Game.Vfx.Validation
{
    public sealed class SemanticVfxValidationShowcase : MonoBehaviour
    {
        private CharacterId _target;
        private VitalityRegistry _vitality;
        private SemanticVfxPresenter _presenter;
        private SceneVfxBindingResolver _bindings;
        private VfxCueCoordinator _coordinator;
        private SemanticVfxFeedbackAdapter _adapter;
        private float _startedAt;
        private bool _hitDone;
        private bool _defeatDone;
        private bool _interactionDone;
        private bool _destructionDone;
        private bool _rebuildDone;
        private string _phase = "Starting";
        private VfxSubmitResult _predicted;
        private VfxSubmitResult _confirmed;

        private void Start()
        {
            _startedAt = Time.unscaledTime;
            ConfigureCamera();

            GameObject presenterRoot = new GameObject("Production Semantic VFX Presenter");
            _presenter = presenterRoot.AddComponent<SemanticVfxPresenter>();

            // The effects are presentation bound to semantic origins. Give those origins neutral,
            // collider-free silhouettes so built-player visual review can judge the production VFX
            // relative to a representative host instead of against an empty sky.
            Transform fallback = CreateVisibleAnchor(
                "Encounter Result",
                new Vector3(0f, 1.2f, 5f),
                PrimitiveType.Sphere,
                new Vector3(0.45f, 0.45f, 0.45f),
                new Color(0.22f, 0.18f, 0.34f, 1f));
            Transform character = CreateVisibleAnchor(
                "Character Target",
                new Vector3(-2.4f, 1f, 4.4f),
                PrimitiveType.Capsule,
                new Vector3(0.8f, 1.45f, 0.8f),
                new Color(0.16f, 0.19f, 0.24f, 1f));
            Transform worldObject = CreateVisibleAnchor(
                "World Object",
                new Vector3(0f, 1f, 4.2f),
                PrimitiveType.Cube,
                new Vector3(0.9f, 1.35f, 0.9f),
                new Color(0.13f, 0.24f, 0.27f, 1f));
            _bindings = new SceneVfxBindingResolver(fallback)
                .BindCharacter("character:vfx-target", character)
                .BindWorldObject("altar:crystal", worldObject);

            _coordinator = new VfxCueCoordinator(VfxCueCatalog.CreateDefault(), _bindings, _presenter, _presenter);
            _target = new CharacterId("character:vfx-target");
            _vitality = new VitalityRegistry();
            _vitality.Register(VitalitySnapshot.Alive(_target, 100));
            _adapter = new SemanticVfxFeedbackAdapter(_coordinator, _vitality);

            Debug.Log("VFX_VALIDATION ready: semanticPresenter=1 gameplayPhysics=" + _presenter.CountGameplayPhysicsComponents());
        }

        private void Update()
        {
            float elapsed = Time.unscaledTime - _startedAt;
            if (!_hitDone && elapsed >= 1f)
            {
                var eventId = new VfxEventId("damage:" + _target.Value + ":1");
                _predicted = _coordinator.Submit(new VfxCueRequest(GameplayVfxCues.Hit, eventId,
                    VfxSemanticOrigin.Character(_target), VfxCuePhase.Predicted));
                DamageResult damage = _vitality.ApplyDamage(new DamageRequest(_target, 20));
                _confirmed = _adapter.OnDamageConfirmed(damage);
                _phase = "Predicted hit reconciled to confirmed damage";
                Debug.Log("VFX_VALIDATION dedupe: predicted=" + _predicted + " confirmed=" + _confirmed + " plays=" + _presenter.OneShotPlayCount);
                _hitDone = true;
            }

            if (!_defeatDone && elapsed >= 3f)
            {
                DamageResult defeat = _vitality.ApplyDamage(new DamageRequest(_target, 80));
                _adapter.OnDamageConfirmed(defeat);
                VfxPersistentStateRebuilder.RebuildFromVitality(_vitality, _coordinator);
                _phase = "Confirmed defeat + current defeated treatment";
                Debug.Log("VFX_VALIDATION defeat: occurred=" + defeat.DefeatOccurred + " persistent=" + _presenter.PersistentCount + " plays=" + _presenter.OneShotPlayCount);
                _defeatDone = true;
            }

            if (!_interactionDone && elapsed >= 5f)
            {
                _adapter.Publish(new WorldInteractionFact(77, _target, new WorldObjectId("altar:crystal"), WorldObjectKind.DoorToggle, 1, 2));
                _phase = "Confirmed semantic interaction pulse";
                Debug.Log("VFX_VALIDATION interaction: sequence=77 plays=" + _presenter.OneShotPlayCount);
                _interactionDone = true;
            }

            if (!_destructionDone && elapsed >= 6.5f)
            {
                var alteration = new AlterationEvent(AlterationEvent.KindExplosion, 42, new int3(2, 1, 4), 2, 0, 991u, 3, 9);
                VfxSubmitResult result = _adapter.OnAlterationCommitted(alteration);
                _phase = "Confirmed world alteration -> cosmetic debris";
                Debug.Log("VFX_VALIDATION destruction: result=" + result + " gameplayPhysics=" + _presenter.CountGameplayPhysicsComponents() + " plays=" + _presenter.OneShotPlayCount);
                _destructionDone = true;
            }

            if (!_rebuildDone && elapsed >= 8f)
            {
                int historicalBefore = _presenter.OneShotPlayCount;
                var rebuilt = new VfxCueCoordinator(VfxCueCatalog.CreateDefault(), _bindings, _presenter, _presenter);
                VfxPersistentStateRebuilder.RebuildFromVitality(_vitality, rebuilt);
                int historicalAfter = _presenter.OneShotPlayCount;
                _phase = "Reconnect rebuilt current treatment without replay";
                Debug.Log("VFX_VALIDATION reconnect: persistent=" + _presenter.PersistentCount + " historicalBefore=" + historicalBefore + " historicalAfter=" + historicalAfter);
                _rebuildDone = true;
            }
        }

        private void OnGUI()
        {
            GUI.Box(new Rect(28, 24, 680, 164), string.Empty);
            GUI.Label(new Rect(50, 42, 640, 30), "SEMANTIC COMBAT / INTERACTION VFX");
            GUI.Label(new Rect(50, 76, 640, 24), "Authority stays in gameplay/world modules • presentation consumes confirmed semantic facts");
            GUI.Label(new Rect(50, 106, 640, 24), "Phase: " + _phase);
            GUI.Label(new Rect(50, 136, 640, 24), "One-shots: " + (_presenter == null ? 0 : _presenter.OneShotPlayCount) +
                "   Persistent: " + (_presenter == null ? 0 : _presenter.PersistentCount) +
                "   Gameplay physics in VFX: " + (_presenter == null ? 0 : _presenter.CountGameplayPhysicsComponents()));

            GUI.Box(new Rect(28, 532, 1220, 150), string.Empty);
            GUI.Label(new Rect(50, 550, 1150, 28), "GOLD = predicted/confirmed HIT   •   RED = DEFEAT + persistent aura   •   CYAN = INTERACTION   •   EARTH = confirmed voxel debris");
            GUI.Label(new Rect(50, 584, 1150, 24), "Stable identity dedupe: predicted=" + _predicted + " / confirmed=" + _confirmed);
            GUI.Label(new Rect(50, 616, 1150, 24), "Reconnect restores only current semantic treatment; historical hit / interaction / destruction one-shots are not replayed.");
        }

        private static Transform CreateVisibleAnchor(
            string name,
            Vector3 position,
            PrimitiveType primitiveType,
            Vector3 scale,
            Color color)
        {
            GameObject anchor = GameObject.CreatePrimitive(primitiveType);
            anchor.name = name;
            anchor.transform.position = position;
            anchor.transform.localScale = scale;

            Collider collider = anchor.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            Renderer renderer = anchor.GetComponent<Renderer>();
            if (renderer != null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Unlit/Color");
                if (shader != null)
                {
                    Material material = new Material(shader) { name = name + " Validation Material" };
                    if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
                    if (material.HasProperty("_Color")) material.SetColor("_Color", color);
                    renderer.material = material;
                }
            }

            return anchor.transform;
        }

        private static void ConfigureCamera()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                GameObject go = new GameObject("VFX Validation Camera");
                go.tag = "MainCamera";
                camera = go.AddComponent<Camera>();
            }
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.035f, 0.06f, 1f);
            camera.fieldOfView = 48f;
            camera.transform.position = new Vector3(0f, 2.6f, -5.8f);
            camera.transform.LookAt(new Vector3(0f, 1.1f, 4.4f));
        }

        private void OnDestroy()
        {
            _adapter?.Dispose();
        }
    }
}
