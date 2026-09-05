using System;
using System.IO;
using System.Security.Cryptography;
using Game.Characters.Api;
using Game.Encounters.Api;
using Game.WorldBuilder.Api;
using UnityEngine;

namespace Game.Composition.Kentridge.Playable.Validation
{
    /// <summary>
    /// Focused standalone-player consumer for the Kentridge WorldBuilder-to-encounter bridge.
    /// Test-only setup supplies deterministic authored WorldBuilder facts; all realization work is
    /// performed by the same KentridgeForestEncounterRealization production adapter used by the
    /// playable slice.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class KentridgeEncounterRealizationValidation : MonoBehaviour
    {
        private const string SuccessMarker = "KENTRIDGE_ENCOUNTER_REALIZATION_VALIDATION PASS";
        private const string MilestonePrefix = "VOXEL_VALIDATION_MILESTONE ";
        private string _status = "Kentridge encounter realization validation: starting";

        private void Awake()
        {
            EnsureValidationCamera();
        }

        private void Start()
        {
            try
            {
                RunValidation();
                _status = "PASS  WorldBuilder macro placement -> encounter anchor + 3 bandit formation bindings";
                Debug.Log(SuccessMarker + " anchor=(180,0,-170) participants=3");
                Debug.Log(MilestonePrefix + "{\"name\":\"encounter-realization-ready\",\"participants\":3}");
            }
            catch (Exception exception)
            {
                _status = "FAIL  " + exception.Message;
                Debug.LogException(exception);
                throw;
            }
        }

        private static void RunValidation()
        {
            var forestNode = new TopDownWorldNodeSpec("forest", "forest", TopDownWorldNodeKind.Region);
            var layout = new TopDownWorldLayout(
                "forest",
                123u,
                new[]
                {
                    new TopDownWorldNodePlacement(forestNode, new TopDownWorldGridPoint(2, -3))
                },
                Array.Empty<TopDownWorldRouteSpec>());

            KentridgeForestEncounterRealization.RememberMacroLayout(
                layout,
                "forest",
                1000,
                -500,
                400);

            var definition = new EncounterDefinition(
                new EncounterId("kentridge-validation-forest"),
                EncounterCombatPolicy.Required,
                "forest-ambush");
            var result = KentridgeForestEncounterRealization.Compose(
                definition,
                CharacterId.FromStableKey("validation", "bandit-left"),
                CharacterId.FromStableKey("validation", "bandit-centre"),
                CharacterId.FromStableKey("validation", "bandit-right"));

            if (!result.IsSuccess)
                throw new InvalidOperationException("Encounter realization failed: " + result.Diagnostic);
            if (!result.Realization.Anchor.Equals(new CharacterVector3(180f, 0f, -170f)))
                throw new InvalidOperationException("Encounter anchor did not come from the expected WorldBuilder macro placement.");
            if (result.Realization.Characters.Count != 3)
                throw new InvalidOperationException("Encounter realization did not produce the three authored bandit bindings.");

            RequirePosition(result.Realization.Characters[0].Position, new CharacterVector3(174.6f, 0f, -170.8f), "left");
            RequirePosition(result.Realization.Characters[1].Position, new CharacterVector3(180.8f, 0f, -168.8f), "centre");
            RequirePosition(result.Realization.Characters[2].Position, new CharacterVector3(185.8f, 0f, -169.9f), "right");
        }

        private static void RequirePosition(CharacterVector3 actual, CharacterVector3 expected, string role)
        {
            if (!actual.Equals(expected))
                throw new InvalidOperationException(
                    "Encounter " + role + " formation binding did not use the Kentridge realization facts. Expected " +
                    expected + ", got " + actual + ".");
        }

        private static void EnsureValidationCamera()
        {
            if (Camera.main != null)
                return;

            var cameraObject = new GameObject("Kentridge Encounter Validation Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.075f, 0.095f, 1f);
            camera.transform.position = new Vector3(0f, 2f, -6f);
        }

        private void OnGUI()
        {
            GUI.Box(new Rect(24f, 24f, Mathf.Max(320f, Screen.width - 48f), 82f), _status);
        }
    }

    /// <summary>
    /// Process-side identity proof used by multi-process built-player validation.
    /// The harness supplies the authoritative feature SHA, while the player hashes the executable
    /// it is actually running so the proof cannot be satisfied by echoing the harness hash.
    /// </summary>
    internal static class ValidationBuildIdentityReporter
    {
        private const string MilestonePrefix = "VOXEL_VALIDATION_MILESTONE ";
        private const string SourceShaArgument = "-voxel-validation-source-sha";

        [Serializable]
        private sealed class BuildIdentityMilestone
        {
            public string name;
            public string sourceSha;
            public string executableSha256;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ReportBuildIdentity()
        {
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
            string sourceSha = CommandLineValue(Environment.GetCommandLineArgs(), SourceShaArgument);
            if (string.IsNullOrEmpty(sourceSha))
                return;

            try
            {
                string executablePath = ResolveMacPlayerExecutable(Application.dataPath);
                string executableSha256 = ComputeSha256(executablePath);
                var milestone = new BuildIdentityMilestone
                {
                    name = "build-identity",
                    sourceSha = sourceSha.ToLowerInvariant(),
                    executableSha256 = executableSha256
                };
                Debug.Log(MilestonePrefix + JsonUtility.ToJson(milestone));
            }
            catch (Exception exception)
            {
                Debug.LogError("Built-player validation could not report executable identity: " + exception);
            }
#endif
        }

        internal static string CommandLineValue(string[] arguments, string flag)
        {
            if (arguments == null || string.IsNullOrEmpty(flag))
                return null;

            string assignmentPrefix = flag + "=";
            for (int index = 0; index < arguments.Length; index++)
            {
                string argument = arguments[index];
                if (string.Equals(argument, flag, StringComparison.Ordinal))
                    return index + 1 < arguments.Length ? arguments[index + 1] : null;
                if (argument != null && argument.StartsWith(assignmentPrefix, StringComparison.Ordinal))
                    return argument.Substring(assignmentPrefix.Length);
            }

            return null;
        }

        internal static string ResolveMacPlayerExecutable(string applicationDataPath)
        {
            if (string.IsNullOrEmpty(applicationDataPath))
                throw new InvalidOperationException("Application.dataPath is unavailable.");

            string contentsPath = Path.GetFullPath(Path.Combine(applicationDataPath, "..", ".."));
            string macOsPath = Path.Combine(contentsPath, "MacOS");
            if (!Directory.Exists(macOsPath))
                throw new InvalidOperationException("Built player MacOS directory does not exist: " + macOsPath);

            string[] candidates = Directory.GetFiles(macOsPath);
            if (candidates.Length != 1)
                throw new InvalidOperationException(
                    "Expected exactly one built-player executable in " + macOsPath + ", found " + candidates.Length + ".");
            return Path.GetFullPath(candidates[0]);
        }

        internal static string ComputeSha256(string path)
        {
            using (SHA256 sha256 = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                byte[] hash = sha256.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
            }
        }
    }
}
