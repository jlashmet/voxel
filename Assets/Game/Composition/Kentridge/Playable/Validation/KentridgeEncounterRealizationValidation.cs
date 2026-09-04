using System;
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
}
