using System;
using System.Collections.Generic;
using Game.Input.Api;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Input.Runtime
{
    public sealed class InputContextService : IInputContextService
    {
        private readonly List<Entry> _stack = new List<Entry>();
        private int _nextToken = 1;

        public InputContextId ActiveContext =>
            _stack.Count == 0 ? InputContextId.Exploration : _stack[_stack.Count - 1].Context;

        public IInputContextLease Push(InputContextId context)
        {
            int token = _nextToken++;
            _stack.Add(new Entry(token, context));
            return new Lease(this, token, context);
        }

        private void Release(int token)
        {
            for (int i = _stack.Count - 1; i >= 0; i--)
            {
                if (_stack[i].Token != token) continue;
                _stack.RemoveAt(i);
                return;
            }
        }

        private readonly struct Entry
        {
            public int Token { get; }
            public InputContextId Context { get; }

            public Entry(int token, InputContextId context)
            {
                Token = token;
                Context = context;
            }
        }

        private sealed class Lease : IInputContextLease
        {
            private InputContextService _owner;
            private readonly int _token;
            public InputContextId Context { get; }

            public Lease(InputContextService owner, int token, InputContextId context)
            {
                _owner = owner;
                _token = token;
                Context = context;
            }

            public void Dispose()
            {
                InputContextService owner = _owner;
                if (owner == null) return;
                _owner = null;
                owner.Release(_token);
            }
        }
    }

    /// <summary>
    /// Production physical-input adapter. One composition-owned instance can feed gameplay snapshots,
    /// semantic action state, presentation labels and persisted binding overrides without any
    /// UnityEngine.Input fallback.
    /// </summary>
    public sealed class UnityPlayerInputReader :
        IPlayerInputReader,
        IInputBindingOverrideService,
        IInputBindingPresentation,
        IInputActionStateReader,
        IDisposable
    {
        private readonly IInputContextService _contexts;
        private readonly InputActionMap _actions;
        private readonly InputAction _move;
        private readonly InputAction _pointer;
        private readonly InputAction _primary;
        private readonly InputAction _secondary;
        private readonly InputAction _confirm;
        private readonly InputAction _cancel;
        private readonly InputAction _interact;
        private readonly InputAction _jump;
        private readonly InputAction _sprint;
        private bool _disposed;

        public UnityPlayerInputReader(IInputContextService contexts)
        {
            _contexts = contexts ?? throw new ArgumentNullException(nameof(contexts));
            _actions = new InputActionMap("Player");

            _move = _actions.AddAction("Move", InputActionType.Value);
            _move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            _move.AddBinding("<Gamepad>/leftStick");

            _pointer = _actions.AddAction("Pointer", InputActionType.Value);
            _pointer.AddBinding("<Pointer>/delta");
            _pointer.AddBinding("<Gamepad>/rightStick");

            _primary = _actions.AddAction("Primary", InputActionType.Button);
            _primary.AddBinding("<Mouse>/leftButton");
            _primary.AddBinding("<Gamepad>/buttonSouth");

            _secondary = _actions.AddAction("Secondary", InputActionType.Button);
            _secondary.AddBinding("<Mouse>/rightButton");
            _secondary.AddBinding("<Gamepad>/buttonWest");

            _confirm = _actions.AddAction("Confirm", InputActionType.Button);
            _confirm.AddBinding("<Keyboard>/enter");
            _confirm.AddBinding("<Keyboard>/space");
            _confirm.AddBinding("<Gamepad>/buttonSouth");

            _cancel = _actions.AddAction("Cancel", InputActionType.Button);
            _cancel.AddBinding("<Keyboard>/escape");
            _cancel.AddBinding("<Gamepad>/buttonEast");

            _interact = _actions.AddAction("Interact", InputActionType.Button);
            _interact.AddBinding("<Keyboard>/e");
            _interact.AddBinding("<Gamepad>/buttonSouth");

            _jump = _actions.AddAction("Jump", InputActionType.Button);
            _jump.AddBinding("<Keyboard>/space");
            _jump.AddBinding("<Gamepad>/buttonSouth");

            _sprint = _actions.AddAction("Sprint", InputActionType.Button);
            _sprint.AddBinding("<Keyboard>/leftShift");
            _sprint.AddBinding("<Gamepad>/leftStickPress");

            _actions.Enable();
        }

        public PlayerInputSnapshot Read(LocalPlayerId player)
        {
            ThrowIfDisposed();
            InputContextId context = _contexts.ActiveContext;
            if (context == InputContextId.Disabled || context == InputContextId.Ui)
                return default;

            Vector2 move = _move.ReadValue<Vector2>();
            Vector2 pointer = _pointer.ReadValue<Vector2>();
            return new PlayerInputSnapshot(
                move.x,
                move.y,
                pointer.x,
                pointer.y,
                _primary.WasPressedThisFrame(),
                _secondary.WasPressedThisFrame(),
                _confirm.WasPressedThisFrame(),
                _cancel.WasPressedThisFrame());
        }

        public bool WasPressed(LocalPlayerId player, InputActionId action)
        {
            ThrowIfDisposed();
            if (!InputAllowed()) return false;
            InputAction input = ResolveSemanticAction(action);
            return input != null && input.WasPressedThisFrame();
        }

        public bool IsHeld(LocalPlayerId player, InputActionId action)
        {
            ThrowIfDisposed();
            if (!InputAllowed()) return false;
            InputAction input = ResolveSemanticAction(action);
            return input != null && input.IsPressed();
        }

        public bool TryGetDisplayLabel(LocalPlayerId player, InputActionId action, out string displayLabel)
        {
            ThrowIfDisposed();
            InputAction input = ResolveSemanticAction(action);
            if (input == null || input.bindings.Count == 0)
            {
                displayLabel = string.Empty;
                return false;
            }

            for (int i = 0; i < input.bindings.Count; i++)
            {
                InputBinding binding = input.bindings[i];
                if (binding.isComposite || binding.isPartOfComposite) continue;
                string path = binding.effectivePath;
                if (string.IsNullOrWhiteSpace(path)) continue;
                displayLabel = InputControlPath.ToHumanReadableString(
                    path,
                    InputControlPath.HumanReadableStringOptions.OmitDevice);
                if (!string.IsNullOrWhiteSpace(displayLabel)) return true;
            }

            displayLabel = input.name;
            return true;
        }

        public IReadOnlyList<InputBindingOverride> SnapshotOverrides()
        {
            ThrowIfDisposed();
            var result = new List<InputBindingOverride>();
            foreach (InputAction action in _actions.actions)
            {
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    InputBinding binding = action.bindings[i];
                    if (string.IsNullOrWhiteSpace(binding.overridePath)) continue;
                    result.Add(new InputBindingOverride(action.name, i, binding.overridePath));
                }
            }
            return result;
        }

        public bool TryApplyOverride(InputBindingOverride bindingOverride, out string error)
        {
            ThrowIfDisposed();
            InputAction action = _actions.FindAction(bindingOverride.ActionId, false);
            if (action == null)
            {
                error = "Unknown input action: " + bindingOverride.ActionId;
                return false;
            }
            if (bindingOverride.BindingIndex < 0 || bindingOverride.BindingIndex >= action.bindings.Count)
            {
                error = "Binding index is outside action bindings: " + bindingOverride.BindingIndex;
                return false;
            }

            action.ApplyBindingOverride(bindingOverride.BindingIndex, bindingOverride.OverridePath);
            error = string.Empty;
            return true;
        }

        public void ClearOverrides()
        {
            ThrowIfDisposed();
            _actions.RemoveAllBindingOverrides();
        }

        [Obsolete("Legacy UnityEngine.Input suppression is no longer required; device ownership is centralized in the Input System adapter.")]
        public void SuppressLegacyReadersForCurrentFrame()
        {
            // Compatibility no-op. No legacy input API is touched.
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _actions.Disable();
            _actions.Dispose();
        }

        private bool InputAllowed()
        {
            InputContextId context = _contexts.ActiveContext;
            return context != InputContextId.Disabled && context != InputContextId.Ui;
        }

        private InputAction ResolveSemanticAction(InputActionId action)
        {
            if (action == StandardInputActions.Interact) return _interact;
            if (action == StandardInputActions.Cancel) return _cancel;
            if (action == StandardInputActions.Jump) return _jump;
            if (action == StandardInputActions.Sprint) return _sprint;
            return null;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(UnityPlayerInputReader));
        }
    }
}