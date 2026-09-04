using System;
using Game.Input.Api;
using Game.Input.Runtime;
using UnityEngine;

namespace Game.Input.Validation
{
    public sealed class InputSystemValidationShowcase : MonoBehaviour
    {
        private InputContextService _contexts;
        private UnityPlayerInputReader _reader;
        private string _backend = "pending";
        private string _contextProof = "pending";
        private string _bindingProof = "pending";

        private void Start()
        {
            try
            {
                _contexts = new InputContextService();
                _reader = new UnityPlayerInputReader(_contexts);
                _backend = "InputSystem";
                Debug.Log("INPUT_VALIDATION backend=InputSystem context=Exploration");

                using (_contexts.Push(InputContextId.Ui))
                {
                    PlayerInputSnapshot suppressed = _reader.Read(new LocalPlayerId(0));
                    if (suppressed.MoveX != 0f || suppressed.MoveY != 0f || suppressed.PrimaryPressed)
                        throw new InvalidOperationException("UI context leaked gameplay input.");
                    _contextProof = "Ui suppressed; Exploration restored";
                }
                if (_contexts.ActiveContext != InputContextId.Exploration)
                    throw new InvalidOperationException("Input context did not restore after UI lease.");
                Debug.Log("INPUT_VALIDATION context-unwind: active=Exploration uiSuppressed=True");

                var binding = new InputBindingOverride("Confirm", 0, "<Keyboard>/f");
                if (!_reader.TryApplyOverride(binding, out string error)) throw new InvalidOperationException(error);
                if (_reader.SnapshotOverrides().Count != 1 || !_reader.SnapshotOverrides()[0].Equals(binding))
                    throw new InvalidOperationException("Binding override did not round-trip.");
                _bindingProof = "Confirm[0] -> <Keyboard>/f";
                Debug.Log("INPUT_VALIDATION binding-override: action=Confirm index=0 path=<Keyboard>/f");
            }
            catch (Exception ex)
            {
                Debug.LogError("INPUT_VALIDATION failure: " + ex);
                throw;
            }
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(40f, 40f, 700f, 300f), GUI.skin.box);
            GUILayout.Label("GAME.INPUT — PRODUCTION INPUT SYSTEM VALIDATION");
            GUILayout.Label("Backend: " + _backend);
            GUILayout.Label("Context ownership: " + _contextProof);
            GUILayout.Label("Binding override: " + _bindingProof);
            GUILayout.Label("Physical devices remain inside Game.Input.Runtime; callers consume Game.Input.Api snapshots.");
            GUILayout.EndArea();
        }

        private void OnDestroy()
        {
            _reader?.Dispose();
        }
    }
}
