using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Game.Kentridge.PlayableSlice
{
    /// <summary>
    /// Scene-local compatibility bridge for the remaining Kentridge exploration controls. The project
    /// is Input-System-only, so legacy UnityEngine.Input calls are invalid in both the player and CI.
    /// Keep physical-device knowledge here while the rest of the slice continues to use its existing
    /// KeyCode/axis-shaped call sites.
    /// </summary>
    internal static class Input
    {
        private static int s_ResetFrame = -1;

        public static bool GetKeyDown(KeyCode key)
        {
            if (Time.frameCount == s_ResetFrame) return false;
            KeyControl control = ResolveKey(key);
            return control != null && control.wasPressedThisFrame;
        }

        public static bool GetMouseButtonDown(int button)
        {
            if (Time.frameCount == s_ResetFrame) return false;
            Mouse mouse = Mouse.current;
            if (mouse == null) return false;
            switch (button)
            {
                case 0: return mouse.leftButton.wasPressedThisFrame;
                case 1: return mouse.rightButton.wasPressedThisFrame;
                case 2: return mouse.middleButton.wasPressedThisFrame;
                default: return false;
            }
        }

        public static float GetAxisRaw(string axisName)
        {
            if (Time.frameCount == s_ResetFrame) return 0f;
            Mouse mouse = Mouse.current;
            if (mouse == null) return 0f;
            Vector2 delta = mouse.delta.ReadValue();
            if (axisName == "Mouse X") return delta.x;
            if (axisName == "Mouse Y") return delta.y;
            return 0f;
        }

        public static bool GetKey(KeyCode key)
        {
            if (Time.frameCount == s_ResetFrame) return false;
            KeyControl control = ResolveKey(key);
            return control != null && control.isPressed;
        }

        /// <summary>
        /// Legacy ResetInputAxes was used only as a one-frame input fence when modal UI closed.
        /// Preserve that behavior without mutating Input System device state.
        /// </summary>
        public static void ResetInputAxes() => s_ResetFrame = Time.frameCount;

        private static KeyControl ResolveKey(KeyCode key)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return null;
            switch (key)
            {
                case KeyCode.A: return keyboard.aKey;
                case KeyCode.D: return keyboard.dKey;
                case KeyCode.E: return keyboard.eKey;
                case KeyCode.F: return keyboard.fKey;
                case KeyCode.I: return keyboard.iKey;
                case KeyCode.Q: return keyboard.qKey;
                case KeyCode.R: return keyboard.rKey;
                case KeyCode.S: return keyboard.sKey;
                case KeyCode.W: return keyboard.wKey;
                case KeyCode.Space: return keyboard.spaceKey;
                case KeyCode.Return: return keyboard.enterKey;
                case KeyCode.KeypadEnter: return keyboard.numpadEnterKey;
                case KeyCode.Escape: return keyboard.escapeKey;
                case KeyCode.Tab: return keyboard.tabKey;
                case KeyCode.LeftShift: return keyboard.leftShiftKey;
                case KeyCode.RightShift: return keyboard.rightShiftKey;
                case KeyCode.LeftControl: return keyboard.leftCtrlKey;
                case KeyCode.RightControl: return keyboard.rightCtrlKey;
                case KeyCode.UpArrow: return keyboard.upArrowKey;
                case KeyCode.DownArrow: return keyboard.downArrowKey;
                case KeyCode.LeftArrow: return keyboard.leftArrowKey;
                case KeyCode.RightArrow: return keyboard.rightArrowKey;
                case KeyCode.F10: return keyboard.f10Key;
                default: return null;
            }
        }
    }
}
