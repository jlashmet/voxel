using System.Linq;
using Game.Composition.WorldBuilderWorldGen;
using Game.Cutscenes.Content.Kentridge;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Kentridge;
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
            Assert.That(
                resolution.Bindings.Single(value => value.Role.Equals(startingPub)).Site,
                Is.EqualTo(SettlementPlanSiteCandidateFacts.CandidateId(
                    plan.Id,
                    (int)KentridgeRole.Pub)));

            SiteCandidate pub = facts.Candidates.Single(value =>
                value.Id.Equals(SettlementPlanSiteCandidateFacts.CandidateId(
                    plan.Id,
                    (int)KentridgeRole.Pub)));
            SiteCapabilityOffer spawn = pub.Capabilities.Single(value =>
                value.Kind == SiteCapabilityKind.PlayerSpawn);
            Assert.That(spawn.Capacity, Is.EqualTo(5));
        }
    }
}
