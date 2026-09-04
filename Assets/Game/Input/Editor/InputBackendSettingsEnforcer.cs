using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Game.Input.Editor
{
    /// <summary>
    /// Keeps clean checkouts on the production Input System backend. The project predates the package and
    /// may still serialize the legacy backend value; enforcing it before builds prevents accidental fallback.
    /// </summary>
    [InitializeOnLoad]
    internal static class InputBackendSettingsEnforcer
    {
        private static readonly Regex ActiveInputHandler =
            new Regex("(?m)^  activeInputHandler: [0-9]+$", RegexOptions.Compiled);

        static InputBackendSettingsEnforcer()
        {
            EnsureInputSystemBackend();
        }

        [MenuItem("Tools/Game/Input/Ensure Input System Backend")]
        internal static void EnsureInputSystemBackend()
        {
            string root = Directory.GetParent(Application.dataPath).FullName;
            string path = Path.Combine(root, "ProjectSettings", "ProjectSettings.asset");
            string text = File.ReadAllText(path);
            Match match = ActiveInputHandler.Match(text);
            if (!match.Success)
                throw new InvalidDataException("ProjectSettings activeInputHandler was not found.");
            if (match.Value.EndsWith(" 1")) return;

            File.WriteAllText(path, ActiveInputHandler.Replace(text, "  activeInputHandler: 1", 1));
            Debug.Log("INPUT_BACKEND configured: InputSystem");
        }
    }
}
