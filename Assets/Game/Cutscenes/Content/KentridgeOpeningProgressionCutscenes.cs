using System.Collections.Generic;
using Game.Cutscenes.Api;

namespace Game.Cutscenes.Content.Kentridge
{
    /// <summary>
    /// Source-backed Kentridge opening beats after the recovered pub scene. Missing legacy dialogue
    /// payloads remain identity-only cues; no replacement prose is authored here.
    /// </summary>
    public static class KentridgeOpeningProgressionCutscenes
    {
        public static readonly CutsceneActorId Weldon = new CutsceneActorId("weldon");
        public static readonly CutsceneActorId Awon = new CutsceneActorId("awon");
        public static readonly CutsceneActorId Steven = new CutsceneActorId("steven");
        public static readonly CutsceneActorId Madeline = new CutsceneActorId("madeline");
        public static readonly CutsceneActorId Logan = new CutsceneActorId("logan");
        public static readonly CutsceneActorId Medrare = new CutsceneActorId("medrare");

        public static readonly CutsceneStagePointId MedrareApproachPoint =
            new CutsceneStagePointId("medrare-player-approach");
        public static readonly CutsceneCueId MedrareJoinZoomHalf =
            new CutsceneCueId("kentridge.medrare.join.zoom-0.5");

        public static readonly CutsceneDefinition AwonDefinition = new CutsceneDefinition(
            "kentridge.awon.house-back-room",
            CutsceneStageSetupDefinition.Empty,
            BuildAwonSteps());

        public static readonly CutsceneDefinition SeeMedrareDefinition = new CutsceneDefinition(
            "kentridge.see-medrare",
            CutsceneStageSetupDefinition.Empty,
            new[]
            {
                CutsceneStep.Dialogue(KentridgeOpeningScript.SeeMedrareSourceDialogue)
            });

        public static readonly CutsceneDefinition MedrareJoinDefinition = new CutsceneDefinition(
            "kentridge.medrare.join-opening",
            CutsceneStageSetupDefinition.Empty,
            new[]
            {
                CutsceneStep.Camera(MedrareJoinZoomHalf),
                CutsceneStep.Wait(1500),
                CutsceneStep.Move(Medrare, MedrareApproachPoint, 2000),
                CutsceneStep.Dialogue(KentridgeOpeningScript.MedrareJoinSourceDialogue5000)
            },
            new[]
            {
                new CutsceneStagePointRequirement(
                    MedrareApproachPoint,
                    CutsceneStageRegion.ConversationApproach,
                    8,
                    CutsceneStageFacingHint.TowardStageCenter)
            });

        public static readonly CutsceneDefinition MedrareFirstSpellDefinition = new CutsceneDefinition(
            "kentridge.medrare.first-spell",
            CutsceneStageSetupDefinition.Empty,
            new[]
            {
                CutsceneStep.Dialogue(KentridgeOpeningScript.MedrareFirstSpellSourceDialogue)
            });

        public static readonly CutsceneDefinition MedrareToChurchDefinition = new CutsceneDefinition(
            "kentridge.medrare.to-church",
            CutsceneStageSetupDefinition.Empty,
            new[]
            {
                CutsceneStep.Dialogue(KentridgeOpeningScript.MedrareToChurchSourceDialogue)
            });

        private static IReadOnlyList<CutsceneStep> BuildAwonSteps()
        {
            return new[]
            {
                Spoken(Awon, 1),
                Spoken(Weldon, 2),
                Spoken(Awon, 3),
                Spoken(Steven, 4),
                Spoken(Madeline, 5),
                Spoken(Awon, 6),
                Spoken(Logan, 7),
                Spoken(Madeline, 8),
                Spoken(Awon, 9),
                Spoken(Logan, 10),
                Spoken(Awon, 11),
                Spoken(Awon, 12),
                Spoken(Weldon, 13),
                Spoken(Awon, 14),
                Spoken(Weldon, 15),
                Spoken(Awon, 16),
                Spoken(Awon, 17),
                Spoken(Awon, 18),
                Spoken(Awon, 19),
                Spoken(Weldon, 20),
                Spoken(Awon, 21),
                Spoken(Weldon, 22)
            };
        }

        private static CutsceneStep Spoken(CutsceneActorId speaker, int lineNumber) =>
            CutsceneStep.Dialogue(speaker, KentridgeOpeningScript.CueForAwonOpeningLine(lineNumber));
    }
}
