using System.Collections.Generic;
using Game.Cutscenes.Api;

namespace Game.Cutscenes.Content.Kentridge
{
    /// <summary>
    /// Opening beats after the recovered pub scene. Logan and Medrare preserve retained source
    /// dialogue. Awon's referenced text payload is absent from the retained snapshot, so his scene
    /// exposes only the issue-contract training beats rather than inventing dialogue.
    /// </summary>
    public static class KentridgeOpeningProgressionCutscenes
    {
        public static readonly CutsceneActorId Weldon = new CutsceneActorId("weldon");
        public static readonly CutsceneActorId Logan = new CutsceneActorId("logan");
        public static readonly CutsceneActorId Medrare = new CutsceneActorId("medrare");

        public static readonly CutsceneStagePointId MedrareStart = new CutsceneStagePointId("medrare-start");
        public static readonly CutsceneStagePointId MedrareConversation = new CutsceneStagePointId("medrare-conversation");

        public static readonly CutsceneDefinition LoganToChurchDefinition = new CutsceneDefinition(
            "kentridge.logan.to-church",
            CutsceneStageSetupDefinition.Empty,
            new[]
            {
                CutsceneStep.Dialogue(Logan, KentridgeOpeningScript.CueForLoganToChurchLine(1)),
                CutsceneStep.Dialogue(Logan, KentridgeOpeningScript.CueForLoganToChurchLine(2)),
                CutsceneStep.Dialogue(Logan, KentridgeOpeningScript.CueForLoganToChurchLine(3))
            });

        public static readonly CutsceneDefinition AwonDefinition = new CutsceneDefinition(
            "kentridge.awon.house-back-room",
            CutsceneStageSetupDefinition.Empty,
            new[]
            {
                CutsceneStep.Dialogue(KentridgeOpeningScript.CueForAwonOpeningBeat(1)),
                CutsceneStep.Dialogue(KentridgeOpeningScript.CueForAwonOpeningBeat(2)),
                CutsceneStep.Dialogue(KentridgeOpeningScript.CueForAwonOpeningBeat(3)),
                CutsceneStep.Dialogue(KentridgeOpeningScript.CueForAwonOpeningBeat(4)),
                CutsceneStep.Dialogue(KentridgeOpeningScript.CueForAwonOpeningBeat(5))
            });

        public static readonly CutsceneDefinition MedrareFirstSpellDefinition = new CutsceneDefinition(
            "kentridge.medrare.first-spell",
            new CutsceneStageSetupDefinition(new[]
            {
                new CutsceneActorPlacement(Medrare, MedrareStart)
            }),
            new List<CutsceneStep>
            {
                // KentridgeMedrareJoin.m: 1.5-second beat, then Medrare walks to the player before
                // the dialogue block starts. Semantic stage points keep those cues generator-owned.
                CutsceneStep.Wait(1500),
                CutsceneStep.Move(Medrare, MedrareConversation, 2000),
                Spoken(Weldon, 1), Spoken(Medrare, 2), Spoken(Weldon, 3), Spoken(Medrare, 4),
                Spoken(Weldon, 5), Spoken(Medrare, 6), Spoken(Weldon, 7), Spoken(Medrare, 8),
                Spoken(Weldon, 9), Spoken(Medrare, 10), Spoken(Weldon, 11), Spoken(Logan, 12),
                Spoken(Weldon, 13), Spoken(Medrare, 14), Spoken(Medrare, 15), Spoken(Weldon, 16),
                Spoken(Logan, 17), Spoken(Weldon, 18), Spoken(Logan, 19), Spoken(Weldon, 20),
                Narrated(21), Narrated(22), Narrated(23)
            },
            new[]
            {
                new CutsceneStagePointRequirement(
                    MedrareStart,
                    CutsceneStageRegion.ConversationApproach,
                    4,
                    CutsceneStageFacingHint.TowardStageCenter),
                new CutsceneStagePointRequirement(
                    MedrareConversation,
                    CutsceneStageRegion.InteriorGatheringArea,
                    4,
                    CutsceneStageFacingHint.TowardStageCenter)
            });

        private static CutsceneStep Spoken(CutsceneActorId speaker, int line) =>
            CutsceneStep.Dialogue(speaker, KentridgeOpeningScript.CueForMedrareFirstSpellLine(line));

        private static CutsceneStep Narrated(int line) =>
            CutsceneStep.Dialogue(KentridgeOpeningScript.CueForMedrareFirstSpellLine(line));
    }
}
