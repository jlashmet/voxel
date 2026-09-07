using System;
using Game.Characters.Api;
using Game.Combat.Api;
using Game.Combat.Runtime;
using Game.Composition.Kentridge.Playable;
using Game.Encounters.Api;
using Game.Input.Api;
using Game.Vitality.Api;
using Game.Vitality.Runtime;
using Game.WorldBuilder.Api;
using UnityEngine;

namespace Game.Composition.Kentridge.Playable.Validation
{
    /// <summary>
    /// Focused standalone-player consumer for the Kentridge WorldBuilder-to-encounter bridge and its
    /// authored forest combat tuning. Test-only input supplies Primary intent; realization, Combat,
    /// Vitality, player command handling, and enemy AI use the same production paths as the playable slice.
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
                _status = "PASS  WorldBuilder encounter realization + player-input forest combat balance";
                Debug.Log(
                    SuccessMarker +
                    " anchor=(180,0,-170) participants=3 combatWinner=Player playerVitality=" +
                    KentridgeForestCombatTuning.PlayerInitialVitality);
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
            ValidateForestCombatBalance();
        }

        private static void ValidateForestCombatBalance()
        {
            CharacterId playerCharacter = CharacterId.FromStableKey("validation", "forest-player");
            var enemyCharacters = new[]
            {
                CharacterId.FromStableKey("validation", "forest-bandit-left"),
                CharacterId.FromStableKey("validation", "forest-bandit-centre"),
                CharacterId.FromStableKey("validation", "forest-bandit-right")
            };

            var vitality = new VitalityRegistry();
            RequireVitality(
                vitality,
                playerCharacter,
                KentridgeForestCombatTuning.InitialVitality(CombatTeam.Player));
            for (int i = 0; i < enemyCharacters.Length; i++)
                RequireVitality(
                    vitality,
                    enemyCharacters[i],
                    KentridgeForestCombatTuning.InitialVitality(CombatTeam.Enemy));

            var participants = new CombatParticipant[enemyCharacters.Length + 1];
            for (int i = 0; i < enemyCharacters.Length; i++)
                participants[i] = CombatParticipant.FromCharacter(enemyCharacters[i], CombatTeam.Enemy);
            CombatParticipant player = CombatParticipant.FromCharacter(playerCharacter, CombatTeam.Player);
            participants[participants.Length - 1] = player;

            var combat = new CombatService(vitality);
            combat.BeginCombat(new CombatEncounterRequest("kentridge-validation-forest", participants));
            var input = new PrimaryInputReader();
            var playerInput = new CombatInputController(combat, input, new LocalPlayerId(0), player.Id);
            var enemyAi = new CombatAiBattleDriver(combat, KentridgeForestCombatTuning.BattleSeed);

            int playerActions = 0;
            int watchdog = 0;
            while (combat.IsActive && watchdog++ < 64)
            {
                CombatParticipant active = FindParticipant(combat, combat.ActiveParticipant);
                if (active.Team == CombatTeam.Player)
                {
                    CombatCommandResult action = playerInput.Tick(1f);
                    if (!action.Succeeded)
                        throw new InvalidOperationException(
                            "Kentridge player Primary action was rejected: " + action.RejectReason);
                    playerActions++;
                }
                else
                {
                    enemyAi.Step();
                }
            }

            if (combat.IsActive)
                throw new InvalidOperationException("Kentridge forest combat exceeded the validation action watchdog.");
            if (!combat.WinningTeam.HasValue || combat.WinningTeam.Value != CombatTeam.Player)
                throw new InvalidOperationException(
                    "Kentridge forest combat tuning did not resolve to the player team; winner=" +
                    (combat.WinningTeam.HasValue ? combat.WinningTeam.Value.ToString() : "none") + ".");
            if (playerActions <= 0)
                throw new InvalidOperationException("Kentridge forest combat completed without a player Primary action.");
            if (!combat.TryGetHitPoints(player.Id, out int remainingVitality) || remainingVitality <= 0)
                throw new InvalidOperationException("Kentridge forest combat winner has no remaining authoritative vitality.");
        }

        private static CombatParticipant FindParticipant(CombatService combat, CombatParticipantId id)
        {
            for (int i = 0; i < combat.ActiveParticipants.Count; i++)
            {
                CombatParticipant participant = combat.ActiveParticipants[i];
                if (participant.Id.Equals(id)) return participant;
            }

            throw new InvalidOperationException(
                "Active participant '" + id + "' is absent from the Kentridge validation combat.");
        }

        private static void RequireVitality(VitalityRegistry vitality, CharacterId character, int maximum)
        {
            if (!vitality.Register(VitalitySnapshot.Alive(character, maximum)))
                throw new InvalidOperationException(
                    "Could not register Kentridge validation vitality for '" + character + "'.");
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

        private sealed class PrimaryInputReader : IPlayerInputReader
        {
            public PlayerInputSnapshot Read(LocalPlayerId player) =>
                new PlayerInputSnapshot(0f, 0f, 0f, 0f, true, false, false, false);
        }
    }
}
