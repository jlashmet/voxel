using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Opt-in standalone-player evidence for SceneIssues that must prove the real showcase input
    /// path. The harness is inert unless the replay issue explicitly requests the supported action;
    /// it queues Input System device events and observes production <see cref="VoxelShowcase"/>
    /// state rather than moving the camera/player directly.
    /// </summary>
    public static class SceneIssueInputReplayHarness
    {
        private const string ReplayArgument = "-voxel-scene-issue";
        private const string InputSmokeAction = "showcase-input-smoke";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            string issuePath = Argument(ReplayArgument);
            if (string.IsNullOrEmpty(issuePath) || !File.Exists(issuePath)) return;

            IssueRecord record;
            try
            {
                record = JsonUtility.FromJson<IssueRecord>(File.ReadAllText(issuePath));
            }
            catch (Exception error)
            {
                Debug.LogError($"SCENEISSUE input replay could not parse issue.json: {error.Message}");
                return;
            }

            if (!string.Equals(record?.replayAction, InputSmokeAction, StringComparison.OrdinalIgnoreCase))
                return;

            var root = new GameObject("Scene Issue Input Replay Harness")
            {
                hideFlags = HideFlags.DontSave
            };
            root.AddComponent<InputSmokeReplay>();
            UnityEngine.Object.DontDestroyOnLoad(root);
            Debug.Log("SCENEISSUE Input System smoke replay armed.");
        }

        private static string Argument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (string.Equals(args[i], name, StringComparison.Ordinal))
                    return args[i + 1];
            return null;
        }

        [Serializable]
        private sealed class IssueRecord
        {
            public string replayAction;
        }

        [DefaultExecutionOrder(9000)]
        private sealed class InputSmokeReplay : MonoBehaviour
        {
            private VoxelShowcase _showcase;
            private Keyboard _keyboard;
            private Mouse _mouse;
            private Vector3 _startPosition;
            private Quaternion _startRotation;
            private int _phase;
            private int _frames;
            private bool _finished;

            private void Update()
            {
                if (_finished) return;

                if (_showcase == null)
                {
                    _showcase = UnityEngine.Object.FindFirstObjectByType<VoxelShowcase>();
                    if (_showcase == null) return;
                    _keyboard = InputSystem.AddDevice<Keyboard>();
                    _mouse = InputSystem.AddDevice<Mouse>();
                    _phase = 1;
                    _frames = 0;
                    return;
                }

                _frames++;
                switch (_phase)
                {
                    case 1:
                        // Use the production F binding to enter fly mode. That removes terrain
                        // readiness from the movement proof without bypassing the input path.
                        InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.F));
                        _phase = 2;
                        _frames = 0;
                        break;

                    case 2:
                        if (_frames < 2) return;
                        InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
                        _phase = 3;
                        _frames = 0;
                        break;

                    case 3:
                        if (_frames < 2) return;
                        _startPosition = _showcase.transform.position;
                        _startRotation = _showcase.transform.rotation;
                        InputSystem.QueueStateEvent(
                            _keyboard, new KeyboardState(Key.W, Key.LeftShift));
                        InputSystem.QueueStateEvent(
                            _mouse, new MouseState { delta = new Vector2(32f, -14f) });
                        _phase = 4;
                        _frames = 0;
                        break;

                    case 4:
                        if (_frames < 10) return;
                        InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
                        float moved = Vector3.Distance(_startPosition, _showcase.transform.position);
                        float looked = Quaternion.Angle(_startRotation, _showcase.transform.rotation);
                        if (moved > 0.05f && looked > 0.25f)
                        {
                            Debug.Log(
                                $"SCENEISSUE Input System smoke PASS moved={moved:0.###}m " +
                                $"looked={looked:0.###}deg scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
                            Finish();
                        }
                        else
                        {
                            Debug.LogError(
                                $"SCENEISSUE Input System smoke FAIL moved={moved:0.###}m " +
                                $"looked={looked:0.###}deg");
                            Finish();
                        }
                        break;
                }
            }

            private void OnDestroy()
            {
                RemoveDevices();
            }

            private void Finish()
            {
                _finished = true;
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
}
