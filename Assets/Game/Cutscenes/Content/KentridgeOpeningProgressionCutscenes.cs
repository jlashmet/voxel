using System;
using System.Collections.Generic;
using Game.Cutscenes.Api;

namespace Game.Cutscenes.Content.Kentridge
{
    /// <summary>
    /// Source-faithful opening beats after the recovered pub scene. The retained Mounting Force
    /// snapshot is missing Awon's referenced text payload, so that one-shot progression marker has
    /// no invented dialogue. Medrare dialogue and narration are preserved verbatim from the source.
    /// </summary>
    public static class KentridgeOpeningProgressionCutscenes
    {
        public static readonly CutsceneActorId Weldon = new CutsceneActorId("weldon");
        public static readonly CutsceneActorId Logan = new CutsceneActorId("logan");
        public static readonly CutsceneActorId Medrare = new CutsceneActorId("medrare");

        public static readonly CutsceneDefinition AwonDefinition = new CutsceneDefinition(
            "kentridge.awon.house-back-room",
            CutsceneStageSetupDefinition.Empty,
            Array.Empty<CutsceneStep>(),
            Array.Empty<CutsceneStagePointRequirement>());

        public static readonly CutsceneDefinition MedrareFirstSpellDefinition = new CutsceneDefinition(
            "kentridge.medrare.first-spell",
            CutsceneStageSetupDefinition.Empty,
            new List<CutsceneStep>
            {
                Spoken(Weldon, 1), Spoken(Medrare, 2), Spoken(Weldon, 3), Spoken(Medrare, 4),
                Spoken(Weldon, 5), Spoken(Medrare, 6), Spoken(Weldon, 7), Spoken(Medrare, 8),
                Spoken(Weldon, 9), Spoken(Medrare, 10), Spoken(Weldon, 11), Spoken(Logan, 12),
                Spoken(Weldon, 13), Spoken(Medrare, 14), Spoken(Medrare, 15), Spoken(Weldon, 16),
                Spoken(Logan, 17), Spoken(Weldon, 18), Spoken(Logan, 19), Spoken(Weldon, 20),
                Narrated(21), Narrated(22), Narrated(23)
            },
            Array.Empty<CutsceneStagePointRequirement>());

        public static readonly CutsceneDefinition MedrareToChurchDefinition = new CutsceneDefinition(
            "kentridge.medrare.to-church",
            CutsceneStageSetupDefinition.Empty,
            new[]
            {
                CutsceneStep.Dialogue(Logan, KentridgeOpeningScript.CueForMedrareToChurchLine(1)),
                CutsceneStep.Dialogue(Logan, KentridgeOpeningScript.CueForMedrareToChurchLine(2)),
                CutsceneStep.Dialogue(Logan, KentridgeOpeningScript.CueForMedrareToChurchLine(3))
            },
            Array.Empty<CutsceneStagePointRequirement>());

        private static CutsceneStep Spoken(CutsceneActorId speaker, int line) =>
            CutsceneStep.Dialogue(speaker, KentridgeOpeningScript.CueForMedrareFirstSpellLine(line));

        private static CutsceneStep Narrated(int line) =>
            CutsceneStep.Dialogue(KentridgeOpeningScript.CueForMedrareFirstSpellLine(line));
    }
}
