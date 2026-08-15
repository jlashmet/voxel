using System;
using System.Linq;
using Game.Cutscenes.Api;
using Game.Cutscenes.Content.Kentridge;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CutsceneStageResolverTests
    {
        [Test]
        public void KentridgeOpeningResolvesFromSemanticSiteGeometryWithoutAuthoredOffsets()
        {
            CutsceneStagePlan plan = BuildOpeningPlan();
            var geometry = new CutsceneSiteGeometry(
                new CutsceneInt3(100, 20, 200),
                new CutsceneInt3(0, 0, 1),
                new CutsceneInt3(1, 0, 0),
                interiorHalfWidthDecimetres: 50,
                interiorDepthDecimetres: 80);

            CutsceneStageBinding first = ProceduralCutsceneStageResolver.Resolve(plan, geometry);
            CutsceneStageBinding second = ProceduralCutsceneStageResolver.Resolve(plan, geometry);

            Assert.That(
                first.Resolve(KentridgeOpeningCutscene.EntranceFocus).Position,
                Is.EqualTo(first.Resolve(KentridgeOpeningCutscene.LoganStart).Position),
                "A focus-only entrance point must not consume a separate actor staging slot.");

            CutsceneStagePoint madeline = first.Resolve(KentridgeOpeningCutscene.MadelineStage);
            CutsceneStagePoint steven = first.Resolve(KentridgeOpeningCutscene.StevenStage);
            CutsceneStagePoint lead = first.Resolve(KentridgeOpeningCutscene.LeadStage);

            Assert.That(madeline.Position, Is.Not.EqualTo(steven.Position));
            Assert.That(steven.Position, Is.Not.EqualTo(lead.Position));
            Assert.That(madeline.Position, Is.Not.EqualTo(lead.Position));

            Assert.That(madeline.Position, Is.EqualTo(new CutsceneInt3(84, 20, 253)));
            Assert.That(steven.Position, Is.EqualTo(new CutsceneInt3(100, 20, 253)));
            Assert.That(lead.Position, Is.EqualTo(new CutsceneInt3(116, 20, 253)));

            Assert.That(madeline.Forward, Is.EqualTo(new CutsceneInt3(1, 0, 0)));
            Assert.That(steven.Forward, Is.EqualTo(new CutsceneInt3(0, 0, 1)));
            Assert.That(lead.Forward, Is.EqualTo(new CutsceneInt3(-1, 0, 0)));

            foreach (CutsceneStagePointId point in plan.Definition.RequiredStagePoints)
            {
                Assert.That(first.TryResolve(point, out CutsceneStagePoint firstPoint), Is.True);
                Assert.That(second.TryResolve(point, out CutsceneStagePoint secondPoint), Is.True);
                Assert.That(firstPoint.Position, Is.EqualTo(secondPoint.Position));
                Assert.That(firstPoint.Forward, Is.EqualTo(secondPoint.Forward));
                Assert.That(firstPoint.Position.Y, Is.EqualTo(20));
            }
        }

        [Test]
        public void ResolverRejectsSiteThatCannotFitRequiredOccupiedStagePoints()
        {
            CutsceneStagePlan plan = BuildOpeningPlan();
            var tooNarrow = new CutsceneSiteGeometry(
                new CutsceneInt3(0, 0, 0),
                new CutsceneInt3(0, 0, 1),
                new CutsceneInt3(1, 0, 0),
                interiorHalfWidthDecimetres: 12,
                interiorDepthDecimetres: 80);

            Assert.Throws<InvalidOperationException>(
                () => ProceduralCutsceneStageResolver.Resolve(plan, tooNarrow));
        }

        [Test]
        public void GeometryRejectsNonCardinalOrNonOrthogonalFrames()
        {
            Assert.Throws<ArgumentException>(() => new CutsceneSiteGeometry(
                new CutsceneInt3(0, 0, 0),
                new CutsceneInt3(1, 0, 1),
                new CutsceneInt3(1, 0, 0),
                50,
                80));

            Assert.Throws<ArgumentException>(() => new CutsceneSiteGeometry(
                new CutsceneInt3(0, 0, 0),
                new CutsceneInt3(0, 0, 1),
                new CutsceneInt3(0, 0, -1),
                50,
                80));
        }

        private static CutsceneStagePlan BuildOpeningPlan()
        {
            var game = Campaign.Create("cutscene-stage-resolution-test");
            SiteRef pub = game.World.RequireSite("starting-pub", site => site
                .Archetype(SiteArchetype.Pub)
                .RequireCapability(SiteCapability.CutsceneStage));

            game.Story.Cutscene(KentridgeOpeningCutscene.Definition, scene => scene
                .At(pub)
                .Bind(KentridgeOpeningCutscene.Lead, CutsceneActorTarget.Player(0))
                .Bind(KentridgeOpeningCutscene.Madeline, CutsceneActorTarget.Player(1))
                .Bind(KentridgeOpeningCutscene.Steven, CutsceneActorTarget.Player(2))
                .Bind(KentridgeOpeningCutscene.Logan, CutsceneActorTarget.Player(3))
                .Trigger(StoryTrigger.NewGame()));

            PlanningGraph graph = BlueprintCompiler.Compile(game.Build());
            return graph.CutsceneStages.Single();
        }
    }
}
