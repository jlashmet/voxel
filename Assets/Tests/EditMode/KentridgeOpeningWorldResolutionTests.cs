using System.Linq;
using Game.Composition.WorldBuilderWorldGen;
using Game.Cutscenes.Content.Kentridge;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class KentridgeOpeningWorldResolutionTests
    {
        private const uint Seed = 0x4B454E54u;

        [Test]
        public void AuthoredOpeningPubResolvesAgainstMeasuredKentridgeCapabilities()
        {
            var game = Campaign.Create("kentridge-opening-resolution");
            SiteRef startingPub = game.World.RequireSite("starting-pub", site => site
                .Archetype(SiteArchetype.Pub)
                .RequireCapability(SiteCapability.Interior)
                .RequireCapability(SiteCapability.PlayerSpawn(4))
                .RequireCapability(SiteCapability.PublicExit));

            NpcRef madeline = game.World.RequireNpc("madeline", npc => npc.PlaceAt(startingPub));
            NpcRef steven = game.World.RequireNpc("steven", npc => npc.PlaceAt(startingPub));
            NpcRef logan = game.World.RequireNpc("logan", npc => npc.PlaceAt(startingPub));

            game.Story.Cutscene(KentridgeOpeningCutscene.Definition, scene => scene
                .At(startingPub)
                .Bind(KentridgeOpeningCutscene.Lead, CutsceneActorTarget.Player(0))
                .Bind(KentridgeOpeningCutscene.Madeline, CutsceneActorTarget.Npc(madeline))
                .Bind(KentridgeOpeningCutscene.Steven, CutsceneActorTarget.Npc(steven))
                .Bind(KentridgeOpeningCutscene.Logan, CutsceneActorTarget.Npc(logan)));

            PlanningGraph graph = BlueprintCompiler.Compile(game.Build());
            SettlementPlan plan = KentridgeDefinition.Build(Seed);
            var projections = new KentridgeArchitectureSiteProjectionProvider(plan);
            var traversal = new SettlementStreetTraversalFacts(plan, projections);
            var facts = new SettlementPlanWorldBuilderFacts(
                plan,
                new RegionRef("kentridge-region"),
                new SettlementRef("kentridge"),
                projections,
                traversal,
                projections);

            SiteResolutionResult resolution = SiteRoleResolver.Resolve(graph, facts);

            Assert.That(resolution.IsResolved, Is.True,
                resolution.Diagnostics.Count == 0
                    ? string.Empty
                    : string.Join("\n", resolution.Diagnostics.Select(value => value.ToString())));
            ResolvedSiteId resolvedPub = SettlementPlanSiteCandidateFacts.CandidateId(
                plan.Id,
                (int)KentridgeRole.Pub);
            Assert.That(
                resolution.Bindings.Single(value => value.Role.Equals(startingPub)).Site,
                Is.EqualTo(resolvedPub));

            SiteCandidate pub = facts.Candidates.Single(value => value.Id.Equals(resolvedPub));
            SiteCapabilityOffer spawn = pub.Capabilities.Single(value =>
                value.Kind == SiteCapabilityKind.PlayerSpawn);
            Assert.That(spawn.Capacity, Is.EqualTo(5));

            var realizationFacts = new KentridgeVoxelSiteRealizationFacts(plan, 1);
            var npcAssignments = NpcPlacementResolver.ResolveSites(graph, resolution);
            var npcPlacements = KentridgeNpcWorldPlacementResolver.Resolve(
                npcAssignments,
                plan,
                realizationFacts);

            Assert.That(npcPlacements.Count, Is.EqualTo(3));
            Assert.That(npcPlacements.All(value => value.Site.Equals(resolvedPub)), Is.True);
            CollectionAssert.AreEquivalent(
                new[] { "madeline", "steven", "logan" },
                npcPlacements.Select(value => value.Npc.Id).ToArray());
            Assert.That(
                npcPlacements
                    .Select(value => value.Position.Position.X + ":" +
                                     value.Position.Position.Y + ":" +
                                     value.Position.Position.Z)
                    .Distinct()
                    .Count(),
                Is.EqualTo(3),
                "The three authored pub NPCs must receive distinct physical interior positions.");

            RealizedWorldPoint entrance;
            Assert.That(
                realizationFacts.TryGetPublicEntrance((int)KentridgeRole.Pub, out entrance),
                Is.True);
            Assert.That(
                npcPlacements.All(value => value.Position.Position.Y == entrance.Position.Y),
                Is.True,
                "NPC placement and cutscene staging must share the backend's realized ground-floor height.");

            var cutsceneGeometry = new SettlementCutsceneSiteGeometryProvider(
                plan,
                resolution,
                projections,
                realizationFacts);
            var stages = CutsceneStageRealizer.Realize(graph, cutsceneGeometry);

            Assert.That(stages.Count, Is.EqualTo(1));
            CutsceneStageRealization opening = stages.Single();
            Assert.That(opening.Site, Is.EqualTo(startingPub));
            Assert.That(opening.Cutscene, Is.EqualTo(graph.CutsceneStages.Single().Cutscene));

            var authoredPoints = new[]
            {
                KentridgeOpeningCutscene.LeadStart,
                KentridgeOpeningCutscene.MadelineStage,
                KentridgeOpeningCutscene.StevenStage,
                KentridgeOpeningCutscene.LoganStart,
                KentridgeOpeningCutscene.LeadStage,
                KentridgeOpeningCutscene.EntranceFocus,
                KentridgeOpeningCutscene.LoganStop,
            };
            for (var i = 0; i < authoredPoints.Length; i++)
            {
                var point = opening.Binding.Resolve(authoredPoints[i]);
                Assert.That(point.Position.Y, Is.EqualTo(entrance.Position.Y),
                    "Every opening stage point must use the same realized pub floor as NPC placement.");
            }
        }
    }
}
