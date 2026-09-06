using UnityEngine;
using UnityEngine.InputSystem;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// One frame of semantic showcase controls. The player-facing driver consumes this snapshot
    /// instead of owning device/key policy in each movement/edit method, so every scene that uses
    /// <see cref="VoxelShowcase"/> shares the same Input System contract.
    /// </summary>
    internal readonly struct ShowcaseInputFrame
    {
        internal ShowcaseInputFrame(
            bool toggleCursor,
            bool toggleFly,
            bool respawn,
            bool interact,
            bool forward,
            bool backward,
            bool right,
            bool left,
            bool sprint,
            bool jump,
            bool descend,
            bool primaryEdit,
            bool secondaryEdit,
            Vector2 lookDelta,
            int scrollDirection)
        {
            ToggleCursor = toggleCursor;
            ToggleFly = toggleFly;
            Respawn = respawn;
            Interact = interact;
            Forward = forward;
            Backward = backward;
            Right = right;
            Left = left;
            Sprint = sprint;
            Jump = jump;
            Descend = descend;
            PrimaryEdit = primaryEdit;
            SecondaryEdit = secondaryEdit;
            LookDelta = lookDelta;
            ScrollDirection = scrollDirection;
        }

        internal bool ToggleCursor { get; }
        internal bool ToggleFly { get; }
        internal bool Respawn { get; }
        internal bool Interact { get; }
        internal bool Forward { get; }
        internal bool Backward { get; }
        internal bool Right { get; }
        internal bool Left { get; }
        internal bool Sprint { get; }
        internal bool Jump { get; }
        internal bool Descend { get; }
        internal bool PrimaryEdit { get; }
        internal bool SecondaryEdit { get; }
        internal Vector2 LookDelta { get; }
        internal int ScrollDirection { get; }
    }

    internal static class ShowcaseInputSystem
    {
        // ProjectSettings/InputManager.asset configured legacy Mouse X/Y at sensitivity 0.1.
        // Input System mouse delta is pixel-like, so preserve the shipped look scale here while
        // keeping VoxelShowcase.m_LookSensitivity authoritative for player tuning.
        internal const float LegacyMouseAxisScale = 0.1f;

        internal static ShowcaseInputFrame ReadCurrent()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            Vector2 look = mouse != null
                ? mouse.delta.ReadValue() * LegacyMouseAxisScale
                : Vector2.zero;
            float wheel = mouse != null ? mouse.scroll.ReadValue().y : 0f;
            int scrollDirection = wheel > 0.01f ? 1 : wheel < -0.01f ? -1 : 0;

            return new ShowcaseInputFrame(
                toggleCursor: keyboard != null && keyboard.escapeKey.wasPressedThisFrame,
                toggleFly: keyboard != null && keyboard.fKey.wasPressedThisFrame,
                respawn: keyboard != null && keyboard.rKey.wasPressedThisFrame,
                interact: keyboard != null && keyboard.eKey.wasPressedThisFrame,
                forward: keyboard != null && keyboard.wKey.isPressed,
                backward: keyboard != null && keyboard.sKey.isPressed,
                right: keyboard != null && keyboard.dKey.isPressed,
                left: keyboard != null && keyboard.aKey.isPressed,
                sprint: keyboard != null && keyboard.leftShiftKey.isPressed,
                jump: keyboard != null && keyboard.spaceKey.isPressed,
                descend: keyboard != null && keyboard.leftCtrlKey.isPressed,
                primaryEdit: mouse != null && mouse.leftButton.wasPressedThisFrame,
                secondaryEdit: mouse != null && mouse.rightButton.wasPressedThisFrame,
                lookDelta: look,
                scrollDirection: scrollDirection);
        }
    }
}
