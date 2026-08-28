using System;

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
}
