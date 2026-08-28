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
    public sealed class MountainDragonEncounterRuntime
    {
        public const string Greeting = "Hello, I'm Mr. Dragon.";
        private static readonly CutsceneCueId GreetingCue =
            new CutsceneCueId("showcase.mountain-dragon.greeting");

        private readonly CampaignBlueprint _blueprint;
        private readonly CampaignRuntime _campaign;
        private readonly SiteProximityWatcher _proximity;
        private readonly DialoguePresentation _presentation;

        public MountainLandmarkSpec Landmark { get; }
        public bool HasTriggered => _proximity.FiredCount > 0;
        public string ActiveDialogue => _presentation.ActiveDialogue;

        public MountainDragonEncounterRuntime(uint seed)
        {
            Landmark = ShowcaseMountainDragonLayout.CreateLandmark(seed);

            var game = Campaign.Create("showcase-mountain-dragon");
            SiteRef summit = game.World.RequireSite(
                "mountain-dragon-summit",
                site => site.Archetype(SiteArchetype.Ruin));

            var definition = new CutsceneDefinition(
                "mountain-dragon-greeting",
                CutsceneStageSetupDefinition.Empty,
                new[] { CutsceneStep.Dialogue(GreetingCue) });
            CutsceneRef greeting = game.Story.Cutscene(
                definition,
                scene => scene.At(summit));

            game.Story.Rule("approach-mountain-dragon", rule => rule
                .When(StoryTrigger.EnterSiteProximity(summit))
                .If(StoryCondition.CutsceneNotCompleted(greeting))
                .Then(StoryEffect.PlayCutscene(greeting)));

            _blueprint = game.Build();
            _presentation = new DialoguePresentation();
            _campaign = new CampaignRuntime(
                _blueprint,
                Array.Empty<CutsceneStageRealization>(),
                EmptyActorProvider.Instance,
                _presentation);
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

            _presentation.Advance(elapsedMilliseconds);
            int matched = _proximity.Update(
                playerVoxelX,
                playerVoxelZ,
                site => _campaign.EnterSiteProximity(_blueprint, site));

            // Dialogue-only cutscenes have no timed choreography; one zero-time tick executes the
            // cue through the ordinary CutsceneRunner and records completion in CampaignRuntime.
            _campaign.Tick(0);
            return matched;
        }

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

        private sealed class DialoguePresentation : ICutscenePresentation
        {
            private int _remainingMilliseconds;

            public string ActiveDialogue { get; private set; }

            public void Advance(int elapsedMilliseconds)
            {
                if (_remainingMilliseconds <= 0) return;
                _remainingMilliseconds -= elapsedMilliseconds;
                if (_remainingMilliseconds <= 0) ActiveDialogue = null;
            }

            public ICutsceneOperation SetCamera(CutsceneCueId cameraCue) =>
                CompletedCutsceneOperation.Instance;

            public ICutsceneOperation ShowDialogue(
                CutsceneActorId speaker,
                CutsceneCueId dialogueCue)
            {
                ActiveDialogue = dialogueCue.Equals(GreetingCue)
                    ? Greeting
                    : dialogueCue.Value;
                _remainingMilliseconds = 5000;
                return CompletedCutsceneOperation.Instance;
            }

            public ICutsceneOperation PlaySound(CutsceneCueId soundCue) =>
                CompletedCutsceneOperation.Instance;
        }
    }
}
