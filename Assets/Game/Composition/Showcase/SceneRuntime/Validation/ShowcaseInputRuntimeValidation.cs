using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace VoxelEngine.Showcase.Validation
{
    /// <summary>
    /// Drives the production VoxelShowcase exclusively through Input System events. Test-only
    /// orchestration owns no movement/look/edit logic; success is measured from production state.
    /// </summary>
    public sealed class ShowcaseInputRuntimeValidation : MonoBehaviour
    {
        private VoxelShowcase _showcase;
        private Keyboard _keyboard;
        private Mouse _mouse;
        private int _phase;
        private int _frames;
        private Vector3 _startPosition;
        private Quaternion _startRotation;
        private Quaternion _relockRotation;
        private bool _initialFlashlight;
        private bool _finished;

        private void Start()
        {
            _showcase = GetComponent<VoxelShowcase>();
            if (_showcase == null) _showcase = FindFirstObjectByType<VoxelShowcase>();
            if (_showcase == null)
            {
                Debug.LogError("Showcase input validation FAIL: production VoxelShowcase missing.");
                _finished = true;
                return;
            }

            _keyboard = InputSystem.AddDevice<Keyboard>();
            _mouse = InputSystem.AddDevice<Mouse>();
            _keyboard.MakeCurrent();
            _mouse.MakeCurrent();
            Debug.Log("Showcase input validation ready: production VoxelShowcase + Input System devices.");
        }

        private void Update()
        {
            if (_finished || _showcase == null) return;
            _frames++;

            switch (_phase)
            {
                case 0:
                    if (_frames < 3) return;
                    // Unlock through the production Escape binding.
                    InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.Escape));
                    Advance();
                    break;

                case 1:
                    if (_frames < 2) return;
                    InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
                    Advance();
                    break;

                case 2:
                    if (_frames < 2) return;
                    // Relock while a huge delta is queued. Production SetCursorLocked must discard
                    // this frame rather than snapping the camera after cursor recapture.
                    _relockRotation = _showcase.transform.rotation;
                    InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.Escape));
                    InputSystem.QueueStateEvent(
                        _mouse, new MouseState { delta = new Vector2(4000f, -3000f) });
                    Advance();
                    break;

                case 3:
                    if (_frames < 2) return;
                    float recaptureAngle = Quaternion.Angle(_relockRotation, _showcase.transform.rotation);
                    if (recaptureAngle > 0.25f)
                    {
                        Fail($"cursor recapture applied stale look delta ({recaptureAngle:0.###} deg)");
                        return;
                    }
                    InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
                    Advance();
                    break;

                case 4:
                    if (_frames < 2) return;
                    // Enter fly mode through the real F binding so movement is independent of
                    // terrain-generation readiness, then release before the movement proof.
                    InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.F));
                    Advance();
                    break;

                case 5:
                    if (_frames < 2) return;
                    InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
                    Advance();
                    break;

                case 6:
                    if (_frames < 2) return;
                    _startPosition = _showcase.transform.position;
                    _startRotation = _showcase.transform.rotation;
                    _initialFlashlight = _showcase.FlashlightEnabled;
                    InputSystem.QueueStateEvent(
                        _keyboard, new KeyboardState(Key.W, Key.LeftShift));
                    InputSystem.QueueStateEvent(
                        _mouse,
                        new MouseState
                        {
                            delta = new Vector2(32f, -14f),
                            scroll = new Vector2(0f, 120f),
                        }.WithButton(MouseButton.Right));
                    Advance();
                    break;

                case 7:
                    if (_frames < 10) return;
                    InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
                    float moved = Vector3.Distance(_startPosition, _showcase.transform.position);
                    float looked = Quaternion.Angle(_startRotation, _showcase.transform.rotation);
                    bool edited = _showcase.FlashlightEnabled != _initialFlashlight;
                    if (moved <= 0.05f || looked <= 0.25f || !edited)
                    {
                        Fail($"production response incomplete moved={moved:0.###}m looked={looked:0.###}deg edited={edited}");
                        return;
                    }

                    Debug.Log(
                        $"Showcase input validation PASS moved={moved:0.###}m " +
                        $"looked={looked:0.###}deg edited={edited} recaptureSafe=true");
                    Finish();
                    break;
            }
        }

        private void Advance()
        {
            _phase++;
            _frames = 0;
        }

        private void Fail(string reason)
        {
            Debug.LogError($"Showcase input validation FAIL: {reason}");
            Finish();
        }

        private void Finish()
        {
            _finished = true;
            RemoveDevices();
        }

        private void OnDestroy()
        {
            RemoveDevices();
        }

        private void RemoveDevices()
        {
            if (_mouse != null && _mouse.added) InputSystem.RemoveDevice(_mouse);
            if (_keyboard != null && _keyboard.added) InputSystem.RemoveDevice(_keyboard);
            _mouse = null;
            _keyboard = null;
        }
    }
}
