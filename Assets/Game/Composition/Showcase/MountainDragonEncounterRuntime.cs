using System;
using Game.Composition.Campaign;
using Game.Composition.Campaign.Runtime;
using Game.Cutscenes.Api;
using Game.Cutscenes.Runtime;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using Game.WorldBuilder.Voxel;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Production composition for the mountain summit encounter. WorldBuilder owns spatial
    /// proximity, Story owns the semantic transition, and Cutscenes owns dialogue execution.
    /// </summary>
    public sealed class MountainDragonEncounterRuntime : IActiveCutsceneDialogue
    {
        public const string Greeting = "Hello, I'm Mr. Dragon.";
        private static readonly CutsceneCueId GreetingCue =
            new CutsceneCueId("showcase.mountain-dragon.greeting");

        private readonly CampaignBlueprint _blueprint;
        private readonly CampaignRuntime _campaign;
        private readonly SiteProximityWatcher _proximity;
        private readonly TimedCutsceneDialogueRuntime _dialogue;

        public MountainLandmarkSpec Landmark { get; }
        public bool HasTriggered => _proximity.FiredCount > 0;
        public string ActiveDialogue => _dialogue.ActiveDialogue;

        public MountainDragonEncounterRuntime(uint seed)
        {
            Landmark = ShowcaseMountainDragonLayout.CreateLandmark(seed);

            var game = Campaign.Create("showcase-mountain-dragon");
            RegionHandle mountainRegion = game.World.Region("showcase-mountain-region");
            SiteHandle summit = mountainRegion.Site(
                "mountain-dragon-summit",
                SiteArchetype.Ruin);

            var definition = new CutsceneDefinition(
                "mountain-dragon-greeting",
                CutsceneStageSetupDefinition.Empty,
                new[] { CutsceneStep.Dialogue(GreetingCue) });
            CutsceneHandle greeting = summit.Cutscene(definition);

            game.Story.Rule("approach-mountain-dragon", rule => rule
                .When(StoryTrigger.EnterSiteProximity(summit))
                .If(StoryCondition.CutsceneNotCompleted(greeting))
                .Then(StoryEffect.PlayCutscene(greeting)));

            _blueprint = game.Build();
            _dialogue = new TimedCutsceneDialogueRuntime(
                ResolveDialogue,
                displayDurationMilliseconds: 5000);
            var presentation = new CutscenePresentationRouter(
                ImmediateCutsceneCueRuntime.Instance,
                _dialogue,
                ImmediateCutsceneCueRuntime.Instance);
            _campaign = new CampaignRuntime(
                _blueprint,
                Array.Empty<CutsceneStageRealization>(),
                EmptyActorProvider.Instance,
                presentation);
            _proximity = new SiteProximityWatcher(new[]
            {
                new SiteProximityTriggerSpec(
                    summit,
                    Landmark.SummitApproachWorldX,
                    Landmark.SummitApproachWorldZ,
                    radius: 90,
                    oneShot: true)
            });
        }

        public int Update(int playerVoxelX, int playerVoxelZ, int elapsedMilliseconds)
        {
            if (elapsedMilliseconds < 0)
                throw new ArgumentOutOfRangeException(nameof(elapsedMilliseconds));

            _dialogue.Advance(elapsedMilliseconds);
            int matched = _proximity.Update(
                playerVoxelX,
                playerVoxelZ,
                site => _campaign.EnterSiteProximity(_blueprint, site));

            // Dialogue-only cutscenes have no timed choreography; one zero-time tick executes the
            // cue through the ordinary CutsceneRunner and records completion in CampaignRuntime.
            _campaign.Tick(0);
            return matched;
        }

        private static string ResolveDialogue(CutsceneActorId speaker, CutsceneCueId dialogueCue) =>
            dialogueCue.Equals(GreetingCue) ? Greeting : dialogueCue.Value;

        private sealed class EmptyActorProvider : IWorldBoundCutsceneActorProvider
        {
            public static readonly EmptyActorProvider Instance = new EmptyActorProvider();
            private EmptyActorProvider() { }

            public bool TryResolveNpc(NpcRef npc, out ICutsceneActorRuntime actor)
            {
                actor = null;
                return false;
            }

            public bool TryResolvePlayer(int playerSlot, out ICutsceneActorRuntime actor)
            {
                actor = null;
                return false;
            }
        }
    }
}
