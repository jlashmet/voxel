using UnityEngine;

namespace Game.Kentridge.PlayableSlice
{
    /// <summary>
    /// Keeps the legacy Kentridge scene's direct Unity input reads explicit after the scene assembly
    /// began referencing Game.Input for menu-context ownership. The local type intentionally wins
    /// name lookup over the sibling Game.Input namespace until the slice's remaining exploration
    /// controls are migrated to the reusable input runtime.
    /// </summary>
    internal static class Input
    {
        public static bool GetKeyDown(KeyCode key) => UnityEngine.Input.GetKeyDown(key);
        public static bool GetMouseButtonDown(int button) => UnityEngine.Input.GetMouseButtonDown(button);
        public static float GetAxisRaw(string axisName) => UnityEngine.Input.GetAxisRaw(axisName);
        public static bool GetKey(KeyCode key) => UnityEngine.Input.GetKey(key);
        public static void ResetInputAxes() => UnityEngine.Input.ResetInputAxes();
    }
}
