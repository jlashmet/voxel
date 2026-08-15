using System.Linq;
using Game.Cutscenes.Api;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class DerivedSiteCapabilityTests
    {
        [Test]
        public void BindingStagedCutsceneAutomaticallyConstrainsHostSite()
        {
            var game = Campaign.Create("derived-cutscene-stage");
            SiteRef pub = game.World.RequireSite("pub", site => site
                .Archetype(SiteArchetype.Pub));

            var actor = new CutsceneActorId("lead");
            var mark = new CutsceneStagePointId("lead-mark");
            var definition = new CutsceneDefinition(
                "opening",
                new CutsceneStageSetupDefinition(new[]
                {
                    new CutsceneActorPlacement(actor, mark)
                }),
                new[]
                {
                    CutsceneStep.Dialogue(actor, new CutsceneCueId("lead.line"))
                },
                new[]
                {
                    new CutsceneStagePointRequirement(
                        mark,
                        CutsceneStageRegion.InteriorGatheringArea,
                        minimumClearanceDecimetres: 10)
                });

            game.Story.Cutscene(definition, cutscene => cutscene
                .At(pub)
                .Bind(actor, CutsceneActorTarget.Player(0)));

            CampaignBlueprint blueprint = game.Build();
            SiteSpec resolvedPub = blueprint.Sites.Single(site => site.Ref.Equals(pub));

            Assert.That(
                resolvedPub.Capabilities.Any(capability => capability.Kind == SiteCapabilityKind.CutsceneStage),
                Is.True,
                "The bound choreography should impose its stage capability without duplicated authoring on the site.");
            Assert.That(BlueprintValidator.Validate(blueprint).IsValid, Is.True);

            PlanningGraph graph = BlueprintCompiler.Compile(blueprint);
            Assert.That(graph.CutsceneStages.Single().Site, Is.EqualTo(pub));
            Assert.That(graph.CutsceneStages.Single().Requirements.Single().Point, Is.EqualTo(mark));
        }
    }
}
