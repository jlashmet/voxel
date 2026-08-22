using Game.Cutscenes.Api;

namespace Game.Cutscenes.Content.Kentridge
{
    /// <summary>
    /// Port of the recovered Kentridge opening choreography. It deliberately contains no world
    /// coordinates; WorldBuilder/world generation must satisfy the declared semantic stage regions.
    /// </summary>
    public static class KentridgeOpeningCutscene
    {
        public static readonly CutsceneActorId Lead = new CutsceneActorId("lead");
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
        public static readonly CutsceneCueId BeforeLoganDialogue = new CutsceneCueId("kentridge.pub.opening.before-logan");
        public static readonly CutsceneCueId LoganArrivesDialogue = new CutsceneCueId("kentridge.pub.opening.logan-arrives");
        public static readonly CutsceneCueId LoganConversationDialogue = new CutsceneCueId("kentridge.pub.opening.logan-conversation");

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

            return new CutsceneDefinition("kentridge.pub.opening", setup, new[]
            {
                // Original Opening.m: hold the camera on WalkTo for one second, open the door,
                // then let Weldon walk into the already-framed conversation area.
                CutsceneStep.Camera(EstablishingCamera),
                CutsceneStep.Wait(1000),
                CutsceneStep.Sound(DoorOpenSound),
                CutsceneStep.Move(Lead, LeadStage, 2500),
                CutsceneStep.Parallel(
                    CutsceneStep.FaceActor(Madeline, Lead),
                    CutsceneStep.FaceActor(Steven, Lead)),
                CutsceneStep.Wait(500),
                CutsceneStep.FaceActor(Lead, Madeline),
                CutsceneStep.Wait(500),
                CutsceneStep.Dialogue(BeforeLoganDialogue),

                // Before Logan enters, the original cast turns toward the entrance and waits.
                CutsceneStep.Parallel(
                    CutsceneStep.FacePoint(Lead, EntranceFocus),
                    CutsceneStep.FacePoint(Madeline, EntranceFocus),
                    CutsceneStep.FacePoint(Steven, EntranceFocus)),
                CutsceneStep.Wait(3000),

                // Logan walks into the group; only after he arrives does everybody turn to him.
                CutsceneStep.Move(Logan, LoganStop, 2000),
                CutsceneStep.Parallel(
                    CutsceneStep.FaceActor(Lead, Logan),
                    CutsceneStep.FaceActor(Madeline, Logan),
                    CutsceneStep.FaceActor(Steven, Logan),
                    CutsceneStep.FaceActor(Logan, Steven)),
                CutsceneStep.Dialogue(LoganArrivesDialogue),
                CutsceneStep.Wait(500),
                CutsceneStep.Dialogue(LoganConversationDialogue)
            }, stage);
        }
    }
}
