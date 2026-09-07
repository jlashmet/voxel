using Game.Composition.Campaign.Content;
using Game.Composition.Kentridge.Runtime;
using Game.Composition.WorldBuilderWorldGen;
using Game.Composition.WorldBuilderWorldGen.Runtime;
using Game.Cutscenes.Api;
using Game.Outcomes.Api;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
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

        [Test]
        public void RichOpeningRealizationOverlaysMatchingFullRunSemanticIdentities()
        {
            const uint seed = 0x4B454E54u;
            var destinationSpeaker = new CutsceneActorId("destination-npc");
            CutsceneDefinition destination = DialogueOnly(
                "destination-conversation",
                destinationSpeaker);

            KnownOpeningCampaignContent opening = KnownOpeningCampaignContent.Build(
                destination,
                (scene, roles) => scene.Bind(destinationSpeaker, roles.DestinationNpc));
            AuthoredTownPlan town = WorldBuilderTownAuthoring.Author(
                WorldBuilderTownIds.Kentridge,
                seed);
            SettlementPlan settlement = KentridgeDefinition.Build(seed);
            KentridgeCampaignGenerationPlan openingGeneration =
                KentridgeCampaignSessionBootstrap.Plan(opening.Blueprint, town);
            KentridgeCampaignWorldRealization openingWorld =
                KentridgeCampaignWorldRealizationBoundary.Realize(
                    openingGeneration,
                    new KentridgeCampaignRealizationFacts(
                        new KentridgeVoxelSiteRealizationFacts(settlement, 1)));

            AuthoredFullRunKentridgeComposition composition =
                AuthoredFullRunKentridgeComposition.Build(
                    destination,
                    seed,
                    settlement.CentreDm,
                    voxelsPerDecimetre: 1,
                    configureDestinationCutscene: (scene, roles) =>
                        scene.Bind(destinationSpeaker, roles.DestinationNpc),
                    richOpeningWorld: openingWorld);

            CutsceneStageRealization exactIntro = FindStage(
                openingWorld,
                opening.IntroCutscene);
            CutsceneStageRealization fullIntro = FindStage(
                composition.World,
                composition.Content.IntroCutscene);
            Assert.That(
                ReferenceEquals(fullIntro, exactIntro),
                Is.True,
                "The full-run session must use the exact rich Kentridge opening stage instead of the macro fallback anchor.");

            ResolvedNpcWorldPlacement exactDestination = FindNpc(
                openingWorld,
                opening.DestinationNpc);
            ResolvedNpcWorldPlacement fullDestination = FindNpc(
                composition.World,
                composition.Content.DestinationNpc);
            Assert.That(
                ReferenceEquals(fullDestination, exactDestination),
                Is.True,
                "The full-run session must preserve the exact rich Kentridge opening NPC placement.");
            Assert.That(
                composition.World.Npcs.Count,
                Is.GreaterThan(openingWorld.Npcs.Count),
                "Continuation NPCs must remain present after the rich opening overlay.");
        }

        private static CutsceneStageRealization FindStage(
            KentridgeCampaignWorldRealization world,
            CutsceneRef cutscene)
        {
            for (var i = 0; i < world.CutsceneStages.Count; i++)
                if (world.CutsceneStages[i].Cutscene.Equals(cutscene))
                    return world.CutsceneStages[i];
            Assert.Fail("Opening realization did not contain cutscene stage '" + cutscene + "'.");
            return null;
        }

        private static CutsceneStageRealization FindStage(
            AuthoredFullRunCampaignWorldRealization world,
            CutsceneRef cutscene)
        {
            for (var i = 0; i < world.CutsceneStages.Count; i++)
                if (world.CutsceneStages[i].Cutscene.Equals(cutscene))
                    return world.CutsceneStages[i];
            Assert.Fail("Full-run realization did not contain cutscene stage '" + cutscene + "'.");
            return null;
        }

        private static ResolvedNpcWorldPlacement FindNpc(
            KentridgeCampaignWorldRealization world,
            NpcRef npc)
        {
            for (var i = 0; i < world.Npcs.Count; i++)
                if (world.Npcs[i].Npc.Equals(npc))
                    return world.Npcs[i];
            Assert.Fail("Opening realization did not contain NPC '" + npc + "'.");
            return null;
        }

        private static ResolvedNpcWorldPlacement FindNpc(
            AuthoredFullRunCampaignWorldRealization world,
            NpcRef npc)
        {
            for (var i = 0; i < world.Npcs.Count; i++)
                if (world.Npcs[i].Npc.Equals(npc))
                    return world.Npcs[i];
            Assert.Fail("Full-run realization did not contain NPC '" + npc + "'.");
            return null;
        }

        private static CutsceneDefinition DialogueOnly(string id, CutsceneActorId speaker) =>
            new CutsceneDefinition(
                id,
                CutsceneStageSetupDefinition.Empty,
                new[] { CutsceneStep.Dialogue(speaker, new CutsceneCueId(id + ".dialogue")) });
    }
}
