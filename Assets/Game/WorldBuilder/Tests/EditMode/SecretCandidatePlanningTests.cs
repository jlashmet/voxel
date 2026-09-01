using System.Linq;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class SecretCandidatePlanningTests
    {
        [Test]
        public void CompilerCreatesSecretCandidateWorkOnlyForCapableSites()
        {
            var game = Campaign.Create("secret-candidate-planning");

            SiteRef dungeon = game.World.RequireSite("dungeon", site => site
                .Archetype(SiteArchetype.Dungeon)
                .RequireCapability(SiteCapability.SecretCandidateHost));

            SiteRef roadsideCamp = game.World.RequireSite("roadside-camp", site => site
                .Archetype(SiteArchetype.Camp));

            LootTableRef treasure = game.Loot.Table("secret-treasure", loot => loot
                .RollCount(1, 2)
                .Guaranteed(LootCategory.Currency));

            SecretPolicyRef policyRef = game.World.Secrets.Policy("false-wall-secrets", secret => secret
                .Entrance(SecretEntranceType.DestroyableFalseWall)
                .Distribution(new SecretDistribution(1, 3, 3500))
                .RequireHiddenSpace()
                .RewardWith(treasure));

            PlanningGraph graph = BlueprintCompiler.Compile(game.Build());

            Assert.That(graph.SecretCandidates.Count, Is.EqualTo(1));
            SecretCandidatePlan plan = graph.SecretCandidates.Single();
            Assert.That(plan.Policy, Is.EqualTo(policyRef));
            Assert.That(plan.Site, Is.EqualTo(dungeon));
            Assert.That(plan.RequiresHiddenSpace, Is.True);
            Assert.That(plan.MinimumCandidateCount, Is.EqualTo(1));
            Assert.That(plan.PreferredCandidateCount, Is.EqualTo(3));
            Assert.That(plan.AllowedEntrances.Single(), Is.EqualTo(SecretEntranceType.DestroyableFalseWall));

            PlanningNode policyNode = graph.Nodes.Single(node =>
                node.Kind == PlanningNodeKind.SecretPolicy && node.Id == "secret-policy:false-wall-secrets");
            Assert.That(policyNode.Dependencies, Does.Contain("site:dungeon"));
            Assert.That(policyNode.Dependencies, Does.Not.Contain("site:roadside-camp"));
        }
    }
}
