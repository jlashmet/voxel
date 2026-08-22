using System.Collections.Generic;
using Game.Cutscenes.Api;

namespace Game.Cutscenes.Content.Kentridge
{
    /// <summary>
    /// Port of the recovered Kentridge opening choreography. It deliberately contains no world
    /// coordinates; WorldBuilder/world generation must satisfy the declared semantic stage regions.
    /// </summary>
    public static class KentridgeOpeningCutscene
    {
        public static readonly CutsceneActorId Lead = new CutsceneActorId("weldon");
        public static readonly CutsceneActorId Madeline = new CutsceneActorId("madeline");
        public static readonly CutsceneActorId Steven = new CutsceneActorId("steven");
        public static readonly CutsceneActorId Logan = new CutsceneActorId("logan");

        public static readonly CutsceneStagePointId LeadStart = new CutsceneStagePointId("lead-start");
        public static readonly CutsceneStagePointId MadelineStage = new CutsceneStagePointId("madeline-stage");
        public static readonly CutsceneStagePointId StevenStage = new CutsceneStagePointId("steven-stage");
        public static readonly CutsceneStagePointId LoganStart = new CutsceneStagePointId("logan-start");
        public static readonly CutsceneStagePointId LeadStage = new CutsceneStagePointId("lead-stage");
        public static readonly CutsceneStagePointId EntranceFocus = new CutsceneStagePointId("entrance-focus");
        public static readonly CutsceneStagePointId LoganStop = new CutsceneStagePointId("logan-stop");

        public static readonly CutsceneCueId EstablishingCamera = new CutsceneCueId("kentridge.pub.opening.establishing");
        public static readonly CutsceneCueId DoorOpenSound = new CutsceneCueId("door.open");

        // Retain the original public beat names as aliases for callers that referenced them before
        // the recovered script was expanded from three stand-ins into its actual 31 spoken lines.
        public static readonly CutsceneCueId BeforeLoganDialogue = KentridgeOpeningScript.CueForOriginalLine(1);
        public static readonly CutsceneCueId LoganArrivesDialogue = KentridgeOpeningScript.CueForOriginalLine(11);
        public static readonly CutsceneCueId LoganConversationDialogue = KentridgeOpeningScript.CueForOriginalLine(12);

        public static readonly CutsceneDefinition Definition = Create();

        private static CutsceneDefinition Create()
        {
            var setup = new CutsceneStageSetupDefinition(new[]
            {
                new CutsceneActorPlacement(Lead, LeadStart),
                new CutsceneActorPlacement(Madeline, MadelineStage),
                new CutsceneActorPlacement(Steven, StevenStage),
                new CutsceneActorPlacement(Logan, LoganStart)
            });

            var stage = new[]
            {
                new CutsceneStagePointRequirement(LeadStart, CutsceneStageRegion.PlayerSpawnArea, 8, CutsceneStageFacingHint.IntoSite),
                new CutsceneStagePointRequirement(MadelineStage, CutsceneStageRegion.InteriorGatheringArea, 8, CutsceneStageFacingHint.TowardStageCenter),
                new CutsceneStagePointRequirement(StevenStage, CutsceneStageRegion.InteriorGatheringArea, 8, CutsceneStageFacingHint.TowardStageCenter),
                new CutsceneStagePointRequirement(LoganStart, CutsceneStageRegion.PublicEntrance, 8, CutsceneStageFacingHint.IntoSite),
                new CutsceneStagePointRequirement(LeadStage, CutsceneStageRegion.InteriorGatheringArea, 8, CutsceneStageFacingHint.TowardStageCenter),
                new CutsceneStagePointRequirement(EntranceFocus, CutsceneStageRegion.PublicEntrance, 4, CutsceneStageFacingHint.IntoSite),
                new CutsceneStagePointRequirement(LoganStop, CutsceneStageRegion.EntranceApproach, 8, CutsceneStageFacingHint.TowardStageCenter)
            };

            var steps = new List<CutsceneStep>
            {
                // Opening.m: frame WalkTo at 0.80 zoom, hold three seconds, open the door, wait
                // another two seconds, then let Weldon walk into the already-framed pub group.
                CutsceneStep.Camera(EstablishingCamera),
                CutsceneStep.Wait(3000),
                CutsceneStep.Sound(DoorOpenSound),
                CutsceneStep.Wait(2000),
                CutsceneStep.Move(Lead, LeadStage, 2500),
                CutsceneStep.Parallel(
                    CutsceneStep.FaceActor(Madeline, Lead),
                    CutsceneStep.FaceActor(Steven, Lead)),
                CutsceneStep.Wait(500),
                CutsceneStep.FaceActor(Lead, Madeline),
                CutsceneStep.Wait(500),

                // Opening.txt lines 1-10: the original conversation before Logan enters.
                Dialogue(Madeline, 1),
                Dialogue(Lead, 2),
                Dialogue(Lead, 3),
                Dialogue(Madeline, 4),
                Dialogue(Lead, 5),
                Dialogue(Steven, 6),
                Dialogue(Steven, 7),
                Dialogue(Madeline, 8),
                Dialogue(Lead, 9),
                Dialogue(Madeline, 10),

                CutsceneStep.Wait(500),
                CutsceneStep.Parallel(
                    CutsceneStep.FacePoint(Lead, EntranceFocus),
                    CutsceneStep.FacePoint(Madeline, EntranceFocus),
                    CutsceneStep.FacePoint(Steven, EntranceFocus)),
                CutsceneStep.Wait(2500),
                CutsceneStep.Move(Logan, LoganStop, 2000),

                // Opening.m deliberately lets Logan speak once before the group turns to face him.
                Dialogue(Logan, 11),
                CutsceneStep.Parallel(
                    CutsceneStep.FaceActor(Lead, Logan),
                    CutsceneStep.FaceActor(Madeline, Logan),
                    CutsceneStep.FaceActor(Steven, Logan)),
                CutsceneStep.Wait(500),

                // Opening.txt lines 12-31: Logan recruits the group and Weldon redirects them home.
                Dialogue(Logan, 12),
                Dialogue(Madeline, 13),
                Dialogue(Logan, 14),
                Dialogue(Logan, 15),
                Dialogue(Lead, 16),
                Dialogue(Logan, 17),
                Dialogue(Logan, 18),
                Dialogue(Logan, 19),
                Dialogue(Steven, 20),
                Dialogue(Logan, 21),
                Dialogue(Logan, 22),
                Dialogue(Steven, 23),
                Dialogue(Lead, 24),
                Dialogue(Madeline, 25),
                Dialogue(Lead, 26),
                Dialogue(Lead, 27),
                Dialogue(Lead, 28),
                Dialogue(Logan, 29),
                Dialogue(Lead, 30),
                Dialogue(Logan, 31)
            };

            return new CutsceneDefinition("kentridge.pub.opening", setup, steps, stage);
        }

        private static CutsceneStep Dialogue(CutsceneActorId speaker, int oneBasedLineNumber) =>
            CutsceneStep.Dialogue(speaker, KentridgeOpeningScript.CueForOriginalLine(oneBasedLineNumber));
    }
}
