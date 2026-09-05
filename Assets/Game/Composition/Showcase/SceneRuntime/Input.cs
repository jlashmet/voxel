using Game.Input.Runtime;
using UnityEngine;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Compatibility facade for the Showcase assembly's existing legacy-shaped input calls.
    /// Physical device state is owned by Game.Input.Runtime; this type only preserves the local
    /// call shape while the showcase driver remains source-compatible with the Input System-only
    /// player configuration.
    /// </summary>
    internal static class Input
    {
        public static bool GetKey(KeyCode key) => UnityInputCompatibility.GetKey(key);
        public static bool GetKeyDown(KeyCode key) => UnityInputCompatibility.GetKeyDown(key);
        public static bool GetKeyUp(KeyCode key) => UnityInputCompatibility.GetKeyUp(key);
        public static bool GetKey(string keyName) => UnityInputCompatibility.GetKey(keyName);
        public static bool GetKeyDown(string keyName) => UnityInputCompatibility.GetKeyDown(keyName);
        public static bool GetKeyUp(string keyName) => UnityInputCompatibility.GetKeyUp(keyName);
        public static float GetAxis(string axisName) => UnityInputCompatibility.GetAxis(axisName);
        public static float GetAxisRaw(string axisName) => UnityInputCompatibility.GetAxisRaw(axisName);
        public static bool GetButton(string buttonName) => UnityInputCompatibility.GetButton(buttonName);
        public static bool GetButtonDown(string buttonName) => UnityInputCompatibility.GetButtonDown(buttonName);
        public static bool GetButtonUp(string buttonName) => UnityInputCompatibility.GetButtonUp(buttonName);
        public static bool GetMouseButton(int button) => UnityInputCompatibility.GetMouseButton(button);
        public static bool GetMouseButtonDown(int button) => UnityInputCompatibility.GetMouseButtonDown(button);
        public static bool GetMouseButtonUp(int button) => UnityInputCompatibility.GetMouseButtonUp(button);
        public static Vector3 mousePosition => UnityInputCompatibility.MousePosition;
        public static Vector2 mouseScrollDelta => UnityInputCompatibility.MouseScrollDelta;
        public static bool mousePresent => UnityInputCompatibility.MousePresent;
        public static bool anyKey => UnityInputCompatibility.AnyKey;
        public static bool anyKeyDown => UnityInputCompatibility.AnyKeyDown;
        public static void ResetInputAxes() => UnityInputCompatibility.ResetInputAxes();
    }
}
