using System;
using System.Collections.Generic;
using Game.Cutscenes.Api;

namespace Game.Cutscenes.Content.Kentridge
{
    /// <summary>
    /// Opening progression beats that occur after the recovered pub scene. These are kept separate
    /// from KentridgeOpeningCutscene so the original 31-line pub transcript remains untouched.
    /// </summary>
    public static class KentridgeOpeningProgressionCutscenes
    {
        public static readonly CutsceneActorId Logan = new CutsceneActorId("logan");
        public static readonly CutsceneActorId Awon = new CutsceneActorId("awon");
        public static readonly CutsceneActorId Medrare = new CutsceneActorId("medrare");

        public static readonly CutsceneCueId LoganCamera = new CutsceneCueId("kentridge.logan.opening.camera");
        public static readonly CutsceneCueId LoganIntroduction = new CutsceneCueId("kentridge.logan.opening.introduction");
        public static readonly CutsceneCueId LoganInstructions = new CutsceneCueId("kentridge.logan.opening.instructions");
        public static readonly CutsceneCueId AwonTournament = new CutsceneCueId("kentridge.awon.opening.tournament");
        public static readonly CutsceneCueId MedrareRumor = new CutsceneCueId("kentridge.medrare.opening.rumor");

        public static readonly CutsceneDefinition LoganDefinition = new CutsceneDefinition(
            "kentridge.logan.opening",
            EmptySetup(),
            new List<CutsceneStep>
            {
                CutsceneStep.Camera(LoganCamera),
                CutsceneStep.Dialogue(Logan, LoganIntroduction),
                CutsceneStep.Dialogue(Logan, LoganInstructions)
            },
            Array.Empty<CutsceneStagePointRequirement>());

        public static readonly CutsceneDefinition AwonDefinition = new CutsceneDefinition(
            "kentridge.awon.opening",
            EmptySetup(),
            new[] { CutsceneStep.Dialogue(Awon, AwonTournament) },
            Array.Empty<CutsceneStagePointRequirement>());

        public static readonly CutsceneDefinition MedrareDefinition = new CutsceneDefinition(
            "kentridge.medrare.opening",
            EmptySetup(),
            new[] { CutsceneStep.Dialogue(Medrare, MedrareRumor) },
            Array.Empty<CutsceneStagePointRequirement>());

        private static CutsceneStageSetupDefinition EmptySetup() =>
            new CutsceneStageSetupDefinition(Array.Empty<CutsceneActorPlacement>());
    }
}
