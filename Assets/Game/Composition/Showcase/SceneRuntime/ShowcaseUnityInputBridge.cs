using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Scene-local compatibility boundary for the legacy-shaped Showcase controls. The project is
    /// Input-System-only, so production showcase code cannot call UnityEngine.Input directly.
    /// Keeping the physical-device mapping here lets the existing scene remain unchanged while all
    /// reads flow through the supported Input System package.
    /// </summary>
    internal static class Input
    {
        private static int s_ResetFrame = -1;

        public static bool GetKey(KeyCode key)
        {
            if (Time.frameCount == s_ResetFrame) return false;
            ButtonControl control = ResolveKey(key);
            return control != null && control.isPressed;
        }

        public static bool GetKeyDown(KeyCode key)
        {
            if (Time.frameCount == s_ResetFrame) return false;
            ButtonControl control = ResolveKey(key);
            return control != null && control.wasPressedThisFrame;
        }

        public static bool GetKeyUp(KeyCode key)
        {
            if (Time.frameCount == s_ResetFrame) return false;
            ButtonControl control = ResolveKey(key);
            return control != null && control.wasReleasedThisFrame;
        }

        public static bool GetMouseButton(int button)
        {
            if (Time.frameCount == s_ResetFrame) return false;
            ButtonControl control = ResolveMouseButton(button);
            return control != null && control.isPressed;
        }

        public static bool GetMouseButtonDown(int button)
        {
            if (Time.frameCount == s_ResetFrame) return false;
            ButtonControl control = ResolveMouseButton(button);
            return control != null && control.wasPressedThisFrame;
        }

        public static bool GetMouseButtonUp(int button)
        {
            if (Time.frameCount == s_ResetFrame) return false;
            ButtonControl control = ResolveMouseButton(button);
            return control != null && control.wasReleasedThisFrame;
        }

        public static float GetAxis(string axisName) => GetAxisRaw(axisName);

        public static float GetAxisRaw(string axisName)
        {
            if (Time.frameCount == s_ResetFrame) return 0f;

            if (axisName == "Mouse X")
                return Mouse.current?.delta.ReadValue().x ?? 0f;
            if (axisName == "Mouse Y")
                return Mouse.current?.delta.ReadValue().y ?? 0f;
            if (axisName == "Mouse ScrollWheel")
                return Mathf.Clamp((Mouse.current?.scroll.ReadValue().y ?? 0f) / 120f, -1f, 1f);

            if (axisName == "Horizontal")
            {
                float keyboard = DigitalAxis(KeyCode.A, KeyCode.D);
                if (!Mathf.Approximately(keyboard, 0f)) return keyboard;
                return Gamepad.current?.leftStick.x.ReadValue() ?? 0f;
            }

            if (axisName == "Vertical")
            {
                float keyboard = DigitalAxis(KeyCode.S, KeyCode.W);
                if (!Mathf.Approximately(keyboard, 0f)) return keyboard;
                return Gamepad.current?.leftStick.y.ReadValue() ?? 0f;
            }

            return 0f;
        }

        public static bool GetButton(string buttonName)
        {
            if (buttonName == "Jump") return GetKey(KeyCode.Space);
            if (buttonName == "Submit") return GetKey(KeyCode.Return);
            if (buttonName == "Cancel") return GetKey(KeyCode.Escape);
            return false;
        }

        public static bool GetButtonDown(string buttonName)
        {
            if (buttonName == "Jump") return GetKeyDown(KeyCode.Space);
            if (buttonName == "Submit") return GetKeyDown(KeyCode.Return);
            if (buttonName == "Cancel") return GetKeyDown(KeyCode.Escape);
            return false;
        }

        public static bool GetButtonUp(string buttonName)
        {
            if (buttonName == "Jump") return GetKeyUp(KeyCode.Space);
            if (buttonName == "Submit") return GetKeyUp(KeyCode.Return);
            if (buttonName == "Cancel") return GetKeyUp(KeyCode.Escape);
            return false;
        }

        public static Vector3 mousePosition
        {
            get
            {
                Vector2 value = Mouse.current?.position.ReadValue() ?? Vector2.zero;
                return new Vector3(value.x, value.y, 0f);
            }
        }

        public static Vector2 mouseScrollDelta => Mouse.current?.scroll.ReadValue() ?? Vector2.zero;

        /// <summary>
        /// Legacy ResetInputAxes was used as a one-frame fence after focus/modal transitions. Do
        /// not mutate Input System device state; suppress scene reads for the rest of this frame.
        /// </summary>
        public static void ResetInputAxes() => s_ResetFrame = Time.frameCount;

        private static float DigitalAxis(KeyCode negative, KeyCode positive)
        {
            float value = 0f;
            if (GetKey(negative)) value -= 1f;
            if (GetKey(positive)) value += 1f;
            return value;
        }

        private static ButtonControl ResolveMouseButton(int button)
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return null;
            switch (button)
            {
                case 0: return mouse.leftButton;
                case 1: return mouse.rightButton;
                case 2: return mouse.middleButton;
                case 3: return mouse.backButton;
                case 4: return mouse.forwardButton;
                default: return null;
            }
        }

        private static ButtonControl ResolveKey(KeyCode key)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return null;
            switch (key)
            {
                case KeyCode.A: return keyboard.aKey;
                case KeyCode.B: return keyboard.bKey;
                case KeyCode.C: return keyboard.cKey;
                case KeyCode.D: return keyboard.dKey;
                case KeyCode.E: return keyboard.eKey;
                case KeyCode.F: return keyboard.fKey;
                case KeyCode.G: return keyboard.gKey;
                case KeyCode.H: return keyboard.hKey;
                case KeyCode.I: return keyboard.iKey;
                case KeyCode.J: return keyboard.jKey;
                case KeyCode.K: return keyboard.kKey;
                case KeyCode.L: return keyboard.lKey;
                case KeyCode.M: return keyboard.mKey;
                case KeyCode.N: return keyboard.nKey;
                case KeyCode.O: return keyboard.oKey;
                case KeyCode.P: return keyboard.pKey;
                case KeyCode.Q: return keyboard.qKey;
                case KeyCode.R: return keyboard.rKey;
                case KeyCode.S: return keyboard.sKey;
                case KeyCode.T: return keyboard.tKey;
                case KeyCode.U: return keyboard.uKey;
                case KeyCode.V: return keyboard.vKey;
                case KeyCode.W: return keyboard.wKey;
                case KeyCode.X: return keyboard.xKey;
                case KeyCode.Y: return keyboard.yKey;
                case KeyCode.Z: return keyboard.zKey;
                case KeyCode.Alpha0: return keyboard.digit0Key;
                case KeyCode.Alpha1: return keyboard.digit1Key;
                case KeyCode.Alpha2: return keyboard.digit2Key;
                case KeyCode.Alpha3: return keyboard.digit3Key;
                case KeyCode.Alpha4: return keyboard.digit4Key;
                case KeyCode.Alpha5: return keyboard.digit5Key;
                case KeyCode.Alpha6: return keyboard.digit6Key;
                case KeyCode.Alpha7: return keyboard.digit7Key;
                case KeyCode.Alpha8: return keyboard.digit8Key;
                case KeyCode.Alpha9: return keyboard.digit9Key;
                case KeyCode.Space: return keyboard.spaceKey;
                case KeyCode.Return: return keyboard.enterKey;
                case KeyCode.KeypadEnter: return keyboard.numpadEnterKey;
                case KeyCode.Escape: return keyboard.escapeKey;
                case KeyCode.Tab: return keyboard.tabKey;
                case KeyCode.Backspace: return keyboard.backspaceKey;
                case KeyCode.Delete: return keyboard.deleteKey;
                case KeyCode.LeftShift: return keyboard.leftShiftKey;
                case KeyCode.RightShift: return keyboard.rightShiftKey;
                case KeyCode.LeftControl: return keyboard.leftCtrlKey;
                case KeyCode.RightControl: return keyboard.rightCtrlKey;
                case KeyCode.LeftAlt: return keyboard.leftAltKey;
                case KeyCode.RightAlt: return keyboard.rightAltKey;
                case KeyCode.UpArrow: return keyboard.upArrowKey;
                case KeyCode.DownArrow: return keyboard.downArrowKey;
                case KeyCode.LeftArrow: return keyboard.leftArrowKey;
                case KeyCode.RightArrow: return keyboard.rightArrowKey;
                case KeyCode.F1: return keyboard.f1Key;
                case KeyCode.F2: return keyboard.f2Key;
                case KeyCode.F3: return keyboard.f3Key;
                case KeyCode.F4: return keyboard.f4Key;
                case KeyCode.F5: return keyboard.f5Key;
                case KeyCode.F6: return keyboard.f6Key;
                case KeyCode.F7: return keyboard.f7Key;
                case KeyCode.F8: return keyboard.f8Key;
                case KeyCode.F9: return keyboard.f9Key;
                case KeyCode.F10: return keyboard.f10Key;
                case KeyCode.F11: return keyboard.f11Key;
                case KeyCode.F12: return keyboard.f12Key;
                default: return null;
            }
        }
    }
}
