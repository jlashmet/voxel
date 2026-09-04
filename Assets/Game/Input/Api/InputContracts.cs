using System;
using System.Collections.Generic;

namespace Game.Input.Api
{
    public readonly struct LocalPlayerId : IEquatable<LocalPlayerId>
    {
        public int Value { get; }

        public LocalPlayerId(int value)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            Value = value;
        }

        public bool Equals(LocalPlayerId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is LocalPlayerId other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => "Player" + Value;
    }

    public enum InputContextId
    {
        Exploration = 0,
        Combat = 1,
        Ui = 2,
        Disabled = 3
    }

    public readonly struct PlayerInputSnapshot
    {
        public float MoveX { get; }
        public float MoveY { get; }
        public float PointerX { get; }
        public float PointerY { get; }
        public bool PrimaryPressed { get; }
        public bool SecondaryPressed { get; }
        public bool ConfirmPressed { get; }
        public bool CancelPressed { get; }

        public PlayerInputSnapshot(
            float moveX,
            float moveY,
            float pointerX,
            float pointerY,
            bool primaryPressed,
            bool secondaryPressed,
            bool confirmPressed,
            bool cancelPressed)
        {
            MoveX = moveX;
            MoveY = moveY;
            PointerX = pointerX;
            PointerY = pointerY;
            PrimaryPressed = primaryPressed;
            SecondaryPressed = secondaryPressed;
            ConfirmPressed = confirmPressed;
            CancelPressed = cancelPressed;
        }
    }

    public readonly struct InputBindingOverride : IEquatable<InputBindingOverride>
    {
        public string ActionId { get; }
        public int BindingIndex { get; }
        public string OverridePath { get; }

        public InputBindingOverride(string actionId, int bindingIndex, string overridePath)
        {
            if (string.IsNullOrWhiteSpace(actionId)) throw new ArgumentException("Action id is required.", nameof(actionId));
            if (bindingIndex < 0) throw new ArgumentOutOfRangeException(nameof(bindingIndex));
            if (string.IsNullOrWhiteSpace(overridePath)) throw new ArgumentException("Override path is required.", nameof(overridePath));
            ActionId = actionId.Trim();
            BindingIndex = bindingIndex;
            OverridePath = overridePath.Trim();
        }

        public bool Equals(InputBindingOverride other) =>
            BindingIndex == other.BindingIndex &&
            string.Equals(ActionId, other.ActionId, StringComparison.Ordinal) &&
            string.Equals(OverridePath, other.OverridePath, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is InputBindingOverride other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = StringComparer.Ordinal.GetHashCode(ActionId ?? string.Empty);
                hash = (hash * 397) ^ BindingIndex;
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(OverridePath ?? string.Empty);
                return hash;
            }
        }
    }

    public interface IPlayerInputReader
    {
        PlayerInputSnapshot Read(LocalPlayerId player);
    }

    public interface IInputContextLease : IDisposable
    {
        InputContextId Context { get; }
    }

    public interface IInputContextService
    {
        InputContextId ActiveContext { get; }
        IInputContextLease Push(InputContextId context);
    }

    public interface IInputBindingOverrideService
    {
        IReadOnlyList<InputBindingOverride> SnapshotOverrides();
        bool TryApplyOverride(InputBindingOverride bindingOverride, out string error);
        void ClearOverrides();
    }
}
