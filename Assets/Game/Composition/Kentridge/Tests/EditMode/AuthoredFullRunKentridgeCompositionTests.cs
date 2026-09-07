using Game.Composition.Kentridge.Runtime;
using Game.Cutscenes.Api;
using Game.Outcomes.Api;
using MountingForce.WorldGen.Content.Kentridge;
using NUnit.Framework;

namespace Game.Composition.Kentridge.Tests
{
    public sealed class AuthoredFullRunKentridgeCompositionTests
    {
        [Test]
        public void ProductionCompositionOwnsFullHierarchyPhysicalRealizationAndSystem15Query()
        {
            const uint seed = 0x4B454E54u;
            var settlement = KentridgeDefinition.Build(seed);
            var destinationSpeaker = new CutsceneActorId("destination-npc");

            AuthoredFullRunKentridgeComposition composition =
                AuthoredFullRunKentridgeComposition.Build(
                    DialogueOnly("destination-conversation", destinationSpeaker),
                    seed,
                    settlement.CentreDm,
                    voxelsPerDecimetre: 1,
                    configureDestinationCutscene: (scene, roles) =>
                        scene.Bind(destinationSpeaker, roles.DestinationNpc));

            Assert.That(
                composition.PhysicalWorld.Graph.HierarchyPlan.Settlements.Count,
                Is.GreaterThan(1),
                "The production full-run composition must consume the multi-settlement hierarchy, not the opening-only planner.");
            Assert.That(
                ReferenceEquals(composition.Content.Blueprint, composition.PhysicalWorld.Blueprint),
                Is.True);
            Assert.That(
                ReferenceEquals(composition.Generation, composition.World.Generation),
                Is.True);
            Assert.That(composition.Generation.Sites.IsResolved, Is.True);
            Assert.That(composition.World.Npcs.Count, Is.EqualTo(composition.Generation.NpcAssignments.Count));
            Assert.That(composition.World.CutsceneStages.Count, Is.GreaterThan(0));
            Assert.That(
                composition.OutcomeQuery.Snapshot().Lifecycle,
                Is.EqualTo(GameOutcomeLifecycle.Running));
        }

        private static CutsceneDefinition DialogueOnly(string id, CutsceneActorId speaker) =>
            new CutsceneDefinition(
                id,
                CutsceneStageSetupDefinition.Empty,
                new[] { CutsceneStep.Dialogue(speaker, new CutsceneCueId(id + ".dialogue")) });
    }
}
