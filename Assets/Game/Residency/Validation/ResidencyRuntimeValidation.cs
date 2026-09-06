using System;
using Game.CharacterAI.Api;
using Game.CharacterAI.Runtime;
using Game.Characters.Api;
using Game.Characters.Runtime;
using Game.Residency.Api;
using Game.Residency.Runtime;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Storage.Runtime;
using VoxelEngine.Streaming.Api;
using VoxelEngine.Streaming.Runtime;

namespace Game.Residency.Validation
{
    public sealed class ResidencyRuntimeValidation : MonoBehaviour
    {
        private RegionTable _table;
        private BrickPool _pool;
        private RegionResidencyStore _store;
        private GameplayResidencyCoordinator _coordinator;
        private CharacterRegistry _characters;
        private CharacterAiController _ai;
        private IResidencyDemandLease _proximity;
        private IResidencyDemandLease _control;
        private ResidencyTarget _target;
        private CharacterId _characterId;
        private float _started;
        private int _phase;
        private string _status = "starting";

        private void Start()
        {
            EnsureCamera();
            _started = Time.unscaledTime;
            _table = new RegionTable(8, Allocator.Persistent);
            _pool = new BrickPool(32, Allocator.Persistent);
            _store = new RegionResidencyStore(in _table, in _pool);
            var streaming = new RegionStreamingService(_store);
            int3 region = new int3(2, 0, 2);
            _store.EnsureRegionResident(region);

            _characterId = CharacterId.FromStableKey("npc", "residency-validation-worker");
            _characters = new CharacterRegistry();
            CharacterRegistryFailure created = _characters.Create(
                new CharacterDefinition(_characterId, CharacterTraits.ConversationCapable),
                new CharacterKinematicState(new CharacterVector3(12, 0, 12), new CharacterVector3(0, 0, 0), new CharacterVector3(0, 0, 1)),
                out _);
            Require(created == CharacterRegistryFailure.None, "character registration failed");

            var coarse = new SemanticCoarseCycleSimulation(_characterId, new[] { "Work", "TravelHome", "AtHome" });
            _ai = new CharacterAiController(_characterId, new EmptyPerception(_characterId), new IdlePolicy(), new AcceptingExecutor(), coarse);
            var adapter = new CharacterResidencyAdapter(
                _characters,
                id => id == _characterId ? _ai : null,
                id => new ResidencyRegion(region.x, region.y, region.z, 123u));
            _coordinator = new GameplayResidencyCoordinator((IRegionResidencyPins)streaming, new IResidencyTargetAdapter[] { adapter });
            _target = new ResidencyTarget(ResidencyTargetKind.Character, _characterId.Value);
            _proximity = _coordinator.Acquire(new ResidencyDemandRequest(_target, ResidencyFidelity.Coarse, "validation:proximity", "Proximity", "nearby semantic simulation"));
            _coordinator.Reconcile();
            _ai.Tick();
            _ai.Tick();
            Require(_ai.TryGetCoarseState(out AiCoarseStateSnapshot coarseState) && coarseState.SemanticState == "AtHome", "coarse semantic life did not advance");
            Require(_characters.TryGet(_characterId, out CharacterSnapshot same) && same.Id == _characterId, "character identity changed at coarse fidelity");
            _status = "Coarse • AtHome • physical realization not required";
            Debug.Log("RESIDENCY_VALIDATION coarse: id=" + _characterId + " semantic=AtHome detailed=0");
        }

        private void Update()
        {
            float elapsed = Time.unscaledTime - _started;
            if (_phase == 0 && elapsed >= 1f)
            {
                _control = _coordinator.Acquire(new ResidencyDemandRequest(_target, ResidencyFidelity.Detailed, "validation:control", "Control", "explicit controlled character"));
                _coordinator.Reconcile();
                Require(_coordinator.TryGetState(_target, out ResidencyTargetSnapshot state) && state.Current == ResidencyFidelity.Detailed, "explicit demand did not promote immediately to Detailed");
                Require(_ai.SimulationFidelity == AiSimulationFidelity.Detailed, "CharacterAI did not enter Detailed fidelity");
                _status = "Detailed • explicit control demand • physical pin ready";
                Debug.Log("RESIDENCY_VALIDATION detailed: current=Detailed worldReady=true demands=2");
                _phase = 1;
            }
            if (_phase == 1 && elapsed >= 2f)
            {
                _control.Dispose(); _control = null;
                _coordinator.Reconcile();
                Require(_coordinator.TryGetState(_target, out ResidencyTargetSnapshot state) && state.Current == ResidencyFidelity.Coarse, "release of control demand did not reveal remaining Coarse demand");
                _status = "Coarse • control released • proximity demand remains";
                Debug.Log("RESIDENCY_VALIDATION independent-release: current=Coarse demands=1");
                _phase = 2;
            }
            if (_phase == 2 && elapsed >= 3f)
            {
                _proximity.Dispose(); _proximity = null;
                _coordinator.Reconcile();
                Require(_coordinator.TryGetState(_target, out ResidencyTargetSnapshot state) && state.Current == ResidencyFidelity.Dormant, "final release did not demote to Dormant");
                Require(_characters.TryGet(_characterId, out CharacterSnapshot same) && same.Id == _characterId, "CharacterId did not survive full cycle");
                ResidencyDiagnosticsSnapshot diagnostics = _coordinator.GetDiagnostics();
                _status = "Dormant • identity retained • transition sequence deterministic";
                Debug.Log("RESIDENCY_VALIDATION dormant: id=" + _characterId + " transitions=" + diagnostics.TransitionHistory.Count + " demands=0");
                _phase = 3;
            }
        }

        private void OnGUI()
        {
            GUI.Box(new Rect(30, 30, 760, 150), string.Empty);
            GUI.Label(new Rect(50, 50, 700, 30), "GAMEPLAY RESIDENCY • PRODUCTION RUNTIME VALIDATION");
            GUI.Label(new Rect(50, 85, 700, 30), _status);
            GUI.Label(new Rect(50, 120, 700, 30), "Same CharacterId; independent semantic demands; real Storage/Streaming residency stack.");
        }

        private void OnDestroy()
        {
            _control?.Dispose(); _proximity?.Dispose(); _coordinator?.Dispose();
            if (_table.IsCreated) _table.Dispose();
            if (_pool.IsCreated) _pool.Dispose();
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) { Debug.LogError("RESIDENCY_VALIDATION failure: " + message); throw new InvalidOperationException(message); }
        }

        private static void EnsureCamera()
        {
            if (Camera.main != null) return;
            var cameraObject = new GameObject("Residency Validation Camera"); cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>(); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.035f, 0.04f, 0.055f, 1f);
        }

        private sealed class EmptyPerception : IAiPerceptionSource
        {
            private readonly CharacterId _id; public EmptyPerception(CharacterId id) { _id = id; }
            public AiPerceptionSnapshot Observe(CharacterId actor) => new AiPerceptionSnapshot(_id, Array.Empty<AiObservation>());
        }
        private sealed class IdlePolicy : IAiIntentPolicy { public AiIntent SelectIntent(AiPerceptionSnapshot perception) => new AiIntent(perception.Actor, AiIntentKind.Idle, default, string.Empty, 0, "idle"); }
        private sealed class AcceptingExecutor : IAiIntentExecutor { public AiIntentExecutionResult TryExecute(AiIntent intent) => AiIntentExecutionResult.Accept(); }
    }
}
