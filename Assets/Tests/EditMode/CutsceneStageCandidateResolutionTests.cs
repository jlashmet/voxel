using System.Collections.Generic;
using Game.Composition.WorldBuilderWorldGen;
using Game.Cutscenes.Api;
using Game.Cutscenes.Content.Kentridge;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Kentridge;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CutsceneStageCandidateResolutionTests
    {
        private const uint Seed = 0x4B454E54u;

        [Test]
        public void KentridgeOpeningPubResolvesAgainstGeneratedArchitectureStageEnvelope()
        {
            SettlementPlan plan = KentridgeDefinition.Build(Seed);
            var projections = new KentridgeArchitectureSiteProjectionProvider(plan);
            var traversal = new SettlementStreetTraversalFacts(plan, projections);
            var facts = new SettlementPlanWorldBuilderFacts(
                plan,
                new RegionRef("kentridge-region"),
                new SettlementRef("kentridge-settlement"),
                projections,
                traversal,
                projections);

            PlanningGraph graph = BuildOpeningGraph();
            SiteResolutionResult result = SiteRoleResolver.Resolve(graph, facts);

            Assert.IsTrue(result.IsResolved, FirstDiagnostic(result));
            Assert.AreEqual(1, result.Bindings.Count);
            Assert.AreEqual(
                SettlementPlanSiteCandidateFacts.CandidateId(plan.Id, (int)KentridgeRole.Pub),
                result.Bindings[0].Site);
        }

        [Test]
        public void ResolverRejectsAdvertisedCutsceneStageWhenConcreteEnvelopeIsTooNarrow()
        {
            PlanningGraph graph = BuildOpeningGraph();
            var candidate = new SiteCandidate(
                new ResolvedSiteId("narrow-pub"),
                SiteArchetype.Pub,
                new[] { new SiteCapabilityOffer(SiteCapabilityKind.CutsceneStage) });
            var facts = new NarrowStageFacts(candidate, new CutsceneStageEnvelope(12, 80));

            SiteResolutionResult result = SiteRoleResolver.Resolve(graph, facts);

            Assert.IsFalse(result.IsResolved);
            Assert.AreEqual(1, result.Diagnostics.Count);
            Assert.AreEqual("WB3005", result.Diagnostics[0].Code);
            Assert.AreEqual(
                SiteResolutionDiagnosticKind.CapabilityUnsatisfied,
                result.Diagnostics[0].Kind);
        }

        private static PlanningGraph BuildOpeningGraph()
        {
            var game = Campaign.Create("kentridge-opening-site-resolution");
            SiteRef pub = game.World.RequireSite("starting-pub", site => site
                .Archetype(SiteArchetype.Pub));

            game.Story.Cutscene(KentridgeOpeningCutscene.Definition, scene => scene
                .At(pub)
                .Bind(KentridgeOpeningCutscene.Lead, CutsceneActorTarget.Player(0))
                .Bind(KentridgeOpeningCutscene.Madeline, CutsceneActorTarget.Player(1))
                .Bind(KentridgeOpeningCutscene.Steven, CutsceneActorTarget.Player(2))
                .Bind(KentridgeOpeningCutscene.Logan, CutsceneActorTarget.Player(3)));

            return BlueprintCompiler.Compile(game.Build());
        }

        private static string FirstDiagnostic(SiteResolutionResult result) =>
            result.Diagnostics.Count == 0 ? string.Empty : result.Diagnostics[0].ToString();

        private sealed class NarrowStageFacts :
            ISiteCandidateFacts,
            ICutsceneStageCandidateFacts
        {
            private readonly SiteCandidate[] _candidates;
            private readonly CutsceneStageEnvelope _envelope;

            public IReadOnlyList<SiteCandidate> Candidates => _candidates;

            public NarrowStageFacts(
                SiteCandidate candidate,
                CutsceneStageEnvelope envelope)
            {
                _candidates = new[] { candidate };
                _envelope = envelope;
            }

            public bool TryGetCutsceneStageEnvelope(
                ResolvedSiteId candidate,
                out CutsceneStageEnvelope envelope)
            {
                envelope = _envelope;
                return candidate.Equals(_candidates[0].Id);
            }

            public bool IsInRegion(ResolvedSiteId candidate, RegionRef region) => true;
            public bool IsInSettlement(ResolvedSiteId candidate, SettlementRef settlement) => true;
            public bool IsReachable(
                ResolvedSiteId subject,
                ResolvedSiteId target,
                TraversalProfile traversal) => true;
            public int BoundaryDistanceMetres(ResolvedSiteId subject, ResolvedSiteId target) => 0;
            public int PublicEntranceDistanceMetres(ResolvedSiteId subject, ResolvedSiteId target) => 0;
            public int TraversalDistanceMetres(
                ResolvedSiteId subject,
                ResolvedSiteId target,
                TraversalProfile traversal) => 0;
        }
    }
}
