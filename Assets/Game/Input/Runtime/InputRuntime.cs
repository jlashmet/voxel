using System;
using System.Collections.Generic;
using Game.Input.Api;
using UnityEngine;

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

    public sealed class UnityPlayerInputReader : IPlayerInputReader
    {
        private readonly IInputContextService _contexts;

        public UnityPlayerInputReader(IInputContextService contexts)
        {
            _contexts = contexts ?? throw new ArgumentNullException(nameof(contexts));
        }

        public PlayerInputSnapshot Read(LocalPlayerId player)
        {
            InputContextId context = _contexts.ActiveContext;
            if (context == InputContextId.Disabled || context == InputContextId.Ui)
                return default;

            Vector3 pointer = UnityEngine.Input.mousePosition;
            return new PlayerInputSnapshot(
                UnityEngine.Input.GetAxisRaw("Horizontal"),
                UnityEngine.Input.GetAxisRaw("Vertical"),
                pointer.x,
                pointer.y,
                UnityEngine.Input.GetMouseButtonDown(0),
                UnityEngine.Input.GetMouseButtonDown(1),
                UnityEngine.Input.GetKeyDown(KeyCode.Return) || UnityEngine.Input.GetKeyDown(KeyCode.Space),
                UnityEngine.Input.GetKeyDown(KeyCode.Escape));
        }

        /// <summary>
        /// Transitional ownership gate for legacy scene controllers that still read UnityEngine.Input
        /// directly. Combat samples first, then clears the Unity frame so an older exploration reader
        /// cannot consume the same physical intent. This lives in Input.Runtime because device-state
        /// ownership must not leak into Combat or its deterministic simulation.
        /// </summary>
        public void SuppressLegacyReadersForCurrentFrame()
        {
            if (_contexts.ActiveContext == InputContextId.Combat)
                UnityEngine.Input.ResetInputAxes();
        }
    }
}
