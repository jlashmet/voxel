using System.Collections.Generic;
using Game.Cutscenes.Api;

namespace Game.Cutscenes.Content.Kentridge
{
    /// <summary>
    /// Source-backed Kentridge opening beats after the recovered pub scene. The original Awon text
    /// payload is absent, so that scene intentionally uses the repository-standard missing-dialogue
    /// placeholder. Medrare text and choreography are ported from the pinned Mounting Force source.
    /// </summary>
    public static class KentridgeOpeningProgressionCutscenes
    {
        public static readonly CutsceneActorId Weldon = new CutsceneActorId("weldon");
        public static readonly CutsceneActorId Awon = new CutsceneActorId("awon");
        public static readonly CutsceneActorId Logan = new CutsceneActorId("logan");
        public static readonly CutsceneActorId Medrare = new CutsceneActorId("medrare");

        public static readonly CutsceneStagePointId MedrareStart = new CutsceneStagePointId("medrare-start");
        public static readonly CutsceneStagePointId MedrareConversation = new CutsceneStagePointId("medrare-conversation");

        public static readonly CutsceneCueId MedrareFirstSpellCamera =
            new CutsceneCueId("kentridge.medrare.first-spell.camera.zoom-0.5");
        public static readonly CutsceneCueId MedrareAttackCue =
            new CutsceneCueId("kentridge.medrare.first-spell.actor.stab");
        public static readonly CutsceneCueId MedrareHitCue =
            new CutsceneCueId("kentridge.medrare.first-spell.sound.hit");
        public static readonly CutsceneCueId MedrareBlackLayerCue =
            new CutsceneCueId("kentridge.medrare.first-spell.transition.black-layer");
        public static readonly CutsceneCueId MedrareFadeInCue =
            new CutsceneCueId("kentridge.medrare.first-spell.transition.fade-in-2000ms");
        public static readonly CutsceneCueId MedrareFadeOutCue =
            new CutsceneCueId("kentridge.medrare.first-spell.transition.fade-out-2000ms");

        public static readonly CutsceneDefinition AwonDefinition = new CutsceneDefinition(
            "kentridge.awon.house-back-room",
            CutsceneStageSetupDefinition.Empty,
            new[]
            {
                CutsceneStep.Dialogue(Awon, KentridgeOpeningScript.CueForAwonOpeningBeat(1))
            });

        public static readonly CutsceneDefinition SeeMedrareDefinition = new CutsceneDefinition(
            "kentridge.see-medrare",
            CutsceneStageSetupDefinition.Empty,
            new[]
            {
                Spoken(Medrare, KentridgeOpeningScript.CueForSeeMedrareLine(1)),
                Spoken(Medrare, KentridgeOpeningScript.CueForSeeMedrareLine(2))
            });

        public static readonly CutsceneDefinition MedrareFirstSpellDefinition = new CutsceneDefinition(
            "kentridge.medrare.first-spell",
            new CutsceneStageSetupDefinition(new[]
            {
                new CutsceneActorPlacement(Medrare, MedrareStart)
            }),
            BuildFirstSpellSteps(),
            new[]
            {
                new CutsceneStagePointRequirement(
                    MedrareStart,
                    CutsceneStageRegion.InteriorGatheringArea,
                    4,
                    CutsceneStageFacingHint.TowardStageCenter),
                new CutsceneStagePointRequirement(
                    MedrareConversation,
                    CutsceneStageRegion.ConversationApproach,
                    4,
                    CutsceneStageFacingHint.TowardStageCenter)
            });

        public static readonly CutsceneDefinition MedrareToChurchDefinition = new CutsceneDefinition(
            "kentridge.medrare.to-church",
            CutsceneStageSetupDefinition.Empty,
            new[]
            {
                Spoken(Logan, KentridgeOpeningScript.CueForMedrareToChurchLine(1))
            });

        private static IReadOnlyList<CutsceneStep> BuildFirstSpellSteps()
        {
            var steps = new List<CutsceneStep>
            {
                CutsceneStep.AcquireControlLock(),
                CutsceneStep.Camera(MedrareFirstSpellCamera),
                CutsceneStep.Wait(1500)
            };

            for (var line = 1; line <= 18; line++)
                steps.Add(Spoken(Medrare, KentridgeOpeningScript.CueForMedrareFirstSpellLine(line)));

            steps.Add(CutsceneStep.Move(Medrare, MedrareConversation, 1000));
            steps.Add(CutsceneStep.ActorCue(Medrare, MedrareAttackCue));
            steps.Add(CutsceneStep.Sound(MedrareHitCue));
            steps.Add(CutsceneStep.Wait(1500));
            steps.Add(Narrated(KentridgeOpeningScript.CueForMedrareFirstSpellLine(19)));
            steps.Add(Narrated(KentridgeOpeningScript.CueForMedrareFirstSpellLine(20)));
            steps.Add(CutsceneStep.Transition(MedrareBlackLayerCue));
            steps.Add(CutsceneStep.Wait(2000));
            steps.Add(CutsceneStep.Transition(MedrareFadeInCue));
            steps.Add(CutsceneStep.Wait(2000));
            steps.Add(Spoken(Medrare, KentridgeOpeningScript.CueForMedrareFirstSpellLine(21)));
            steps.Add(Spoken(Medrare, KentridgeOpeningScript.CueForMedrareFirstSpellLine(22)));
            steps.Add(Narrated(KentridgeOpeningScript.CueForMedrareFirstSpellLine(23)));
            steps.Add(CutsceneStep.Transition(MedrareFadeOutCue));
            steps.Add(CutsceneStep.ReleaseControlLock());
            return steps;
        }

        private static CutsceneStep Spoken(CutsceneActorId speaker, CutsceneCueId cue) =>
            CutsceneStep.Dialogue(speaker, cue);

        private static CutsceneStep Narrated(CutsceneCueId cue) =>
            CutsceneStep.Dialogue(cue);
    }
}
