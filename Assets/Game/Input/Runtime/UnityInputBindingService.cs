using System;
using System.Collections.Generic;
using Game.Input.Api;
using UnityEngine;

namespace Game.Input.Runtime
{
    public sealed class UnityInputBindingService : IInputBindingPresentation, IInputActionReader
    {
        private readonly Dictionary<InputActionId, KeyCode> _bindings = new Dictionary<InputActionId, KeyCode>();

        public UnityInputBindingService()
        {
            _bindings[StandardInputActions.Interact] = KeyCode.E;
            _bindings[StandardInputActions.Cancel] = KeyCode.Escape;
            _bindings[StandardInputActions.Jump] = KeyCode.Space;
            _bindings[StandardInputActions.Sprint] = KeyCode.LeftShift;
        }

        public void Rebind(InputActionId action, KeyCode key)
        {
            if (!action.IsValid) throw new ArgumentException("Input action id is required.", nameof(action));
            _bindings[action] = key;
        }

        public bool TryGetDisplayLabel(LocalPlayerId player, InputActionId action, out string displayLabel)
        {
            if (_bindings.TryGetValue(action, out KeyCode key))
            {
                displayLabel = Format(key);
                return true;
            }
            displayLabel = string.Empty;
            return false;
        }

        public bool WasPressed(LocalPlayerId player, InputActionId action) =>
            _bindings.TryGetValue(action, out KeyCode key) && UnityEngine.Input.GetKeyDown(key);

        private static string Format(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.LeftShift: return "Shift";
                case KeyCode.RightShift: return "Shift";
                case KeyCode.Escape: return "Esc";
                case KeyCode.Return: return "Enter";
                default: return key.ToString();
            }
        }
    }
}
