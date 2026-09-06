using System;
using Game.Application.Api;
using Game.Characters.Api;
using Game.Combat.Api;
using Game.Composition.Kentridge.Playable;
using Game.Composition.Kentridge.Runtime;
using Game.SessionOrchestration.Api;
using Game.Vitality.Api;
using Game.WorldObjects.Api;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Game.Kentridge.PlayableSlice
{
    /// <summary>
    /// Built-player validation orchestration for the System24 production vertical slice. This component
    /// is inert unless explicitly requested by command line. It only observes semantic production state,
    /// invokes the public Application lifecycle intents, and drives gameplay through a virtual Input
    /// System device. It never teleports actors or calls gameplay completion/mutation shortcuts.
    /// </summary>
    [DefaultExecutionOrder(-5000)]
    public sealed class KentridgeSystem24VerticalSliceDriver : MonoBehaviour
    {
        private const string ActivationArgument = "-voxel-system24-vertical-slice";
        private const float DestinationNetworkStopMetres = 1.25f;
        private const float DestinationEntranceStopMetres = 0.65f;
        private const float DestinationStopMetres = 1.75f;
        private const float PostRestoreMovementMetres = 0.75f;

        private enum Stage
        {
            WaitFrontend,
            WaitGameplayReady,
            WaitGameplayControl,
            MoveToDestination,
            WaitDestinationInteraction,
            WaitStoryProgression,
            MoveToAmbush,
            WaitCombatResolved,
            WaitLootPickup,
            Save,
            WaitFrontendAfterLeave,
            WaitRestore,
            VerifyRestore,
            PostRestoreMovement,
            Complete,
            Failed
        }

        private enum DestinationRoutePhase
        {
            NetworkApproach,
            PublicEntrance,
            Npc
        }

        private KentridgeProductionCompositionRoot _root;
        private KentridgePlayableSlice _slice;
        private KentridgeForestBanditEncounter _forest;
        private KentridgeProductionWorldInteraction _worldInteraction;
        private Gamepad _gamepad;
        private Keyboard _keyboard;
        private Stage _stage;
        private DestinationRoutePhase _destinationRoutePhase;
        private float _deadline;
        private float _stageStartedAt;
        private float _nextDiagnosticAt;
        private bool _destinationInteractionQueued;
        private bool _lootInteractionQueued;
        private bool _storyCompleted;
        private bool _combatObserved;
        private bool _lootCollected;
        private bool _restoreVerified;
        private Vector3 _movementOrigin;
        private Vector3 _savedPosition;
        private CharacterId _savedCharacterId;
        private VitalitySnapshot _savedVitality;
        private int _savedLootCount;
        private bool _savedTravelObjectiveCompleted;
        private bool _savedEncounterResolved;
        private WorldObjectStateSnapshot _savedLootState;
        private KentridgeSessionRuntimeGraph _savedGraph;

        public static bool IsRequested
        {
            get
            {
                string[] args = Environment.GetCommandLineArgs();
                for (int i = 0; i < args.Length; i++)
                    if (string.Equals(args[i], ActivationArgument, StringComparison.Ordinal))
                        return true;
                return false;
            }
        }

        private void Start()
        {
            if (!IsRequested)
            {
                enabled = false;
                return;
            }

            _root = GetComponent<KentridgeProductionCompositionRoot>()
                ?? throw new InvalidOperationException("System24 validation requires the production Application root.");
            _slice = GetComponent<KentridgePlayableSlice>()
                ?? throw new InvalidOperationException("System24 validation requires the production playable slice.");
            _forest = GetComponent<KentridgeForestBanditEncounter>()
                ?? throw new InvalidOperationException("System24 validation requires the production forest encounter.");
            _worldInteraction = GetComponent<KentridgeProductionWorldInteraction>()
                ?? throw new InvalidOperationException("System24 validation requires the production WorldObject adapter.");

            _gamepad = InputSystem.AddDevice<Gamepad>();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            QueueMove(Vector2.zero);
            QueueKeyboard();

            Debug.Log("SYSTEM24_VALIDATION start route=frontend-newgame-gameplay-story-encounter-loot-save-continue");
            Enter(Stage.WaitFrontend, 45f);
        }

        private void OnDestroy()
        {
            RemoveValidationDevices();
        }

        private void OnDisable()
        {
            if (IsRequested) RemoveValidationDevices();
        }

        private void Update()
        {
            if (!IsRequested || _stage == Stage.Complete || _stage == Stage.Failed) return;
            if (Time.realtimeSinceStartup > _deadline)
            {
                Fail("timed out waiting for semantic milestone");
                return;
            }

            if (Time.realtimeSinceStartup >= _nextDiagnosticAt)
            {
                _nextDiagnosticAt = Time.realtimeSinceStartup + 10f;
                Debug.Log(
                    "SYSTEM24_VALIDATION progress stage=" + _stage +
                    " flow=" + _root.FlowSnapshot.Lifecycle + "/" + _root.FlowSnapshot.Screen +
                    " ready=" + _root.FlowSnapshot.GameplayReady +
                    " session=" + _root.SessionSnapshot.Lifecycle +
                    " combat=" + _forest.CombatActive + "/" + _forest.CombatResolved +
                    " exitedPub=" + _slice.HasExitedPub +
                    " destinationRoute=" + _destinationRoutePhase +
                    " position=" + Format(_slice.CharacterHost == null ? Vector3.zero : _slice.CharacterHost.Position) +
                    " destination=" + DestinationDiagnostic() +
                    " loot=" + _worldInteraction.ForestLootCount);
            }

            switch (_stage)
            {
                case Stage.WaitFrontend:
                    TickWaitFrontend();
                    break;
                case Stage.WaitGameplayReady:
                    TickWaitGameplayReady();
                    break;
                case Stage.WaitGameplayControl:
                    TickWaitGameplayControl();
                    break;
                case Stage.MoveToDestination:
                    TickMoveToDestination();
                    break;
                case Stage.WaitDestinationInteraction:
                    TickWaitDestinationInteraction();
                    break;
                case Stage.WaitStoryProgression:
                    TickWaitStoryProgression();
                    break;
                case Stage.MoveToAmbush:
                    TickMoveToAmbush();
                    break;
                case Stage.WaitCombatResolved:
                    TickWaitCombatResolved();
                    break;
                case Stage.WaitLootPickup:
                    TickWaitLootPickup();
                    break;
                case Stage.Save:
                    TickSave();
                    break;
                case Stage.WaitFrontendAfterLeave:
                    TickWaitFrontendAfterLeave();
                    break;
                case Stage.WaitRestore:
                    TickWaitRestore();
                    break;
                case Stage.VerifyRestore:
                    TickVerifyRestore();
                    break;
                case Stage.PostRestoreMovement:
                    TickPostRestoreMovement();
                    break;
            }
        }

        private void TickWaitFrontend()
        {
            ApplicationFlowSnapshot flow = _root.FlowSnapshot;
            if (!_root.IsComposed || flow.Lifecycle != ApplicationLifecycle.FrontEnd ||
                flow.Screen != ApplicationScreen.MainMenu)
                return;

            Milestone("frontend", "screen=" + flow.Screen);
            ApplicationOperationResult result = _root.RequestNewGame();
            if (!result.Succeeded)
            {
                Fail("New Game rejected: " + result.Failure + " " + result.Detail);
                return;
            }
            Enter(Stage.WaitGameplayReady, 75f);
        }

        private void TickWaitGameplayReady()
        {
            ApplicationFlowSnapshot flow = _root.FlowSnapshot;
            if (flow.Lifecycle != ApplicationLifecycle.InGame || !flow.GameplayReady ||
                _root.SessionSnapshot.Lifecycle != GameSessionLifecycle.Running)
                return;

            Milestone(
                "gameplay-ready",
                "generation=" + (_root.SessionSnapshot.Handle == null ? 0 : _root.SessionSnapshot.Handle.Generation));
            Enter(Stage.WaitGameplayControl, 75f);
        }

        private void TickWaitGameplayControl()
        {
            if (!_slice.GameplayControlEnabled) return;
            _movementOrigin = _slice.CharacterHost.Position;
            ResetDestinationRoute();
            Milestone("gameplay-control", "position=" + Format(_movementOrigin));
            Enter(Stage.MoveToDestination, 90f);
        }

        private void TickMoveToDestination()
        {
            if (TryHandleUnexpectedCombat()) return;
            if (!_slice.HasExitedPub)
            {
                QueueMove(new Vector2(0f, 1f));
                return;
            }
            if (!_slice.TryGetDestinationNpcWorldPosition(out Vector3 destination)) return;
            if (!TryResolveDestinationPublicRoute(out Vector3 networkApproach, out Vector3 publicEntrance))
            {
                Fail("destination NPC has no resolved production public-circulation route");
                return;
            }

            switch (_destinationRoutePhase)
            {
                case DestinationRoutePhase.NetworkApproach:
                    if (!DriveToward(networkApproach, DestinationNetworkStopMetres)) return;
                    QueueMove(Vector2.zero);
                    Debug.Log(
                        "SYSTEM24_VALIDATION route=destination-network-approach position=" +
                        Format(_slice.CharacterHost.Position));
                    _destinationRoutePhase = DestinationRoutePhase.PublicEntrance;
                    return;
                case DestinationRoutePhase.PublicEntrance:
                    if (!DriveToward(publicEntrance, DestinationEntranceStopMetres)) return;
                    QueueMove(Vector2.zero);
                    Debug.Log(
                        "SYSTEM24_VALIDATION route=destination-public-entrance position=" +
                        Format(_slice.CharacterHost.Position));
                    _destinationRoutePhase = DestinationRoutePhase.Npc;
                    return;
            }

            if (!DriveToward(destination, DestinationStopMetres)) return;
            QueueMove(Vector2.zero);
            Milestone(
                "movement-to-destination",
                "from=" + Format(_movementOrigin) + " to=" + Format(_slice.CharacterHost.Position));
            QueueInteract();
            _destinationInteractionQueued = true;
            Enter(Stage.WaitDestinationInteraction, 15f);
        }

        private void TickWaitDestinationInteraction()
        {
            QueueMove(Vector2.zero);
            if (TryHandleUnexpectedCombat()) return;
            if (_slice.DestinationCutsceneActive)
            {
                Milestone("npc-interaction", "destinationCutscene=true");
                Enter(Stage.WaitStoryProgression, 45f);
                return;
            }

            if (Time.realtimeSinceStartup - _stageStartedAt > 2f)
            {
                _destinationInteractionQueued = false;
                Enter(Stage.MoveToDestination, 45f);
            }
        }

        private void TickWaitStoryProgression()
        {
            QueueMove(Vector2.zero);
            if (TryHandleUnexpectedCombat()) return;
            if (_slice.DestinationCutsceneActive || !_slice.TravelObjectiveCompleted) return;

            _storyCompleted = true;
            Milestone("story-progressed", "travelObjectiveCompleted=true");
            if (_lootCollected)
                Enter(Stage.Save, 10f);
            else
                Enter(Stage.MoveToAmbush, 120f);
        }

        private void TickMoveToAmbush()
        {
            if (_forest.CombatActive)
            {
                BeginCombatWait();
                return;
            }
            if (_forest.CombatResolved)
            {
                Enter(Stage.WaitLootPickup, 20f);
                return;
            }

            Vector3 target = _forest.AmbushCenterWorld;
            DriveToward(target, Mathf.Max(1.5f, _forest.TriggerRadiusMetres * 0.55f));
        }

        private bool TryHandleUnexpectedCombat()
        {
            if (!_forest.CombatActive) return false;
            BeginCombatWait();
            return true;
        }

        private void BeginCombatWait()
        {
            QueueMove(Vector2.zero);
            if (!_combatObserved)
            {
                _combatObserved = true;
                Milestone(
                    "encounter-active",
                    "bandits=" + _forest.BanditCount + " ambush=" + Format(_forest.AmbushCenterWorld));
            }
            Enter(Stage.WaitCombatResolved, 45f);
        }

        private void TickWaitCombatResolved()
        {
            QueueMove(Vector2.zero);
            if (_forest.CombatActive) QueuePrimary();
            if (!_forest.CombatResolved) return;
            if (!_forest.WinningTeam.HasValue || _forest.WinningTeam.Value != CombatTeam.Player)
            {
                Fail("representative combat did not resolve to the player team; winner=" + _forest.WinningTeam);
                return;
            }

            Milestone(
                "combat-resolved",
                "winner=" + _forest.WinningTeam.Value +
                " actions=" + _forest.CombatActionCount +
                " turns=" + _forest.CombatTurnNumber);
            Enter(Stage.WaitLootPickup, 20f);
        }

        private void TickWaitLootPickup()
        {
            QueueMove(Vector2.zero);
            if (_worldInteraction.PickupCollected && _worldInteraction.ForestLootCount > 0)
            {
                _lootCollected = true;
                Milestone(
                    "loot-collected",
                    "count=" + _worldInteraction.ForestLootCount +
                    " worldObjectCollected=" + _worldInteraction.PickupCollected);
                if (_storyCompleted || _slice.TravelObjectiveCompleted)
                {
                    _storyCompleted = true;
                    Enter(Stage.Save, 10f);
                }
                else
                {
                    _movementOrigin = _slice.CharacterHost.Position;
                    ResetDestinationRoute();
                    Enter(Stage.MoveToDestination, 120f);
                }
                return;
            }

            if (_worldInteraction.PickupSpawned && !_lootInteractionQueued)
            {
                QueueInteract();
                _lootInteractionQueued = true;
            }
            else if (_lootInteractionQueued && Time.realtimeSinceStartup - _stageStartedAt > 2f)
            {
                _lootInteractionQueued = false;
                _stageStartedAt = Time.realtimeSinceStartup;
            }
        }

        private void TickSave()
        {
            QueueMove(Vector2.zero);
            if (!_storyCompleted || !_lootCollected || !_forest.CombatResolved)
            {
                Fail("save gate reached without completed representative state");
                return;
            }
            if (!_worldInteraction.TryGetForestLootState(out _savedLootState) || _savedLootState.Enabled)
            {
                Fail("collected forest WorldObject state is unavailable for save verification");
                return;
            }

            KentridgeCharacterHost host = _slice.CharacterHost;
            _savedCharacterId = host.PlayerCharacterId;
            if (_forest.VitalityQuery == null ||
                !_forest.VitalityQuery.TryGet(_savedCharacterId, out _savedVitality))
            {
                Fail("player vitality is unavailable for save verification");
                return;
            }
            _savedPosition = host.Position;
            _savedLootCount = _worldInteraction.ForestLootCount;
            _savedTravelObjectiveCompleted = _slice.TravelObjectiveCompleted;
            _savedEncounterResolved = _forest.CombatResolved;
            _savedGraph = _slice.SessionFactory.Current;

            GameSessionOperationResult save = _root.RequestSave();
            if (!save.Succeeded)
            {
                Fail("save rejected: " + save.Failure + " " + save.Diagnostic);
                return;
            }
            if (!string.Equals(
                    _root.LastPublishedSaveId,
                    KentridgeProductionCompositionRoot.DefaultSaveId,
                    StringComparison.Ordinal))
            {
                Fail("save did not publish the expected semantic save id");
                return;
            }

            Milestone(
                "save-complete",
                "id=" + _root.LastPublishedSaveId +
                " position=" + Format(_savedPosition) +
                " vitality=" + _savedVitality.Current + "/" + _savedVitality.Maximum +
                " vitalityRevision=" + _savedVitality.Revision +
                " lootRevision=" + _savedLootState.Revision);
            ApplicationOperationResult leave = _root.RequestLeaveGame();
            if (!leave.Succeeded)
            {
                Fail("ordered leave rejected: " + leave.Failure + " " + leave.Detail);
                return;
            }
            Enter(Stage.WaitFrontendAfterLeave, 30f);
        }

        private void TickWaitFrontendAfterLeave()
        {
            QueueMove(Vector2.zero);
            if (_root.FlowSnapshot.Lifecycle != ApplicationLifecycle.FrontEnd ||
                _root.SessionSnapshot.Lifecycle != GameSessionLifecycle.Stopped)
                return;

            Milestone(
                "teardown-complete",
                "flow=" + _root.FlowSnapshot.Lifecycle + " session=" + _root.SessionSnapshot.Lifecycle);
            ApplicationOperationResult resume = _root.RequestContinue();
            if (!resume.Succeeded)
            {
                Fail("Continue rejected: " + resume.Failure + " " + resume.Detail);
                return;
            }
            Enter(Stage.WaitRestore, 75f);
        }

        private void TickWaitRestore()
        {
            QueueMove(Vector2.zero);
            KentridgeSessionRuntimeGraph current = _slice.SessionFactory?.Current;
            if (_root.FlowSnapshot.Lifecycle != ApplicationLifecycle.InGame ||
                !_root.FlowSnapshot.GameplayReady ||
                _root.SessionSnapshot.Lifecycle != GameSessionLifecycle.Running ||
                current == null || current.IsDisposed || !current.RestoredFromPersistence)
                return;
            if (ReferenceEquals(current, _savedGraph))
            {
                Fail("Continue reused the prior disposed session graph");
                return;
            }

            Milestone(
                "continue-restored",
                "generation=" + (_root.SessionSnapshot.Handle == null ? 0 : _root.SessionSnapshot.Handle.Generation));
            Enter(Stage.VerifyRestore, 15f);
        }

        private void TickVerifyRestore()
        {
            QueueMove(Vector2.zero);
            KentridgeCharacterHost host = _slice.CharacterHost;
            if (host.PlayerCharacterId != _savedCharacterId)
            {
                Fail("restored character identity changed");
                return;
            }
            if (_forest.VitalityQuery == null ||
                !_forest.VitalityQuery.TryGet(_savedCharacterId, out VitalitySnapshot restoredVitality) ||
                restoredVitality != _savedVitality)
            {
                Fail("restored player vitality, maximum, defeat state or revision changed");
                return;
            }
            if ((host.Position - _savedPosition).sqrMagnitude > 0.05f * 0.05f)
            {
                Fail("restored player position changed: saved=" + Format(_savedPosition) +
                     " restored=" + Format(host.Position));
                return;
            }
            if (_worldInteraction.ForestLootCount != _savedLootCount)
            {
                Fail("restored inventory count changed");
                return;
            }
            if (_slice.TravelObjectiveCompleted != _savedTravelObjectiveCompleted ||
                _forest.CombatResolved != _savedEncounterResolved)
            {
                Fail("restored progression or encounter state changed");
                return;
            }
            if (!_worldInteraction.TryGetForestLootState(out WorldObjectStateSnapshot restoredLoot) ||
                restoredLoot.Enabled != _savedLootState.Enabled ||
                restoredLoot.StateCode != _savedLootState.StateCode ||
                restoredLoot.Revision != _savedLootState.Revision)
            {
                Fail("restored WorldObject state changed");
                return;
            }
            if (_worldInteraction.LastFact.HasValue)
            {
                Fail("Continue replayed a historical WorldObject interaction fact");
                return;
            }

            _restoreVerified = true;
            _movementOrigin = host.Position;
            Milestone(
                "restore-state-verified",
                "loot=" + _worldInteraction.ForestLootCount +
                " worldObjectState=" + restoredLoot.StateCode +
                " revision=" + restoredLoot.Revision +
                " vitality=" + restoredVitality.Current + "/" + restoredVitality.Maximum +
                " vitalityRevision=" + restoredVitality.Revision +
                " objective=" + _slice.TravelObjectiveCompleted +
                " encounter=" + _forest.CombatResolved);
            Enter(Stage.PostRestoreMovement, 15f);
        }

        private void TickPostRestoreMovement()
        {
            if (!_restoreVerified)
            {
                Fail("post-restore action began before restore verification");
                return;
            }
            QueueMove(new Vector2(0f, 1f));
            Vector3 current = _slice.CharacterHost.Position;
            Vector3 delta = current - _movementOrigin;
            delta.y = 0f;
            if (delta.magnitude < PostRestoreMovementMetres) return;

            QueueMove(Vector2.zero);
            Milestone(
                "post-restore-action",
                "moved=" + delta.magnitude.ToString("0.00") + "m position=" + Format(current));
            Milestone(
                "complete",
                "frontend=newgame gameplay=true story=true encounter=true loot=true save=true continue=true");
            _stage = Stage.Complete;
        }

        private void ResetDestinationRoute()
        {
            _destinationRoutePhase = DestinationRoutePhase.NetworkApproach;
        }

        private bool TryResolveDestinationPublicRoute(
            out Vector3 networkApproach,
            out Vector3 publicEntrance)
        {
            networkApproach = default;
            publicEntrance = default;
            KentridgeSessionRuntimeGraphFactory factory = _slice.SessionFactory;
            KentridgeCampaignSession session = _slice.CampaignSession;
            KentridgeCharacterHost host = _slice.CharacterHost;
            if (factory == null || factory.Generation == null || session == null || host == null ||
                !_slice.TryGetDestinationNpcWorldPosition(out Vector3 destination))
                return false;

            for (int i = 0; i < session.World.Npcs.Count; i++)
            {
                var placement = session.World.Npcs[i];
                if (!host.TryGetNpcPosition(placement.Npc, out Vector3 candidate)) continue;
                Vector3 delta = candidate - destination;
                delta.y = 0f;
                if (delta.sqrMagnitude > 0.01f * 0.01f) continue;
                if (!factory.Generation.TryResolveNpcPublicRoute(placement.Npc, out var route))
                    return false;

                float y = host.Position.y;
                networkApproach = new Vector3(
                    route.NetworkApproachDm.X * 0.1f,
                    y,
                    route.NetworkApproachDm.Y * 0.1f);
                publicEntrance = new Vector3(
                    route.PublicEntranceDm.X * 0.1f,
                    y,
                    route.PublicEntranceDm.Y * 0.1f);
                return true;
            }

            return false;
        }

        private bool DriveToward(Vector3 target, float stopDistance)
        {
            Vector3 player = _slice.CharacterHost.Position;
            Vector3 delta = target - player;
            delta.y = 0f;
            float distance = delta.magnitude;
            if (distance <= stopDistance) return true;
            if (distance <= 0.001f)
            {
                QueueMove(Vector2.zero);
                return true;
            }

            Vector3 forward = Vector3.ProjectOnPlane(_slice.transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(_slice.transform.right, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.5f || right.sqrMagnitude < 0.5f)
            {
                Fail("camera basis is invalid for semantic movement input");
                return false;
            }

            Vector3 direction = delta / distance;
            var input = new Vector2(Vector3.Dot(direction, right), Vector3.Dot(direction, forward));
            if (input.sqrMagnitude > 1f) input.Normalize();
            QueueMove(input);
            return false;
        }

        private void QueueMove(Vector2 leftStick)
        {
            if (_gamepad == null || !_gamepad.added) return;
            InputSystem.QueueStateEvent(_gamepad, new GamepadState { leftStick = leftStick });
        }

        private void QueuePrimary()
        {
            if (_gamepad == null || !_gamepad.added) return;
            InputSystem.QueueStateEvent(_gamepad, new GamepadState().WithButton(GamepadButton.South));
            InputSystem.QueueStateEvent(_gamepad, new GamepadState());
        }

        private void QueueInteract()
        {
            QueuePrimary();
        }

        private void QueueKeyboard()
        {
            if (_keyboard == null || !_keyboard.added) return;
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
        }

        private void Enter(Stage stage, float timeoutSeconds)
        {
            _stage = stage;
            _stageStartedAt = Time.realtimeSinceStartup;
            _deadline = _stageStartedAt + Mathf.Max(1f, timeoutSeconds);
            _nextDiagnosticAt = _stageStartedAt + 10f;
            Debug.Log("SYSTEM24_VALIDATION stage=" + stage + " timeout=" + timeoutSeconds.ToString("0.#") + "s");
        }

        private void Milestone(string name, string detail)
        {
            Debug.Log("SYSTEM24_VALIDATION milestone=" + name + " " + detail);
        }

        private void Fail(string reason)
        {
            QueueMove(Vector2.zero);
            Debug.LogError(
                "SYSTEM24_VALIDATION failure: stage=" + _stage +
                " reason=" + reason +
                " flow=" + _root.FlowSnapshot.Lifecycle + "/" + _root.FlowSnapshot.Screen +
                " session=" + _root.SessionSnapshot.Lifecycle +
                " position=" + Format(_slice.CharacterHost == null ? Vector3.zero : _slice.CharacterHost.Position) +
                " exitedPub=" + _slice.HasExitedPub +
                " destinationRoute=" + _destinationRoutePhase +
                " destination=" + DestinationDiagnostic() +
                " combat=" + _forest.CombatActive + "/" + _forest.CombatResolved +
                " loot=" + _worldInteraction.ForestLootCount);
            _stage = Stage.Failed;
        }

        private string DestinationDiagnostic() =>
            _slice.TryGetDestinationNpcWorldPosition(out Vector3 destination) ? Format(destination) : "unavailable";

        private static string Format(Vector3 value) =>
            value.x.ToString("0.0") + "," + value.y.ToString("0.0") + "," + value.z.ToString("0.0");

        private void RemoveValidationDevices()
        {
            if (_gamepad != null && _gamepad.added) InputSystem.RemoveDevice(_gamepad);
            if (_keyboard != null && _keyboard.added) InputSystem.RemoveDevice(_keyboard);
            _gamepad = null;
            _keyboard = null;
        }
    }
}
