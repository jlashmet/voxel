using System;
using System.Globalization;
using Game.Application.Api;
using Game.Input.Api;
using Game.SessionOrchestration.Api;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Game.Kentridge.PlayableSlice.Validation
{
    /// <summary>
    /// Exit-only built-player regression for the production opening handoff. The module scene uses
    /// the real Kentridge composition, generated world, motor and presentation. This observer only
    /// requests New Game and queues physical input; it never changes actors, world state or readiness.
    /// </summary>
    [DefaultExecutionOrder(-5000)]
    public sealed class KentridgeOpeningControlValidation : MonoBehaviour
    {
        private const string Argument = "-voxel-kentridge-opening-control-validation";
        private static readonly LocalPlayerId Player = new LocalPlayerId(0);
        private enum Stage { Frontend, GameplayReady, Control, Exit, Complete, Failed }

        private KentridgeProductionCompositionRoot _root;
        private KentridgePlayableSlice _slice;
        private IPlayerInputReader _input;
        private Gamepad _gamepad;
        private Stage _stage;
        private float _deadline;
        private float _nextDiagnostic;
        private Vector3 _releasePosition;
        private bool _observedOpening;
        private bool _observedMovementInput;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), Argument) < 0) return;
            KentridgePlayableSlice slice = UnityEngine.Object.FindFirstObjectByType<KentridgePlayableSlice>();
            if (slice == null)
            {
                Debug.LogError("KENTRIDGE_OPENING_CONTROL FAIL missing production slice");
                return;
            }
            if (slice.GetComponent<KentridgeOpeningControlValidation>() == null)
                slice.gameObject.AddComponent<KentridgeOpeningControlValidation>();
        }

        private void Start()
        {
            _slice = GetComponent<KentridgePlayableSlice>();
            _root = GetComponent<KentridgeProductionCompositionRoot>();
            if (_slice == null || _root == null || KentridgeSystem24VerticalSliceDriver.IsRequested)
            {
                Fail("requires production composition with no competing System24 input driver");
                return;
            }
            _input = _root.InputActions as IPlayerInputReader;
            if (_input == null)
            {
                Fail("production input adapter does not expose its player snapshot");
                return;
            }
            _gamepad = InputSystem.AddDevice<Gamepad>();
            QueueMove(Vector2.zero);
            Enter(Stage.Frontend, 45f);
        }

        private void Update()
        {
            if (_stage == Stage.Complete || _stage == Stage.Failed) return;
            if (Time.realtimeSinceStartup > _deadline)
            {
                Fail("semantic deadline expired");
                return;
            }
            if (_slice.OpeningCutsceneStarted && _slice.OpeningCutsceneCameraActive)
                _observedOpening = true;
            if (_slice.GameplayControlEnabled && _slice.OpeningCutsceneCameraActive)
            {
                Fail("gameplay control was published before the physical camera/player handoff");
                return;
            }
            if (Time.realtimeSinceStartup >= _nextDiagnostic)
            {
                _nextDiagnostic = Time.realtimeSinceStartup + 1f;
                Debug.Log("KENTRIDGE_OPENING_CONTROL sample " + Diagnostic());
            }

            switch (_stage)
            {
                case Stage.Frontend:
                    if (!_root.IsComposed || _root.FlowSnapshot.Lifecycle != ApplicationLifecycle.FrontEnd)
                        return;
                    ApplicationOperationResult start = _root.RequestNewGame();
                    if (!start.Succeeded)
                    {
                        Fail("New Game rejected: " + start.Failure + " " + start.Detail);
                        return;
                    }
                    Enter(Stage.GameplayReady, 75f);
                    break;
                case Stage.GameplayReady:
                    if (!_root.FlowSnapshot.GameplayReady ||
                        _root.SessionSnapshot.Lifecycle != GameSessionLifecycle.Running)
                        return;
                    Enter(Stage.Control, 75f);
                    break;
                case Stage.Control:
                    if (!_slice.GameplayControlEnabled) return;
                    if (!_observedOpening || _slice.HasExitedPub)
                    {
                        Fail("opening was not observed or player was already outside before movement");
                        return;
                    }
                    _releasePosition = _slice.CharacterHost.Position;
                    Debug.Log("KENTRIDGE_OPENING_CONTROL milestone=released " + Diagnostic());
                    Enter(Stage.Exit, 20f);
                    QueueMove(Vector2.up);
                    break;
                case Stage.Exit:
                    PlayerInputSnapshot input = _input.Read(Player);
                    _observedMovementInput |= input.MoveY > 0.5f;
                    if (!_slice.GameplayControlEnabled)
                    {
                        Fail("control was lost while testing the public exit");
                        return;
                    }
                    if (!_slice.HasExitedPub)
                    {
                        QueueMove(Vector2.up);
                        return;
                    }
                    QueueMove(Vector2.zero);
                    Vector3 displacement = _slice.CharacterHost.Position - _releasePosition;
                    displacement.y = 0f;
                    if (!_observedMovementInput || displacement.sqrMagnitude < 0.25f)
                    {
                        Fail("exit was reported without production movement input and physical travel");
                        return;
                    }
                    Debug.Log("KENTRIDGE_OPENING_CONTROL PASS " + Diagnostic());
                    _stage = Stage.Complete;
                    break;
            }
        }

        private void Enter(Stage stage, float seconds)
        {
            _stage = stage;
            _deadline = Time.realtimeSinceStartup + seconds;
            _nextDiagnostic = Time.realtimeSinceStartup;
            Debug.Log("KENTRIDGE_OPENING_CONTROL stage=" + stage);
        }

        private string Diagnostic()
        {
            if (_slice == null || _root == null || _slice.CharacterHost == null)
                return "stage=" + _stage + " composition=unavailable";
            PlayerInputSnapshot input = _input == null ? default : _input.Read(Player);
            Vector2 device = _gamepad == null || !_gamepad.added ? Vector2.zero : _gamepad.leftStick.ReadValue();
            return "stage=" + _stage +
                " flow=" + _root.FlowSnapshot.Lifecycle + "/" + _root.FlowSnapshot.Screen +
                " session=" + _root.SessionSnapshot.Lifecycle +
                " control=" + _slice.GameplayControlEnabled +
                " openingCamera=" + _slice.OpeningCutsceneCameraActive +
                " openingFocus=" + Vector(_slice.OpeningCutsceneCameraFocus) +
                " exited=" + _slice.HasExitedPub +
                " input=" + Number(input.MoveX) + "," + Number(input.MoveY) +
                " device=" + Number(device.x) + "," + Number(device.y) +
                " position=" + Vector(_slice.CharacterHost.Position) +
                " velocity=" + Vector(_slice.CharacterHost.Velocity) +
                " forward=" + Vector(_slice.transform.forward);
        }

        private void Fail(string reason)
        {
            QueueMove(Vector2.zero);
            Debug.LogError("KENTRIDGE_OPENING_CONTROL FAIL " + reason + " " + Diagnostic());
            _stage = Stage.Failed;
        }

        private void QueueMove(Vector2 movement)
        {
            if (_gamepad != null && _gamepad.added)
                InputSystem.QueueStateEvent(_gamepad, new GamepadState { leftStick = movement });
        }

        private void OnDisable()
        {
            if (_gamepad != null && _gamepad.added) InputSystem.RemoveDevice(_gamepad);
            _gamepad = null;
        }

        private static string Number(float value) => value.ToString("0.000", CultureInfo.InvariantCulture);
        private static string Vector(Vector3 value) => Number(value.x) + "," + Number(value.y) + "," + Number(value.z);
    }
}
