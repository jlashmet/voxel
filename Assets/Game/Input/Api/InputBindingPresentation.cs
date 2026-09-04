using System;

namespace Game.Input.Api
{
    public readonly struct InputActionId : IEquatable<InputActionId>, IComparable<InputActionId>
    {
        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);

        public InputActionId(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Input action id is required.", nameof(value));
            Value = value;
        }

        public int CompareTo(InputActionId other) => StringComparer.Ordinal.Compare(Value ?? string.Empty, other.Value ?? string.Empty);
        public bool Equals(InputActionId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is InputActionId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(InputActionId left, InputActionId right) => left.Equals(right);
        public static bool operator !=(InputActionId left, InputActionId right) => !left.Equals(right);
    }

    public static class StandardInputActions
    {
        public static readonly InputActionId Interact = new InputActionId("interact");
        public static readonly InputActionId Cancel = new InputActionId("cancel");
        public static readonly InputActionId Jump = new InputActionId("jump");
        public static readonly InputActionId Sprint = new InputActionId("sprint");
    }

    public interface IInputBindingPresentation
    {
        bool TryGetDisplayLabel(LocalPlayerId player, InputActionId action, out string displayLabel);
    }

    public interface IInputActionReader
    {
        bool WasPressed(LocalPlayerId player, InputActionId action);
    }
}
