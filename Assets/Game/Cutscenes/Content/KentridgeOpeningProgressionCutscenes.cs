using System.Collections.Generic;
using Game.Cutscenes.Api;

namespace Game.Cutscenes.Content.Kentridge
{
    /// <summary>
    /// Source-backed Kentridge opening beats after the recovered pub scene. The referenced Awon
    /// dialogue payload is absent from the pinned Mounting Force revision, so that scene uses the
    /// repository-standard missing-dialogue placeholder. The Medrare chain preserves the speakers
    /// and text ordering from the pinned dialogue assets; the legacy TMX files do not author extra
    /// movement/camera/transition choreography for these RPGCutScene regions.
    /// </summary>
    public static class KentridgeOpeningProgressionCutscenes
    {
        public static readonly CutsceneActorId Weldon = new CutsceneActorId("weldon");
        public static readonly CutsceneActorId Awon = new CutsceneActorId("awon");
        public static readonly CutsceneActorId Logan = new CutsceneActorId("logan");
        public static readonly CutsceneActorId Medrare = new CutsceneActorId("medrare");

        public static readonly CutsceneDefinition AwonDefinition = new CutsceneDefinition(
            "kentridge.awon.house-back-room",
            CutsceneStageSetupDefinition.Empty,
            new[]
            {
                Narrated(KentridgeOpeningScript.CueForAwonOpeningBeat(1))
            });

        public static readonly CutsceneDefinition SeeMedrareDefinition = new CutsceneDefinition(
            "kentridge.see-medrare",
            CutsceneStageSetupDefinition.Empty,
            new[]
            {
                Spoken(Weldon, KentridgeOpeningScript.CueForSeeMedrareLine(1)),
                Spoken(Logan, KentridgeOpeningScript.CueForSeeMedrareLine(2)),
                Spoken(Logan, KentridgeOpeningScript.CueForSeeMedrareLine(3)),
                Spoken(Weldon, KentridgeOpeningScript.CueForSeeMedrareLine(4))
            });

        public static readonly CutsceneDefinition MedrareFirstSpellDefinition = new CutsceneDefinition(
            "kentridge.medrare.first-spell",
            CutsceneStageSetupDefinition.Empty,
            BuildFirstSpellSteps());

        public static readonly CutsceneDefinition MedrareToChurchDefinition = new CutsceneDefinition(
            "kentridge.medrare.to-church",
            CutsceneStageSetupDefinition.Empty,
            new[]
            {
                Spoken(Logan, KentridgeOpeningScript.CueForMedrareToChurchLine(1)),
                Spoken(Logan, KentridgeOpeningScript.CueForMedrareToChurchLine(2)),
                Spoken(Logan, KentridgeOpeningScript.CueForMedrareToChurchLine(3))
            });

        private static IReadOnlyList<CutsceneStep> BuildFirstSpellSteps()
        {
            return new[]
            {
                Spoken(Medrare, KentridgeOpeningScript.CueForMedrareFirstSpellLine(1)),
                Spoken(Weldon, KentridgeOpeningScript.CueForMedrareFirstSpellLine(2)),
                Spoken(Medrare, KentridgeOpeningScript.CueForMedrareFirstSpellLine(3)),
                Narrated(KentridgeOpeningScript.CueForMedrareFirstSpellLine(4)),
                Narrated(KentridgeOpeningScript.CueForMedrareFirstSpellLine(5)),
                Narrated(KentridgeOpeningScript.CueForMedrareFirstSpellLine(6)),
                Narrated(KentridgeOpeningScript.CueForMedrareFirstSpellLine(7)),
                Spoken(Medrare, KentridgeOpeningScript.CueForMedrareFirstSpellLine(8)),
                Spoken(Medrare, KentridgeOpeningScript.CueForMedrareFirstSpellLine(9)),
                Spoken(Medrare, KentridgeOpeningScript.CueForMedrareFirstSpellLine(10)),
                Spoken(Medrare, KentridgeOpeningScript.CueForMedrareFirstSpellLine(11)),
                Spoken(Weldon, KentridgeOpeningScript.CueForMedrareFirstSpellLine(12)),
                Spoken(Medrare, KentridgeOpeningScript.CueForMedrareFirstSpellLine(13)),
                Spoken(Weldon, KentridgeOpeningScript.CueForMedrareFirstSpellLine(14)),
                Spoken(Medrare, KentridgeOpeningScript.CueForMedrareFirstSpellLine(15)),
                Spoken(Weldon, KentridgeOpeningScript.CueForMedrareFirstSpellLine(16)),
                Spoken(Medrare, KentridgeOpeningScript.CueForMedrareFirstSpellLine(17)),
                Spoken(Weldon, KentridgeOpeningScript.CueForMedrareFirstSpellLine(18)),
                Spoken(Medrare, KentridgeOpeningScript.CueForMedrareFirstSpellLine(19)),
                Spoken(Weldon, KentridgeOpeningScript.CueForMedrareFirstSpellLine(20))
            };
        }

        private static CutsceneStep Spoken(CutsceneActorId speaker, CutsceneCueId cue) =>
            CutsceneStep.Dialogue(speaker, cue);

        private static CutsceneStep Narrated(CutsceneCueId cue) =>
            CutsceneStep.Dialogue(cue);
    }
}
