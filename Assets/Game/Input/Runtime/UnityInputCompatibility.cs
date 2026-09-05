using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Game.Input.Runtime
{
    /// <summary>
    /// Input-System-backed compatibility surface for legacy-shaped callers that are being migrated
    /// away from <see cref="UnityEngine.Input"/>. Physical device ownership stays in Input.Runtime;
    /// composition assemblies can preserve their existing control flow without re-enabling the
    /// legacy Input Manager globally.
    /// </summary>
    public static class UnityInputCompatibility
    {
        private const float LegacyMouseDeltaScale = 0.05f;
        private const float LegacyScrollScale = 1f / 120f;
        private static int s_SuppressedAxisFrame = -1;

        public static bool GetKey(KeyCode key) => ReadKey(key, pressedThisFrame: false, releasedThisFrame: false);

        public static bool GetKeyDown(KeyCode key) => ReadKey(key, pressedThisFrame: true, releasedThisFrame: false);

        public static bool GetKeyUp(KeyCode key) => ReadKey(key, pressedThisFrame: false, releasedThisFrame: true);

        public static bool GetKey(string keyName) => TryParseLegacyKey(keyName, out KeyCode key) && GetKey(key);

        public static bool GetKeyDown(string keyName) => TryParseLegacyKey(keyName, out KeyCode key) && GetKeyDown(key);

        public static bool GetKeyUp(string keyName) => TryParseLegacyKey(keyName, out KeyCode key) && GetKeyUp(key);

        public static float GetAxis(string axisName) => GetAxisRaw(axisName);

        public static float GetAxisRaw(string axisName)
        {
            if (Time.frameCount == s_SuppressedAxisFrame) return 0f;

            switch (axisName)
            {
                case "Mouse X":
                    return Mouse.current == null ? 0f : Mouse.current.delta.ReadValue().x * LegacyMouseDeltaScale;
                case "Mouse Y":
                    return Mouse.current == null ? 0f : Mouse.current.delta.ReadValue().y * LegacyMouseDeltaScale;
                case "Mouse ScrollWheel":
                    return Mouse.current == null ? 0f : Mouse.current.scroll.ReadValue().y * LegacyScrollScale;
                case "Horizontal":
                    return ReadMoveAxis(horizontal: true);
                case "Vertical":
                    return ReadMoveAxis(horizontal: false);
                default:
                    return 0f;
            }
        }

        public static bool GetButton(string buttonName) => ReadNamedButton(buttonName, ButtonRead.Held);

        public static bool GetButtonDown(string buttonName) => ReadNamedButton(buttonName, ButtonRead.Pressed);

        public static bool GetButtonUp(string buttonName) => ReadNamedButton(buttonName, ButtonRead.Released);

        public static bool GetMouseButton(int button) => ReadMouseButton(button, ButtonRead.Held);

        public static bool GetMouseButtonDown(int button) => ReadMouseButton(button, ButtonRead.Pressed);

        public static bool GetMouseButtonUp(int button) => ReadMouseButton(button, ButtonRead.Released);

        public static Vector3 MousePosition
        {
            get
            {
                Vector2 position = Mouse.current == null ? Vector2.zero : Mouse.current.position.ReadValue();
                return new Vector3(position.x, position.y, 0f);
            }
        }

        public static Vector2 MouseScrollDelta =>
            Mouse.current == null ? Vector2.zero : Mouse.current.scroll.ReadValue();

        public static bool MousePresent => Mouse.current != null;
        public static bool AnyKey => Keyboard.current != null && Keyboard.current.anyKey.isPressed;
        public static bool AnyKeyDown => Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame;

        /// <summary>
        /// Legacy callers use this immediately after cursor relock to discard the pointer jump
        /// accumulated while focus was elsewhere. Input System deltas are frame-scoped, so suppress
        /// this frame's compatibility axes instead of mutating device state.
        /// </summary>
        public static void ResetInputAxes() => s_SuppressedAxisFrame = Time.frameCount;

        private static bool ReadKey(KeyCode legacyKey, bool pressedThisFrame, bool releasedThisFrame)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !TryMapKey(legacyKey, out Key key)) return false;
            KeyControl control = keyboard[key];
            if (control == null) return false;
            if (pressedThisFrame) return control.wasPressedThisFrame;
            if (releasedThisFrame) return control.wasReleasedThisFrame;
            return control.isPressed;
        }

        private static bool TryMapKey(KeyCode legacyKey, out Key key)
        {
            switch (legacyKey)
            {
                case KeyCode.LeftControl: key = Key.LeftCtrl; return true;
                case KeyCode.RightControl: key = Key.RightCtrl; return true;
                case KeyCode.Return: key = Key.Enter; return true;
                case KeyCode.BackQuote: key = Key.Backquote; return true;
                case KeyCode.Alpha0: key = Key.Digit0; return true;
                case KeyCode.Alpha1: key = Key.Digit1; return true;
                case KeyCode.Alpha2: key = Key.Digit2; return true;
                case KeyCode.Alpha3: key = Key.Digit3; return true;
                case KeyCode.Alpha4: key = Key.Digit4; return true;
                case KeyCode.Alpha5: key = Key.Digit5; return true;
                case KeyCode.Alpha6: key = Key.Digit6; return true;
                case KeyCode.Alpha7: key = Key.Digit7; return true;
                case KeyCode.Alpha8: key = Key.Digit8; return true;
                case KeyCode.Alpha9: key = Key.Digit9; return true;
                case KeyCode.Keypad0: key = Key.Numpad0; return true;
                case KeyCode.Keypad1: key = Key.Numpad1; return true;
                case KeyCode.Keypad2: key = Key.Numpad2; return true;
                case KeyCode.Keypad3: key = Key.Numpad3; return true;
                case KeyCode.Keypad4: key = Key.Numpad4; return true;
                case KeyCode.Keypad5: key = Key.Numpad5; return true;
                case KeyCode.Keypad6: key = Key.Numpad6; return true;
                case KeyCode.Keypad7: key = Key.Numpad7; return true;
                case KeyCode.Keypad8: key = Key.Numpad8; return true;
                case KeyCode.Keypad9: key = Key.Numpad9; return true;
            }

            return Enum.TryParse(legacyKey.ToString(), ignoreCase: false, out key) && key != Key.None;
        }

        private static bool TryParseLegacyKey(string keyName, out KeyCode key)
        {
            key = KeyCode.None;
            if (string.IsNullOrWhiteSpace(keyName)) return false;
            string normalized = keyName.Trim().Replace(" ", string.Empty);
            if (string.Equals(normalized, "leftctrl", StringComparison.OrdinalIgnoreCase)) normalized = "LeftControl";
            if (string.Equals(normalized, "rightctrl", StringComparison.OrdinalIgnoreCase)) normalized = "RightControl";
            if (string.Equals(normalized, "enter", StringComparison.OrdinalIgnoreCase)) normalized = "Return";
            return Enum.TryParse(normalized, ignoreCase: true, out key);
        }

        private static float ReadMoveAxis(bool horizontal)
        {
            float keyboardValue = 0f;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (horizontal)
                    keyboardValue = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f)
                                  - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f);
                else
                    keyboardValue = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1f : 0f)
                                  - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1f : 0f);
            }

            Gamepad gamepad = Gamepad.current;
            if (gamepad == null) return keyboardValue;
            Vector2 stick = gamepad.leftStick.ReadValue();
            float gamepadValue = horizontal ? stick.x : stick.y;
            return Mathf.Abs(gamepadValue) > Mathf.Abs(keyboardValue) ? gamepadValue : keyboardValue;
        }

        private enum ButtonRead
        {
            Held,
            Pressed,
            Released,
        }

        private static bool ReadNamedButton(string buttonName, ButtonRead read)
        {
            switch (buttonName)
            {
                case "Jump": return ReadKeyButton(KeyCode.Space, read);
                case "Submit": return ReadKeyButton(KeyCode.Return, read);
                case "Cancel": return ReadKeyButton(KeyCode.Escape, read);
                case "Fire1": return ReadMouseButton(0, read);
                case "Fire2": return ReadMouseButton(1, read);
                case "Fire3": return ReadMouseButton(2, read);
                default: return false;
            }
        }

        private static bool ReadKeyButton(KeyCode key, ButtonRead read)
        {
            switch (read)
            {
                case ButtonRead.Pressed: return GetKeyDown(key);
                case ButtonRead.Released: return GetKeyUp(key);
                default: return GetKey(key);
            }
        }

        private static bool ReadMouseButton(int button, ButtonRead read)
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return false;
            ButtonControl control;
            switch (button)
            {
                case 0: control = mouse.leftButton; break;
                case 1: control = mouse.rightButton; break;
                case 2: control = mouse.middleButton; break;
                default: return false;
            }

            switch (read)
            {
                case ButtonRead.Pressed: return control.wasPressedThisFrame;
                case ButtonRead.Released: return control.wasReleasedThisFrame;
                default: return control.isPressed;
            }
        }
    }
}
